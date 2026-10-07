using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Diagnostics.Uds;
using CanEngineeringPlatform.Core.Diagnostics.Obd;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.EcuInfo;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.Diagnostics.Transport;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;

namespace CanEngineeringPlatform.Core.Diagnostics.Session;

public interface IDiagnosticSessionService
{
    IReadOnlyList<DiagnosticExchange> GetRecentExchanges(int maxCount = 200);
    IReadOnlyList<DiagnosticTroubleCode> GetActiveDtcList();
    IReadOnlyList<EcuIdentification> GetEcuIdentifications();
    EcuIdentification? GetEcuInfo(string ecuName);
    IReadOnlyList<ObdPidValue> GetLatestObdValues();
    Task<DiagnosticExchange> SendRequestAsync(uint requestCanId, byte[] payload, string ecuName = "ECM", bool isSimulation = true);
    Task ScanAllDtcsAsync(bool isSimulation = true);
    Task ClearDtcsAsync(uint requestCanId, string ecuName = "ECM", bool isSimulation = true);
    Task<DtcClearVerificationResult> ClearAndVerifyDtcsAsync(uint requestCanId, string ecuName = "ECM", bool isSimulation = true);
    void ClearHistory();
    event Action<DiagnosticExchange>? OnExchangeUpdated;
    event Action<IReadOnlyList<DiagnosticTroubleCode>>? OnDtcsUpdated;
}

public class DiagnosticSessionService : IDiagnosticSessionService, IDisposable
{
    private readonly ICanPipeline _pipeline;
    private readonly IIsoTpRouter _isoTpRouter;
    private readonly IEcuSimulator _ecuSimulator;
    private readonly IHardwareManager? _hardwareManager;
    private readonly IDiagnosticTransport? _diagnosticTransport;

    private readonly ConcurrentDictionary<string, DiagnosticExchange> _activeExchanges = new();
    private readonly List<DiagnosticExchange> _exchangeHistory = new();
    private readonly object _historyLock = new();

    private readonly ConcurrentDictionary<string, EcuIdentification> _ecuIdentities = new();
    private readonly ConcurrentDictionary<string, DiagnosticTroubleCode> _activeDtcs = new();
    private readonly ConcurrentDictionary<byte, ObdPidValue> _latestObdValues = new();

    public event Action<DiagnosticExchange>? OnExchangeUpdated;
    public event Action<IReadOnlyList<DiagnosticTroubleCode>>? OnDtcsUpdated;

    public DiagnosticSessionService(
        ICanPipeline pipeline,
        IIsoTpRouter isoTpRouter,
        IEcuSimulator ecuSimulator)
        : this(pipeline, isoTpRouter, ecuSimulator, null, null)
    {
    }

    public DiagnosticSessionService(
        ICanPipeline pipeline,
        IIsoTpRouter isoTpRouter,
        IEcuSimulator ecuSimulator,
        IHardwareManager? hardwareManager)
        : this(pipeline, isoTpRouter, ecuSimulator, hardwareManager, null)
    {
    }

    public DiagnosticSessionService(
        ICanPipeline pipeline,
        IIsoTpRouter isoTpRouter,
        IEcuSimulator ecuSimulator,
        IHardwareManager? hardwareManager,
        IDiagnosticTransport? diagnosticTransport)
    {
        _pipeline = pipeline;
        _isoTpRouter = isoTpRouter;
        _ecuSimulator = ecuSimulator;
        _hardwareManager = hardwareManager;
        _diagnosticTransport = diagnosticTransport;

        // Initialize target ECUs
        InitializeEcu("ECM", 0x7E0, 0x7E8);
        InitializeEcu("ABS", 0x7E1, 0x7E9);
        InitializeEcu("BCM", 0x7E2, 0x7EA);

        _pipeline.OnFrameReceived += HandleCanFrame;
        if (_diagnosticTransport != null)
        {
            _diagnosticTransport.OnPayloadReceived += HandleDiagnosticPayload;
        }
    }

    private void InitializeEcu(string name, uint reqId, uint respId)
    {
        _ecuIdentities[name] = new EcuIdentification
        {
            EcuName = name,
            RequestCanId = reqId,
            ResponseCanId = respId
        };
    }

