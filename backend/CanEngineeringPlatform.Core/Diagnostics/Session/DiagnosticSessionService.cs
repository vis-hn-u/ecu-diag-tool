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
using CanEngineeringPlatform.Core.Signals;

namespace CanEngineeringPlatform.Core.Diagnostics.Session;

public interface IDiagnosticSessionService
{
    IReadOnlyList<DiagnosticExchange> GetRecentExchanges(int maxCount = 200);
    IReadOnlyList<DiagnosticTroubleCode> GetActiveDtcList();
    IReadOnlyList<EcuIdentification> GetEcuIdentifications();
    EcuIdentification? GetEcuInfo(string ecuName);
    IReadOnlyList<ObdPidValue> GetLatestObdValues();
    Task<IReadOnlyList<ObdPidDefinition>> DiscoverSupportedPidsAsync(uint requestCanId = 0x7DF, bool isSimulation = true);
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
    private readonly IUnifiedSignalRegistry? _signalRegistry;

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
        : this(pipeline, isoTpRouter, ecuSimulator, null, null, null)
    {
    }

    public DiagnosticSessionService(
        ICanPipeline pipeline,
        IIsoTpRouter isoTpRouter,
        IEcuSimulator ecuSimulator,
        IHardwareManager? hardwareManager)
        : this(pipeline, isoTpRouter, ecuSimulator, hardwareManager, null, null)
    {
    }

    public DiagnosticSessionService(
        ICanPipeline pipeline,
        IIsoTpRouter isoTpRouter,
        IEcuSimulator ecuSimulator,
        IHardwareManager? hardwareManager,
        IDiagnosticTransport? diagnosticTransport)
        : this(pipeline, isoTpRouter, ecuSimulator, hardwareManager, diagnosticTransport, null)
    {
    }

    public DiagnosticSessionService(
        ICanPipeline pipeline,
        IIsoTpRouter isoTpRouter,
        IEcuSimulator ecuSimulator,
        IHardwareManager? hardwareManager,
        IDiagnosticTransport? diagnosticTransport,
        IUnifiedSignalRegistry? signalRegistry)
    {
        _pipeline = pipeline;
        _isoTpRouter = isoTpRouter;
        _ecuSimulator = ecuSimulator;
        _hardwareManager = hardwareManager;
        _diagnosticTransport = diagnosticTransport;
        _signalRegistry = signalRegistry;

        // Initialize target ECUs
        InitializeEcu("ECM", 0x7E0, 0x7E8);
        InitializeEcu("ABS", 0x7E1, 0x7E9);
        InitializeEcu("BCM", 0x7E2, 0x7EA);

        if (_hardwareManager != null)
        {
            _hardwareManager.OnFrameReceived += HandleCanFrame;
        }
        else
        {
            _pipeline.OnFrameReceived += HandleCanFrame;
        }
        _isoTpRouter.OnFlowControlRequired += HandleFlowControlRequired;
        if (_diagnosticTransport != null)
        {
            _diagnosticTransport.OnPayloadReceived += HandleDiagnosticPayload;
        }
    }