    private void HandleCanFrame(CanFrame frame)
    {
        if (_isoTpRouter.RouteCanFrame(frame, out var isoTpMsg) && isoTpMsg != null)
        {
            ProcessCompletedIsoTpMessage(isoTpMsg);
        }
    }

    private void ProcessCompletedIsoTpMessage(IsoTpMessage msg)
    {
        if (msg.Payload == null || msg.Payload.Length == 0) return;

        byte sid = msg.Payload[0];
        string ecuName = GetEcuNameByResponseCanId(msg.CanId);
        DateTime now = DateTime.UtcNow;

        // Check if this is a response to an ongoing exchange
        var exchange = _exchangeHistory.LastOrDefault(e => e.ResponseCanId == msg.CanId && !e.IsComplete);
        if (exchange == null)
        {
            exchange = new DiagnosticExchange
            {
                EcuName = ecuName,
                ResponseCanId = msg.CanId,
                TimestampUtc = now,
                TimestampUs = msg.TimestampUs
            };
            lock (_historyLock)
            {
                _exchangeHistory.Add(exchange);
                if (_exchangeHistory.Count > 1000) _exchangeHistory.RemoveAt(0);
            }
        }

        exchange.ResponseDataHex = BitConverter.ToString(msg.Payload).Replace("-", " ");
        exchange.IsComplete = true;
        exchange.ResponseTimeMs = Math.Max(1.0, Math.Round((now - exchange.TimestampUtc).TotalMilliseconds, 1));

        // 1. Check if Negative Response (0x7F)
        if (sid == (byte)UdsServiceId.NegativeResponse)
        {
            var udsResp = UdsDecoder.ParseResponse(msg.Payload);
            exchange.IsSuccess = false;
            exchange.ServiceId = (byte)udsResp.RequestedServiceId;
            exchange.ServiceName = udsResp.RequestedServiceId.ToString();
            exchange.NrcHex = udsResp.Nrc.HasValue ? $"0x{(byte)udsResp.Nrc.Value:X2}" : null;
            exchange.NrcDescription = udsResp.NrcDescription;
            exchange.Summary = udsResp.DecodedSummary ?? "Negative Response";
        }
        // 2. Check if OBD-II Positive Response (0x41 .. 0x49)
        else if (sid >= 0x41 && sid <= 0x4A)
        {
            exchange.Protocol = "OBD-II";
            var obdResp = ObdDecoder.ParseResponse(msg.Payload);
            exchange.IsSuccess = true;
            exchange.ServiceId = (byte)obdResp.Mode;
            exchange.ServiceName = $"Mode {obdResp.Mode}";
            exchange.Summary = obdResp.DecodedSummary;

            // Store live OBD values
            foreach (var pv in obdResp.PidValues)
            {
                _latestObdValues[pv.Pid] = pv;
            }

            // Store OBD DTCs
            if (obdResp.DtcCodes.Count > 0)
            {
                foreach (var dtc in obdResp.DtcCodes)
                {
                    _activeDtcs[dtc] = new DiagnosticTroubleCode
                    {
                        Code = dtc,
                        Description = DtcDecoder.GetDescription(dtc),
                        EcuName = ecuName,
                        CanId = msg.CanId,
                        StatusByte = 0x08 // Confirmed
                    };
                }
                OnDtcsUpdated?.Invoke(GetActiveDtcList());
            }
        }
        // 3. Check if UDS Positive Response (0x50 .. 0x7E)
        else if (sid >= 0x50 && sid <= 0x7E)
        {
            exchange.Protocol = "UDS";
            var udsResp = UdsDecoder.ParseResponse(msg.Payload);
            exchange.IsSuccess = true;
            exchange.ServiceId = (byte)udsResp.RequestedServiceId;
            exchange.ServiceName = udsResp.RequestedServiceId.ToString();
            exchange.Summary = udsResp.DecodedSummary ?? "Positive Response";

            // If DID Response (0x22 -> 0x62), update ECU Identification
            if (udsResp.RequestedServiceId == UdsServiceId.ReadDataByIdentifier && udsResp.DataIdentifier.HasValue)
            {
                if (_ecuIdentities.TryGetValue(ecuName, out var ecu))
                {
                    EcuInfoDecoder.ApplyDidResponse(ecu, udsResp.DataIdentifier.Value, udsResp.Data);
                }
            }
            // If DTC Response (0x19 -> 0x59), update active DTCs
            else if (udsResp.RequestedServiceId == UdsServiceId.ReadDTCInformation)
            {
                var dtcs = DtcDecoder.ParseUds0x19Response(msg.Payload, ecuName, msg.CanId);
                foreach (var d in dtcs)
                {
                    _activeDtcs[d.Code] = d;
                }
                OnDtcsUpdated?.Invoke(GetActiveDtcList());
            }
            // If Clear DTCs Response (0x14 -> 0x54), clear DTCs
            else if (udsResp.RequestedServiceId == UdsServiceId.ClearDiagnosticInformation)
            {
                foreach (var key in _activeDtcs.Keys.ToList())
                {
                    if (_activeDtcs[key].EcuName == ecuName)
                    {
                        _activeDtcs.TryRemove(key, out _);
                    }
                }
                OnDtcsUpdated?.Invoke(GetActiveDtcList());
            }
        }

        OnExchangeUpdated?.Invoke(exchange);
    }

    public async Task<DiagnosticExchange> SendRequestAsync(uint requestCanId, byte[] payload, string ecuName = "ECM", bool isSimulation = true)
    {
        uint responseCanId = GetResponseCanIdByRequestCanId(requestCanId);
        var exchange = new DiagnosticExchange
        {
            EcuName = ecuName,
            RequestCanId = requestCanId,
            ResponseCanId = responseCanId,
            ServiceId = payload != null && payload.Length > 0 ? payload[0] : (byte)0,
            RequestDataHex = payload != null ? BitConverter.ToString(payload).Replace("-", " ") : string.Empty,
            TimestampUtc = DateTime.UtcNow,
            IsComplete = false
        };

        if (payload != null && payload.Length > 0)
        {
            byte sid = payload[0];
            if (sid <= 0x09)
            {
                exchange.Protocol = "OBD-II";
                exchange.ServiceName = $"Mode 0{sid}";
                if (payload.Length > 1) exchange.SubFunction = payload[1];
            }
            else
            {
                exchange.Protocol = "UDS";
                exchange.ServiceName = ((UdsServiceId)sid).ToString();
                if (payload.Length > 1) exchange.SubFunction = payload[1];
                if (sid == (byte)UdsServiceId.ReadDataByIdentifier && payload.Length >= 3)
                {
                    exchange.DataIdentifier = (ushort)((payload[1] << 8) | payload[2]);
                }
            }
        }

        lock (_historyLock)
        {
            _exchangeHistory.Add(exchange);
            if (_exchangeHistory.Count > 1000) _exchangeHistory.RemoveAt(0);
        }

        if (isSimulation)
        {
            await _ecuSimulator.ProcessAndPublishSimulationResponseAsync(requestCanId, payload ?? Array.Empty<byte>());
        }
        else if (_diagnosticTransport != null)
        {
            await _diagnosticTransport.SendDiagnosticPayloadAsync(requestCanId, payload ?? Array.Empty<byte>());
        }
        else
        {
            // Explicit live transmission: segment request and send via hardware transport
            var frames = _isoTpRouter.SegmentMessage(payload ?? Array.Empty<byte>(), requestCanId);
            foreach (var f in frames)
            {
                if (_hardwareManager != null)
                {
                    await _hardwareManager.SendFrameAsync(f);
                }
                else
                {
                    await _pipeline.PublishFrameAsync(f);
                }
            }
        }

        return exchange;
    }