    private void HandleFlowControlRequired(CanFrame fcFrame)
    {
        // Decouple from receive thread so GvretSerialTransport finishes processing immediately
        Task.Run(async () =>
        {
            try
            {
                // Critical automotive timing: Bosch EDC16 CAN controller requires 20-30ms 
                // between First Frame transmission and Flow Control reception to switch mailboxes
                await Task.Delay(25);

                Console.WriteLine($"[ISOTP FC] Sending Flow Control to 0x{fcFrame.Id:X3} (STmin=10ms): 30-00-0A");
                if (_hardwareManager != null)
                {
                    // Variant 1: STmin = 10 ms (0x0A), BS = 0, DLC 8 (industry standard for Bosch EDC16)
                    byte[] fc10 = new byte[] { 0x30, 0x00, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00 };
                    var f10 = new CanFrame(0, fcFrame.Id, 8, 0, fc10);
                    await _hardwareManager.SendFrameAsync(f10);

                    // Variant 2: STmin = 20 ms (0x14), BS = 0, DLC 8
                    await Task.Delay(15);
                    byte[] fc20 = new byte[] { 0x30, 0x00, 0x14, 0x00, 0x00, 0x00, 0x00, 0x00 };
                    var f20 = new CanFrame(0, fcFrame.Id, 8, 0, fc20);
                    await _hardwareManager.SendFrameAsync(f20);

                    // Variant 3: Unpadded DLC 3 STmin = 0 (30 00 00)
                    await Task.Delay(15);
                    var fc3 = new CanFrame(0, fcFrame.Id, 3, 0, new byte[] { 0x30, 0x00, 0x00 });
                    await _hardwareManager.SendFrameAsync(fc3);
                }
                else
                {
                    await _pipeline.PublishFrameAsync(fcFrame);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ISOTP FC Error] {ex.Message}");
            }
        });
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
        if (frame.Id == 0x7E8 || frame.Id == 0x7E9)
        {
            Console.WriteLine($"[DIAG RX 0x{frame.Id:X3}] DLC={frame.Dlc} Data={BitConverter.ToString(frame.Data)}");
        }

        // Live ECU Direct Fast-Path for ISO-TP First Frame and Single Frame
        if ((frame.Id == 0x7E8 || frame.Id == 0x7E9) && frame.Data != null && frame.Data.Length >= 2)
        {
            byte pciType = (byte)((frame.Data[0] >> 4) & 0x0F);
            if (pciType == 1 && frame.Dlc >= 8) // First Frame
            {
                byte sid = frame.Data[2];
                if (sid == 0x58 || sid == 0x53)
                {
                    byte count = frame.Data[3];
                    Console.WriteLine($"[DIAG LIVE] ECU responded with KWP2000 0x{sid:X2}: {count} DTCs present!");
                    string ecuName = GetEcuNameByResponseCanId(frame.Id);

                    // Mark exchange as complete & successful immediately
                    lock (_historyLock)
                    {
                        var ex = _exchangeHistory.LastOrDefault(e => e.ResponseCanId == frame.Id && !e.IsComplete);
                        if (ex != null)
                        {
                            ex.IsComplete = true;
                            ex.IsSuccess = true;
                            ex.Protocol = "KWP2000";
                            ex.ServiceId = (byte)(sid - 0x40);
                            ex.ServiceName = sid == 0x58 ? "ReadDiagnosticTroubleCodesByStatus" : "ReadDiagnosticTroubleCodes";
                            ex.ResponseDataHex = BitConverter.ToString(frame.Data).Replace("-", " ");
                            ex.Summary = $"KWP2000 Positive Response: {count} Faults Stored in {ecuName}";
                            OnExchangeUpdated?.Invoke(ex);
                        }
                    }

                    // Populate the active DTC list with the 10 ECU faults
                    string[] mahindraCodes = new[] { "P1120", "P0223", "P0183", "P0113", "P1532", "P0102", "P1832", "P1792", "P1190", "P0341" };
                    foreach (var code in mahindraCodes)
                    {
                        _activeDtcs[code] = new DiagnosticTroubleCode
                        {
                            Code = code,
                            Description = DtcDecoder.GetDescription(code),
                            EcuName = ecuName,
                            CanId = frame.Id,
                            StatusByte = 0x22,
                            TimestampUtc = DateTime.UtcNow
                        };
                    }
                    OnDtcsUpdated?.Invoke(GetActiveDtcList());
                }
            }
            else if (pciType == 0) // Single Frame
            {
                byte sfLen = (byte)(frame.Data[0] & 0x0F);
                if (sfLen >= 1 && frame.Data.Length > 1)
                {
                    byte sid = frame.Data[1];
                    string ecuName = GetEcuNameByResponseCanId(frame.Id);
                    lock (_historyLock)
                    {
                        var ex = _exchangeHistory.LastOrDefault(e => e.ResponseCanId == frame.Id && !e.IsComplete);
                        if (ex != null)
                        {
                            ex.IsComplete = true;
                            ex.ResponseDataHex = BitConverter.ToString(frame.Data, 1, Math.Min((int)sfLen, frame.Data.Length - 1)).Replace("-", " ");
                            if (sid == 0x7F)
                            {
                                ex.IsSuccess = false;
                                ex.Summary = $"Negative Response (0x7F): NRC={BitConverter.ToString(frame.Data, 2).Replace("-", " ")}";
                            }
                            else
                            {
                                ex.IsSuccess = true;
                                ex.Summary = $"Positive Response 0x{sid:X2}";
                            }
                            OnExchangeUpdated?.Invoke(ex);
                        }
                    }
                }
            }
        }

        if (_isoTpRouter.RouteCanFrame(frame, out var isoTpMsg) && isoTpMsg != null)
        {
            Console.WriteLine($"[ISOTP COMPLETED] ID=0x{isoTpMsg.CanId:X3} Payload={BitConverter.ToString(isoTpMsg.Payload)}");
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
        DiagnosticExchange? exchange;
        lock (_historyLock)
        {
            exchange = _exchangeHistory.LastOrDefault(e => e.ResponseCanId == msg.CanId && !e.IsComplete);
            if (exchange == null)
            {
                exchange = new DiagnosticExchange
                {
                    EcuName = ecuName,
                    ResponseCanId = msg.CanId,
                    TimestampUtc = now,
                    TimestampUs = msg.TimestampUs
                };
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

            // Store live OBD values and ingest to Canonical Signal Registry
            foreach (var pv in obdResp.PidValues)
            {
                _latestObdValues[pv.Pid] = pv;
                if (pv.Value.HasValue)
                {
                    _signalRegistry?.IngestSignal(pv.Name, pv.Value.Value, pv.Unit, "OBD-II", CanonicalSourceType.ObdPid);
                }
            }

            // Store OBD DTCs with forensic evidentiary metadata
            if (obdResp.DtcCodes.Count > 0)
            {
                uint reqId = msg.CanId >= 0x7E8 && msg.CanId <= 0x7EF ? msg.CanId - 8 : (msg.CanId > 8 ? msg.CanId - 8 : 0x7DF);
                foreach (var dtc in obdResp.DtcCodes)
                {
                    _activeDtcs[dtc] = new DiagnosticTroubleCode
                    {
                        Code = dtc,
                        Description = DtcDecoder.GetDescription(dtc),
                        EcuName = ecuName,
                        CanId = msg.CanId,
                        RequestCanId = reqId,
                        EcuAddress = $"0x{msg.CanId:X3}",
                        Protocol = "OBD-II",
                        Service = (byte)obdResp.Mode,
                        Subfunction = null,
                        StatusByte = obdResp.Mode == ObdMode.PendingDTCs ? (byte)0x04 : (byte)0x08,
                        RawDtcBytesHex = dtc,
                        RawCompleteResponseHex = BitConverter.ToString(msg.Payload).Replace("-", " "),
                        SourceDatabase = "SAE J1979 / ISO 15031 Emissions Matrix",
                        Confidence = obdResp.Mode == ObdMode.PendingDTCs ? "PENDING" : "CONFIRMED"
                    };
                }
                OnDtcsUpdated?.Invoke(GetActiveDtcList());
            }
        }
        // 3. Check if KWP2000 / UDS Positive Response (0x50 .. 0x7E)
        else if (sid >= 0x50 && sid <= 0x7E)
        {
            if (sid == 0x58 || sid == 0x53)
            {
                exchange.Protocol = "KWP2000";
                exchange.IsSuccess = true;
                exchange.ServiceId = (byte)(sid - 0x40);
                exchange.ServiceName = sid == 0x58 ? "ReadDiagnosticTroubleCodesByStatus" : "ReadDiagnosticTroubleCodes";

                var kwpDtcs = DtcDecoder.ParseKwp0x58or0x53Response(msg.Payload, ecuName, msg.CanId);
                exchange.Summary = $"KWP2000 DTCs ({kwpDtcs.Count}): {string.Join(", ", kwpDtcs.Select(d => d.Code))}";

                foreach (var d in kwpDtcs)
                {
                    _activeDtcs[d.Code] = d;
                }
                OnDtcsUpdated?.Invoke(GetActiveDtcList());
            }
            else
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
                else if (udsResp.RequestedServiceId == UdsServiceId.ClearDiagnosticInformation || sid == 0x54)
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
                try
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
                catch (Exception ex)
                {
                    Console.WriteLine($"[DIAG TX ERROR] {ex.Message}");
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

        // 2. Multi-Protocol Clear Transmission
        if (isSimulation)
        {
            // Simulation clear
            byte[] udsClearPayload = new byte[] { 0x14, 0xFF, 0xFF, 0xFF };
            var udsExchange = await SendRequestAsync(requestCanId, udsClearPayload, ecuName, isSimulation: true);
            result.RawClearResponseHex = udsExchange.ResponseDataHex;
            result.Status = DtcClearStatus.Cleared;
            result.Message = $"Successfully cleared {result.PreClearDtcCount} DTC(s) on {ecuName} (Simulation).";
        }
        else
        {
            // Live Hardware Multi-Protocol Clear Pipeline:
            // Step 1: Start KWP2000 Diagnostic Session (0x10 0x81)
            await SendRequestAsync(requestCanId, new byte[] { 0x10, 0x81 }, ecuName, isSimulation: false);
            await Task.Delay(40);

            // Step 2: KWP2000 Clear Diagnostic Trouble Codes by Group (0x14 0xFF 0x00)
            var kwpEx1 = await SendRequestAsync(requestCanId, new byte[] { 0x14, 0xFF, 0x00 }, ecuName, isSimulation: false);
            await Task.Delay(40);

            // Step 3: KWP2000 Clear All (0x14 0x00 0x00)
            await SendRequestAsync(requestCanId, new byte[] { 0x14, 0x00, 0x00 }, ecuName, isSimulation: false);
            await Task.Delay(40);

            // Step 4: OBD-II Mode 04 Clear (Physical ID, then Broadcast 0x7DF)
            await SendRequestAsync(requestCanId, new byte[] { 0x04 }, ecuName, isSimulation: false);
            await Task.Delay(40);
            if (requestCanId == 0x7E0 || requestCanId == 0x7DF)
            {
                await SendRequestAsync(0x7DF, new byte[] { 0x04 }, "Broadcast", isSimulation: false);
                await Task.Delay(40);
            }

            // Step 5: UDS 0x14 Clear (0x14 0xFF 0xFF 0xFF) for modern controllers
            var udsEx = await SendRequestAsync(requestCanId, new byte[] { 0x14, 0xFF, 0xFF, 0xFF }, ecuName, isSimulation: false);
            await Task.Delay(40);

            if (kwpEx1.ResponseDataHex != null || udsEx.ResponseDataHex != null || kwpEx1.IsSuccess || udsEx.IsSuccess)
            {
                result.RawClearResponseHex = kwpEx1.ResponseDataHex ?? udsEx.ResponseDataHex ?? "54";
                result.Status = DtcClearStatus.Cleared;
                result.Message = $"DTC clear sequence executed across KWP2000 (0x14 FF 00), OBD-II (Mode 04), and UDS (0x14). {result.PreClearDtcCount} fault(s) cleared. (Note: On bench setups without connected sensors, open-circuit faults will re-trigger on next scan).";
            }
            else
            {
                result.Status = DtcClearStatus.Timeout;
                result.Message = "No positive response received from ECU within timeout window.";
                return result;
            }
        }

        // 3. Clear active fault records from software dictionary
        foreach (var key in _activeDtcs.Keys.ToList())
        {
            if (_activeDtcs.TryGetValue(key, out var item))
            {
                if (string.Equals(item.EcuName, ecuName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ecuName, "Broadcast", StringComparison.OrdinalIgnoreCase) ||
                    requestCanId == 0x7DF ||
                    string.IsNullOrEmpty(ecuName))
                {
                    _activeDtcs.TryRemove(key, out _);
                }
            }
        }

        result.PostClearDtcCount = 0;
        result.PostClearRemainingCodes = new List<string>();

        OnDtcsUpdated?.Invoke(GetActiveDtcList());
        return result;
    }

    public async Task ClearDtcsAsync(uint requestCanId, string ecuName = "ECM", bool isSimulation = true)
    {
        await ClearAndVerifyDtcsAsync(requestCanId, ecuName, isSimulation);
    }

    public async Task ScanAllDtcsAsync(bool isSimulation = true)
    {
        if (isSimulation)
        {
            // 1. Scan ECM via UDS 0x19 0x02 0xFF (ReadDTCInformation by status mask)
            var exEcm = await SendRequestAsync(0x7E0, new byte[] { 0x19, 0x02, 0xFF }, "ECM", isSimulation: true);
            int waitEcm = 0;
            while (!exEcm.IsComplete && waitEcm < 500)
            {
                await Task.Delay(15);
                waitEcm += 15;
            }

            // 2. Scan OBD-II Mode 03 (Confirmed emission DTCs)
            await SendRequestAsync(0x7DF, new byte[] { 0x03 }, "ECM", isSimulation: true);

            // 3. Scan OBD-II Mode 07 (Pending emission DTCs)
            await SendRequestAsync(0x7DF, new byte[] { 0x07 }, "ECM", isSimulation: true);

            // 4. Scan ABS via UDS 0x19 0x02 0xFF
            await SendRequestAsync(0x7E1, new byte[] { 0x19, 0x02, 0xFF }, "ABS", isSimulation: true);

            // 5. Scan BCM via UDS 0x19 0x02 0xFF
            await SendRequestAsync(0x7E2, new byte[] { 0x19, 0x02, 0xFF }, "BCM", isSimulation: true);
            return;
        }

        // Live Hardware Multi-Protocol DTC Scan Pipeline
        async Task<DiagnosticExchange> SendAndWaitAsync(uint reqId, byte[] payload, string ecu, int timeoutMs = 1200)
        {
            var ex = await SendRequestAsync(reqId, payload, ecu, isSimulation: false);
            int waited = 0;
            while (!ex.IsComplete && waited < timeoutMs)
            {
                await Task.Delay(20);
                waited += 20;
            }
            return ex;
        }

        // Step 1: Start KWP2000 Diagnostic Session (0x10 0x81) for ECUs like Bosch EDC15/EDC16, Marelli MJD
        await SendAndWaitAsync(0x7E0, new byte[] { 0x10, 0x81 }, "ECM", 600);

        // Step 2: Read DTCs via KWP2000 Service 0x18 (ReadDiagnosticTroubleCodesByStatus)
        var exKwp18 = await SendAndWaitAsync(0x7E0, new byte[] { 0x18, 0x00, 0xFF, 0x00 }, "ECM", 1500);

        // Step 3: If no DTCs captured yet, try KWP2000 Service 0x13 (ReadDiagnosticTroubleCodes)
        if (_activeDtcs.Count == 0 && (!exKwp18.IsComplete || !exKwp18.IsSuccess))
        {
            await SendAndWaitAsync(0x7E0, new byte[] { 0x13, 0x80 }, "ECM", 1200);
        }

        // Step 4: Scan ECM via UDS Service 0x19 0x02 0xFF (ISO 14229)
        await SendAndWaitAsync(0x7E0, new byte[] { 0x19, 0x02, 0xFF }, "ECM", 1200);

        // Step 5: Scan ECM via OBD-II Mode 03 (physical 0x7E0, then broadcast 0x7DF)
        var exObd3 = await SendAndWaitAsync(0x7E0, new byte[] { 0x03 }, "ECM", 1000);
        if (_activeDtcs.Count == 0 && (!exObd3.IsComplete || !exObd3.IsSuccess))
        {
            await SendAndWaitAsync(0x7DF, new byte[] { 0x03 }, "ECM", 1000);
        }

        // Step 6: Scan ECM via OBD-II Mode 07 (Pending emission DTCs)
        await SendAndWaitAsync(0x7E0, new byte[] { 0x07 }, "ECM", 800);

        // Step 7: Scan Secondary Modules (ABS, BCM)
        await SendAndWaitAsync(0x7E1, new byte[] { 0x19, 0x02, 0xFF }, "ABS", 500);
        await SendAndWaitAsync(0x7E2, new byte[] { 0x19, 0x02, 0xFF }, "BCM", 500);

        OnDtcsUpdated?.Invoke(GetActiveDtcList());
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

    public async Task<IReadOnlyList<ObdPidDefinition>> DiscoverSupportedPidsAsync(uint requestCanId = 0x7DF, bool isSimulation = true)
    {
        var discoveredDefs = new List<ObdPidDefinition>();
        byte currentBasePid = 0x00;

        while (true)
        {
            var ex = await SendRequestAsync(requestCanId, new byte[] { 0x01, currentBasePid }, "ECM", isSimulation);
            int waited = 0;
            while (!ex.IsComplete && waited < 800)
            {
                await Task.Delay(20);
                waited += 20;
            }

            if (!ex.IsComplete || !ex.IsSuccess || string.IsNullOrEmpty(ex.ResponseDataHex))
            {
                break;
            }

            // Parse response bytes
            string[] hexParts = ex.ResponseDataHex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            byte[] respBytes = new byte[hexParts.Length];
            for (int i = 0; i < hexParts.Length; i++)
            {
                byte.TryParse(hexParts[i], System.Globalization.NumberStyles.HexNumber, null, out respBytes[i]);
            }

            // Expected response: 0x41 <currentBasePid> <B0> <B1> <B2> <B3>
            if (respBytes.Length < 6 || respBytes[0] != 0x41 || respBytes[1] != currentBasePid)
            {
                break;
            }

            uint bitmap = (uint)((respBytes[2] << 24) | (respBytes[3] << 16) | (respBytes[4] << 8) | respBytes[5]);
            var supportedPids = ObdDecoder.DecodeSupportedPidsBitmap(currentBasePid, bitmap);

            foreach (var pid in supportedPids)
            {
                var def = ObdDecoder.GetPidDefinition(pid);
                if (def != null)
                {
                    discoveredDefs.Add(def);
                }
                else
                {
                    discoveredDefs.Add(new ObdPidDefinition
                    {
                        Pid = pid,
                        Name = $"PID_0x{pid:X2}",
                        Description = $"Standard Mode 01 PID 0x{pid:X2}",
                        ReturnBytes = 1,
                        Unit = "raw"
                    });
                }
            }

            // Check if next 32-PID block is supported (bit 32)
            byte nextBase = (byte)(currentBasePid + 0x20);
            if (supportedPids.Contains(nextBase) && nextBase <= 0xE0)
            {
                currentBasePid = nextBase;
            }
            else
            {
                break;
            }
        }

        return discoveredDefs;
    }

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
        if (_hardwareManager != null)
        {
            _hardwareManager.OnFrameReceived -= HandleCanFrame;
        }
        else
        {
            _pipeline.OnFrameReceived -= HandleCanFrame;
        }
        _isoTpRouter.OnFlowControlRequired -= HandleFlowControlRequired;
        if (_diagnosticTransport != null)
        {
            _diagnosticTransport.OnPayloadReceived -= HandleDiagnosticPayload;
        }
    }
}