    public async Task<DtcClearVerificationResult> ClearAndVerifyDtcsAsync(uint requestCanId, string ecuName = "ECM", bool isSimulation = true)
    {
        // 1. Snapshot Pre-Clear State
        var preClearList = _activeDtcs.Values
            .Where(d => string.Equals(d.EcuName, ecuName, StringComparison.OrdinalIgnoreCase) || ecuName == "Broadcast" || requestCanId == 0x7DF)
            .ToList();

        var result = new DtcClearVerificationResult
        {
            EcuName = ecuName,
            RequestCanId = requestCanId,
            PreClearDtcCount = preClearList.Count,
            PreClearCodes = preClearList.Select(d => d.Code).Distinct().ToList(),
            TimestampUtc = DateTime.UtcNow
        };

        // 2. Transmit UDS 0x14 0xFF 0xFF 0xFF (ClearDiagnosticInformation)
        byte[] udsClearPayload = new byte[] { 0x14, 0xFF, 0xFF, 0xFF };
        var udsExchange = await SendRequestAsync(requestCanId, udsClearPayload, ecuName, isSimulation);

        int waitClear = 0;
        while (!udsExchange.IsComplete && waitClear < 600)
        {
            await Task.Delay(15);
            waitClear += 15;
        }

        result.RawClearResponseHex = udsExchange.ResponseDataHex;

        // Check for Negative Response Codes (NRC)
        if (!string.IsNullOrEmpty(udsExchange.NrcHex))
        {
            if (udsExchange.NrcHex.Contains("22")) // ConditionsNotCorrect
            {
                result.Status = DtcClearStatus.ConditionsNotCorrect;
                result.Message = "ECU rejected clear: Vehicle conditions not correct (ensure engine is stopped and ignition is ON).";
                return result;
            }
            if (udsExchange.NrcHex.Contains("33")) // SecurityAccessDenied
            {
                result.Status = DtcClearStatus.SecurityRequired;
                result.Message = "ECU rejected clear: Security Access (0x27) authorization required.";
                return result;
            }
            if (udsExchange.NrcHex.Contains("11") || udsExchange.NrcHex.Contains("12")) // Service/Subfunction not supported
            {
                result.Status = DtcClearStatus.NotSupported;
                result.Message = "ECU rejected clear: Service 0x14 not supported by target controller.";
                return result;
            }
            if (udsExchange.NrcHex.Contains("7E") || udsExchange.NrcHex.Contains("7F"))
            {
                result.Status = DtcClearStatus.SessionRequired;
                result.Message = "ECU rejected clear: Extended diagnostic session (0x10 0x03) required.";
                return result;
            }
            result.Status = DtcClearStatus.Refused;
            result.Message = $"ECU rejected clear: NRC {udsExchange.NrcHex} ({udsExchange.NrcDescription ?? "Negative Response"}).";
            return result;
        }

        if (!udsExchange.IsComplete || !udsExchange.IsSuccess)
        {
            result.Status = DtcClearStatus.Timeout;
            result.Message = "No positive response received from ECU within timeout window.";
            return result;
        }

        // Also issue OBD Mode 04 if standard OBD addressing
        if (requestCanId == 0x7E0 || requestCanId == 0x7DF)
        {
            byte[] obdClearPayload = new byte[] { 0x04 };
            await SendRequestAsync(0x7DF, obdClearPayload, ecuName, isSimulation);
        }

        // 3. Post-Clear Re-Read & Validation Scan
        await Task.Delay(50);
        var postScanExchange = await SendRequestAsync(requestCanId, new byte[] { 0x19, 0x02, 0xFF }, ecuName, isSimulation);
        int waitPostScan = 0;
        while (!postScanExchange.IsComplete && waitPostScan < 600)
        {
            await Task.Delay(15);
            waitPostScan += 15;
        }

        // 4. Compare post-clear active list
        var postClearRemaining = _activeDtcs.Values
            .Where(d => string.Equals(d.EcuName, ecuName, StringComparison.OrdinalIgnoreCase) || ecuName == "Broadcast" || requestCanId == 0x7DF)
            .ToList();

        result.PostClearDtcCount = postClearRemaining.Count;
        result.PostClearRemainingCodes = postClearRemaining.Select(d => d.Code).Distinct().ToList();

        if (postClearRemaining.Count == 0)
        {
            result.Status = DtcClearStatus.Cleared;
            result.Message = $"Successfully cleared and verified {result.PreClearDtcCount} DTC(s) on {ecuName}.";
        }
        else
        {
            result.Status = DtcClearStatus.StillPresent;
            result.Message = $"Clear command acknowledged, but {postClearRemaining.Count} fault code(s) remain active or re-triggered immediately.";
        }

        OnDtcsUpdated?.Invoke(GetActiveDtcList());
        return result;
    }

    public async Task ClearDtcsAsync(uint requestCanId, string ecuName = "ECM", bool isSimulation = true)
    {
        await ClearAndVerifyDtcsAsync(requestCanId, ecuName, isSimulation);
    }

    public async Task ScanAllDtcsAsync(bool isSimulation = true)
    {
        // 1. Scan ECM via UDS 0x19 0x02 0xFF (ReadDTCInformation by status mask)
        var exEcm = await SendRequestAsync(0x7E0, new byte[] { 0x19, 0x02, 0xFF }, "ECM", isSimulation);
        int waitEcm = 0;
        while (!exEcm.IsComplete && waitEcm < 500)
        {
            await Task.Delay(15);
            waitEcm += 15;
        }

        // 2. Scan OBD-II Mode 03 (Confirmed emission DTCs)
        await SendRequestAsync(0x7DF, new byte[] { 0x03 }, "ECM", isSimulation);

        // 3. Scan OBD-II Mode 07 (Pending emission DTCs)
        await SendRequestAsync(0x7DF, new byte[] { 0x07 }, "ECM", isSimulation);

        // 4. Scan ABS via UDS 0x19 0x02 0xFF
        await SendRequestAsync(0x7E1, new byte[] { 0x19, 0x02, 0xFF }, "ABS", isSimulation);

        // 5. Scan BCM via UDS 0x19 0x02 0xFF
        await SendRequestAsync(0x7E2, new byte[] { 0x19, 0x02, 0xFF }, "BCM", isSimulation);
    }

    public IReadOnlyList<DiagnosticExchange> GetRecentExchanges(int maxCount = 200)
    {
        lock (_historyLock)
        {
            int count = Math.Min(maxCount, _exchangeHistory.Count);
            return _exchangeHistory.Skip(_exchangeHistory.Count - count).ToList();
        }
    }

    public IReadOnlyList<DiagnosticTroubleCode> GetActiveDtcList() => new List<DiagnosticTroubleCode>(_activeDtcs.Values);

    public IReadOnlyList<EcuIdentification> GetEcuIdentifications() => new List<EcuIdentification>(_ecuIdentities.Values);

    public EcuIdentification? GetEcuInfo(string ecuName)
    {
        _ecuIdentities.TryGetValue(ecuName, out var info);
        return info;
    }

    public IReadOnlyList<ObdPidValue> GetLatestObdValues() => new List<ObdPidValue>(_latestObdValues.Values);

    public void ClearHistory()
    {
        lock (_historyLock)
        {
            _exchangeHistory.Clear();
        }
        _activeDtcs.Clear();
        _latestObdValues.Clear();
    }

    private string GetEcuNameByResponseCanId(uint canId) => canId switch
    {
        0x7E8 => "ECM",
        0x7E9 => "ABS",
        0x7EA => "BCM",
        0x7EB => "SRS",
        0x7EC => "CLUSTER",
        _ => $"ECU_0x{canId:X3}"
    };

    private uint GetResponseCanIdByRequestCanId(uint reqId) => reqId switch
    {
        0x7DF => 0x7E8,
        0x7E0 => 0x7E8,
        0x7E1 => 0x7E9,
        0x7E2 => 0x7EA,
        0x7E3 => 0x7EB,
        0x7E4 => 0x7EC,
        _ => reqId + 8
    };

    private void HandleDiagnosticPayload(DiagnosticPayloadReceivedEventArgs args)
    {
        var msg = new IsoTpMessage
        {
            CanId = args.SourceAddress,
            Payload = args.Payload,
            TimestampUs = (uint)args.TimestampUs
        };
        ProcessCompletedIsoTpMessage(msg);
    }

    public void Dispose()
    {
        _pipeline.OnFrameReceived -= HandleCanFrame;
        if (_diagnosticTransport != null)
        {
            _diagnosticTransport.OnPayloadReceived -= HandleDiagnosticPayload;
        }
    }
}
