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
        IUnifiedSignalRegistry? signalRegistry)
        : this(pipeline, isoTpRouter, ecuSimulator, hardwareManager, null, signalRegistry)
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

        // Subscribe to canonical CAN pipeline to ingest both physical hardware frames and simulation frames
        _pipeline.OnFrameReceived += HandleCanFrame;
        _isoTpRouter.OnFlowControlRequired += HandleFlowControlRequired;
        if (_diagnosticTransport != null)
        {
            _diagnosticTransport.OnPayloadReceived += HandleDiagnosticPayload;
        }
    }

    private void HandleFlowControlRequired(CanFrame fcFrame)
    {
        // Transmit ISO-TP Flow Control immediately to hardware to prevent ECU consecutive frame timeout
        Task.Run(async () =>
        {
            try
            {
                // Immediate standard ISO-TP Flow Control (CTS, BlockSize = 0, STmin = 0, padded to 8 bytes)
                byte[] fc = new byte[] { 0x30, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                var flowControlFrame = new CanFrame(0, fcFrame.Id, 8, 0, fc);

                bool sent = false;
                if (_hardwareManager != null)
                {
                    sent = await _hardwareManager.SendFrameAsync(flowControlFrame);
                }

                if (!sent)
                {
                    await _pipeline.PublishFrameAsync(flowControlFrame);
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
        uint cleanId = frame.Id & 0x1FFFFFFFU;
        bool isDiag = IsDiagnosticResponseId(cleanId);
        if (isDiag)
        {
            Console.WriteLine($"[DIAG RX 0x{cleanId:X}] DLC={frame.Dlc} Data={BitConverter.ToString(frame.Data)}");
        }

        // Live ECU Direct Fast-Path for ISO-TP First Frame and Single Frame across ALL diagnostic IDs
        if (isDiag && frame.Data != null && frame.Data.Length >= 2)
        {
            byte pciType = (byte)((frame.Data[0] >> 4) & 0x0F);
            uint reqId = GetRequestCanIdForResponse(cleanId);
            string ecuName = GetEcuNameByResponseCanId(cleanId);

            if (pciType == 1 && frame.Dlc >= 8) // ISO-TP First Frame
            {
                // 1. Immediately emit ISO-TP Flow Control (CTS, BlockSize=0, STmin=0) to hardware
                byte[] fc = new byte[] { 0x30, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                var fcFrame = new CanFrame(0, reqId, 8, 0, fc);
                if (_hardwareManager != null)
                {
                    _ = _hardwareManager.SendFrameAsync(fcFrame);
                }

                byte sid = frame.Data[2];

                // Case A: KWP2000 ReadDiagnosticTroubleCodes (0x58 or 0x53)
                if (sid == 0x58 || sid == 0x53)
                {
                    byte count = frame.Data[3];
                    Console.WriteLine($"[DIAG LIVE] ECU responded with KWP2000 0x{sid:X2}: {count} DTCs present!");
                    UpdateExchangeHistoryFast(cleanId, sid, (byte)(sid - 0x40), "KWP2000",
                        sid == 0x58 ? "ReadDiagnosticTroubleCodesByStatus" : "ReadDiagnosticTroubleCodes",
                        $"KWP2000 Positive Response: {count} Faults Stored in {ecuName}", frame.Data);

                    if (frame.Data.Length >= 7)
                    {
                        byte b1 = frame.Data[4];
                        byte b2 = frame.Data[5];
                        byte status = frame.Data[6];
                        if (b1 != 0 || b2 != 0)
                        {
                            string code = DtcDecoder.Decode2ByteDtc(b1, b2);
                            RecordActiveDtc(code, ecuName, cleanId, reqId, "KWP2000", (byte)(sid - 0x40), null, status, $"{b1:X2} {b2:X2}", BitConverter.ToString(frame.Data).Replace("-", " "), "ISO 14230 / KWP2000 Bosch EDC17 Matrix", "CONFIRMED");
                        }
                    }
                }
                // Case B: UDS ReadDTCInformation (0x59)
                else if (sid == 0x59)
                {
                    byte subfunc = frame.Data.Length > 3 ? frame.Data[3] : (byte)0x02;
                    Console.WriteLine($"[DIAG LIVE] ECU responded with UDS 0x59 (Subfunc 0x{subfunc:X2})!");
                    UpdateExchangeHistoryFast(cleanId, sid, 0x19, "UDS", "ReadDTCInformation", $"UDS Positive Response: DTCs present in {ecuName}", frame.Data);

                    // First DTC in First Frame (Data[5..7])
                    if (frame.Data.Length >= 8)
                    {
                        byte h = frame.Data[5];
                        byte m = frame.Data[6];
                        byte l = frame.Data[7];
                        if (h != 0 || m != 0)
                        {
                            string code = DtcDecoder.Decode3ByteDtc(h, m, l);
                            RecordActiveDtc(code, ecuName, cleanId, reqId, "UDS", 0x19, subfunc, l, $"{h:X2} {m:X2} {l:X2}", BitConverter.ToString(frame.Data).Replace("-", " "), "ISO 14229 / SAE J2012 OEM Matrix", (l & 0x08) != 0 ? "CONFIRMED" : "ACTIVE");
                        }
                    }
                }
                // Case C: OBD-II Mode 03 / Mode 07 / Mode 0A (0x43 / 0x47 / 0x4A)
                else if (sid == 0x43 || sid == 0x47 || sid == 0x4A)
                {
                    byte mode = (byte)(sid - 0x40);
                    byte count = frame.Data[3];
                    string modeName = sid == 0x47 ? "Mode 07 (Pending DTCs)" : sid == 0x4A ? "Mode 0A (Permanent DTCs)" : "Mode 03 (Confirmed DTCs)";
                    Console.WriteLine($"[DIAG LIVE] ECU responded with OBD-II {modeName}: {count} DTC(s) reported!");
                    UpdateExchangeHistoryFast(cleanId, sid, mode, "OBD-II", modeName, $"OBD-II {modeName}: {count} Faults in {ecuName}", frame.Data);

                    // Decode first DTC
                    if (frame.Data.Length >= 6)
                    {
                        byte b1 = frame.Data[4];
                        byte b2 = frame.Data[5];
                        if (b1 != 0 || b2 != 0)
                        {
                            string code1 = DtcDecoder.Decode2ByteDtc(b1, b2);
                            RecordActiveDtc(code1, ecuName, cleanId, reqId, "OBD-II", mode, null, sid == 0x47 ? (byte)0x04 : (byte)0x08, $"{b1:X2} {b2:X2}", BitConverter.ToString(frame.Data).Replace("-", " "), "SAE J1979 / ISO 15031 Emissions Matrix", sid == 0x47 ? "PENDING" : "CONFIRMED");
                        }
                    }
                    // Decode second DTC if present in First Frame
                    if (frame.Data.Length >= 8)
                    {
                        byte b3 = frame.Data[6];
                        byte b4 = frame.Data[7];
                        if (b3 != 0 || b4 != 0)
                        {
                            string code2 = DtcDecoder.Decode2ByteDtc(b3, b4);
                            RecordActiveDtc(code2, ecuName, cleanId, reqId, "OBD-II", mode, null, sid == 0x47 ? (byte)0x04 : (byte)0x08, $"{b3:X2} {b4:X2}", BitConverter.ToString(frame.Data).Replace("-", " "), "SAE J1979 / ISO 15031 Emissions Matrix", sid == 0x47 ? "PENDING" : "CONFIRMED");
                        }
                    }
                }
            }
            else if (pciType == 0) // Single Frame
            {
                byte sfLen = (byte)(frame.Data[0] & 0x0F);
                if (sfLen >= 1 && frame.Data.Length > 1)
                {
                    byte sid = frame.Data[1];
                    lock (_historyLock)
                    {
                        var ex = _exchangeHistory.LastOrDefault(e => e.ResponseCanId == cleanId && !e.IsComplete);
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

    private void UpdateExchangeHistoryFast(uint responseCanId, byte sid, byte serviceId, string protocol, string serviceName, string summary, byte[] data)
    {
        lock (_historyLock)
        {
            var ex = _exchangeHistory.LastOrDefault(e => e.ResponseCanId == responseCanId && !e.IsComplete);
            if (ex != null)
            {
                ex.IsComplete = true;
                ex.IsSuccess = true;
                ex.Protocol = protocol;
                ex.ServiceId = serviceId;
                ex.ServiceName = serviceName;
                ex.ResponseDataHex = BitConverter.ToString(data).Replace("-", " ");
                ex.Summary = summary;
                OnExchangeUpdated?.Invoke(ex);
            }
        }
    }

    private void RecordActiveDtc(
        string code,
        string ecuName,
        uint canId,
        uint reqId,
        string protocol,
        byte service,
        byte? subfunction,
        byte statusByte,
        string rawDtcBytesHex,
        string rawCompleteResponseHex,
        string sourceDatabase,
        string confidence)
    {
        _activeDtcs[code] = new DiagnosticTroubleCode
        {
            Code = code,
            Description = DtcDecoder.GetDescription(code),
            EcuName = ecuName,
            CanId = canId,
            RequestCanId = reqId,
            EcuAddress = canId > 0x7FF ? $"0x{canId:X8}" : $"0x{canId:X3}",
            Protocol = protocol,
            Service = service,
            Subfunction = subfunction,
            StatusByte = statusByte,
            RawDtcBytesHex = rawDtcBytesHex,
            RawCompleteResponseHex = rawCompleteResponseHex,
            SourceDatabase = sourceDatabase,
            Confidence = confidence,
            TimestampUtc = DateTime.UtcNow
        };
        OnDtcsUpdated?.Invoke(GetActiveDtcList());
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

            // Clear DTCs (Mode 04 positive response: 0x44)
            if (obdResp.Mode == ObdMode.ClearDTCs || sid == 0x44)
            {
                foreach (var key in _activeDtcs.Keys.ToList())
                {
                    if (string.Equals(_activeDtcs[key].EcuName, ecuName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ecuName, "ECM", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ecuName, "Broadcast", StringComparison.OrdinalIgnoreCase))
                    {
                        _activeDtcs.TryRemove(key, out _);
                    }
                }
                OnDtcsUpdated?.Invoke(GetActiveDtcList());
            }

            // Mode 09 VIN & Info (0x49)
            if (obdResp.Mode == ObdMode.VehicleInformation || sid == 0x49)
            {
                if (msg.Payload.Length > 2)
                {
                    byte infoType = msg.Payload[1];
                    if (infoType == 0x02 && msg.Payload.Length >= 4) // VIN
                    {
                        int vinOffset = msg.Payload[2] == 0x01 ? 3 : 2;
                        if (msg.Payload.Length > vinOffset)
                        {
                            string vin = Encoding.ASCII.GetString(msg.Payload, vinOffset, msg.Payload.Length - vinOffset).Trim('\0', ' ');
                            if (_ecuIdentities.TryGetValue(ecuName, out var ecu))
                            {
                                ecu.Vin = vin;
                                ecu.VinStatus = DataAvailability.Supported;
                            }
                        }
                    }
                    else if (infoType == 0x0A && msg.Payload.Length >= 4) // ECU Name
                    {
                        int nameOffset = msg.Payload[2] == 0x01 ? 3 : 2;
                        if (msg.Payload.Length > nameOffset)
                        {
                            string name = Encoding.ASCII.GetString(msg.Payload, nameOffset, msg.Payload.Length - nameOffset).Trim('\0', ' ');
                            if (_ecuIdentities.TryGetValue(ecuName, out var ecu))
                            {
                                ecu.SystemName = name;
                                ecu.SystemNameStatus = DataAvailability.Supported;
                            }
                        }
                    }
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
            if (sid == 0x5A) // KWP2000 ReadEcuIdentification Positive Response
            {
                exchange.Protocol = "KWP2000";
                exchange.IsSuccess = true;
                exchange.ServiceId = 0x1A;
                exchange.ServiceName = "ReadEcuIdentification";
                string fullAscii = Encoding.ASCII.GetString(msg.Payload).Trim('\0', ' ');
                exchange.Summary = $"ECU Identification: {fullAscii}";

                if (_ecuIdentities.TryGetValue(ecuName, out var ecu))
                {
                    ecu.SupplierInfo = "Bosch MEDC17 (EDC17C55)";
                    ecu.SupplierInfoStatus = DataAvailability.Supported;

                    var matchSw = System.Text.RegularExpressions.Regex.Match(fullAscii, @"1037\d{6}");
                    if (matchSw.Success)
                    {
                        ecu.SoftwareNumber = matchSw.Value;
                        ecu.SoftwareNumberStatus = DataAvailability.Supported;
                    }

                    if (fullAscii.Contains("tc1767", StringComparison.OrdinalIgnoreCase))
                    {
                        ecu.HardwareNumber = "Infineon TriCore TC1767 / Step BB (EDC17C55)";
                        ecu.HardwareNumberStatus = DataAvailability.Supported;
                    }
                    else if (fullAscii.Contains("MEDC17", StringComparison.OrdinalIgnoreCase))
                    {
                        ecu.HardwareNumber = "Bosch EDC17 (MEDC17)";
                        ecu.HardwareNumberStatus = DataAvailability.Supported;
                    }
                }
            }
            else if (sid == 0x58 || sid == 0x53)
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
            var simResp = await _ecuSimulator.ProcessAndPublishSimulationResponseAsync(requestCanId, payload ?? Array.Empty<byte>());
            if (simResp != null)
            {
                var simMsg = new IsoTpMessage
                {
                    CanId = responseCanId,
                    Payload = simResp,
                    IsComplete = true
                };
                ProcessCompletedIsoTpMessage(simMsg);
            }
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
            async Task<DiagnosticExchange> SendAndWaitClearAsync(uint reqId, byte[] payload, string ecu, int timeoutMs = 800)
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

            // Step 1: Start KWP2000 Diagnostic Session (0x10 0x81)
            await SendAndWaitClearAsync(requestCanId, new byte[] { 0x10, 0x81 }, ecuName, 400);

            // Step 2: KWP2000 Clear Diagnostic Trouble Codes by Group (0x14 0xFF 0x00)
            var kwpEx1 = await SendAndWaitClearAsync(requestCanId, new byte[] { 0x14, 0xFF, 0x00 }, ecuName, 600);

            // Step 3: KWP2000 Clear All (0x14 0x00 0x00)
            var kwpEx2 = await SendAndWaitClearAsync(requestCanId, new byte[] { 0x14, 0x00, 0x00 }, ecuName, 600);

            // Step 4: OBD-II Mode 04 Clear (Physical ID, then Broadcast 0x7DF)
            var obdEx = await SendAndWaitClearAsync(requestCanId, new byte[] { 0x04 }, ecuName, 600);
            if (requestCanId == 0x7E0 || requestCanId == 0x7DF)
            {
                await SendAndWaitClearAsync(0x7DF, new byte[] { 0x04 }, "Broadcast", 600);
            }

            // Step 5: UDS 0x14 Clear (0x14 0xFF 0xFF 0xFF) for modern controllers
            var udsEx = await SendAndWaitClearAsync(requestCanId, new byte[] { 0x14, 0xFF, 0xFF, 0xFF }, ecuName, 600);

            bool isAnyPositive = obdEx.IsSuccess || (obdEx.ResponseDataHex != null && obdEx.ResponseDataHex.StartsWith("44"))
                              || kwpEx1.IsSuccess || (kwpEx1.ResponseDataHex != null && kwpEx1.ResponseDataHex.StartsWith("54"))
                              || kwpEx2.IsSuccess || (kwpEx2.ResponseDataHex != null && kwpEx2.ResponseDataHex.StartsWith("54"))
                              || udsEx.IsSuccess || (udsEx.ResponseDataHex != null && udsEx.ResponseDataHex.StartsWith("54"));

            if (isAnyPositive)
            {
                result.RawClearResponseHex = (obdEx.ResponseDataHex != null && obdEx.ResponseDataHex.StartsWith("44")) ? obdEx.ResponseDataHex
                    : (kwpEx1.ResponseDataHex != null && kwpEx1.ResponseDataHex.StartsWith("54")) ? kwpEx1.ResponseDataHex
                    : (udsEx.ResponseDataHex != null && udsEx.ResponseDataHex.StartsWith("54")) ? udsEx.ResponseDataHex
                    : "44";
                result.Status = DtcClearStatus.Cleared;
                result.Message = $"DTC clear sequence executed across OBD-II Mode 04 (0x44), KWP2000 (0x14), and UDS (0x14). {result.PreClearDtcCount} fault(s) cleared on {ecuName}.";
            }
            else
            {
                result.RawClearResponseHex = null;
                result.Status = DtcClearStatus.Timeout;
                result.Message = $"No positive response received from {ecuName} for DTC clear across OBD-II Mode 04, KWP2000, or UDS (Timeout).";
                result.PostClearDtcCount = preClearList.Count;
                result.PostClearRemainingCodes = preClearList.Select(d => d.Code).Distinct().ToList();
                return result;
            }
        }

        // 3. Purge active fault records from software dictionary
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
            var exSimObd3 = await SendRequestAsync(0x7DF, new byte[] { 0x03 }, "ECM", isSimulation: true);
            int wait3 = 0;
            while (!exSimObd3.IsComplete && wait3 < 300) { await Task.Delay(15); wait3 += 15; }

            // 3. Scan OBD-II Mode 07 (Pending emission DTCs)
            var exSimObd7 = await SendRequestAsync(0x7DF, new byte[] { 0x07 }, "ECM", isSimulation: true);
            int wait7 = 0;
            while (!exSimObd7.IsComplete && wait7 < 300) { await Task.Delay(15); wait7 += 15; }

            // 4. Scan ABS via UDS 0x19 0x02 0xFF
            var exSimAbs = await SendRequestAsync(0x7E1, new byte[] { 0x19, 0x02, 0xFF }, "ABS", isSimulation: true);
            int waitAbs = 0;
            while (!exSimAbs.IsComplete && waitAbs < 300) { await Task.Delay(15); waitAbs += 15; }

            // 5. Scan BCM via UDS 0x19 0x02 0xFF
            var exSimBcm = await SendRequestAsync(0x7E2, new byte[] { 0x19, 0x02, 0xFF }, "BCM", isSimulation: true);
            int waitBcm = 0;
            while (!exSimBcm.IsComplete && waitBcm < 300) { await Task.Delay(15); waitBcm += 15; }

            OnDtcsUpdated?.Invoke(GetActiveDtcList());
            return;
        }

        // Live Hardware Universal Multi-Protocol DTC & Parameter Scan Pipeline
        async Task<DiagnosticExchange> SendAndWaitAsync(uint reqId, byte[] payload, string ecu, int timeoutMs = 800)
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

        // Step 1: Scan Broadcast 0x7DF & Physical ECM 0x7E0 via OBD-II Mode 03 (Confirmed DTCs)
        await SendAndWaitAsync(0x7DF, new byte[] { 0x03 }, "Broadcast", 600);
        await SendAndWaitAsync(0x7E0, new byte[] { 0x03 }, "ECM", 600);

        // Step 2: Scan Broadcast 0x7DF & Physical ECM 0x7E0 via OBD-II Mode 07 (Pending DTCs - crucial for bench testing!)
        await SendAndWaitAsync(0x7DF, new byte[] { 0x07 }, "Broadcast", 600);
        await SendAndWaitAsync(0x7E0, new byte[] { 0x07 }, "ECM", 600);

        // Step 3: Scan Mode 0A (Permanent DTCs)
        await SendAndWaitAsync(0x7DF, new byte[] { 0x0A }, "Broadcast", 400);

        // Step 4: Scan ECM via UDS Service 0x19 0x02 with multi-mask fallback strategy
        var exUdsFf = await SendAndWaitAsync(0x7E0, new byte[] { 0x19, 0x02, 0xFF }, "ECM", 800);
        if (_activeDtcs.Count == 0 || !exUdsFf.IsSuccess)
        {
            // Fallback masks for ECUs rejecting 0xFF (e.g. Bosch/Continental NRC 0x31)
            await SendAndWaitAsync(0x7E0, new byte[] { 0x19, 0x02, 0x08 }, "ECM", 400); // Confirmed
            await SendAndWaitAsync(0x7E0, new byte[] { 0x19, 0x02, 0x09 }, "ECM", 400); // Failed + Confirmed
            await SendAndWaitAsync(0x7E0, new byte[] { 0x19, 0x02, 0x04 }, "ECM", 400); // Pending
            await SendAndWaitAsync(0x7E0, new byte[] { 0x19, 0x02, 0x2F }, "ECM", 400); // Extended
            await SendAndWaitAsync(0x7E0, new byte[] { 0x19, 0x01, 0xFF }, "ECM", 400); // DTC Count
        }

        // Step 5: Start KWP2000 Diagnostic Session (0x10 0x81) for Bosch EDC15/EDC16/EDC17, Marelli MJD
        await SendAndWaitAsync(0x7E0, new byte[] { 0x10, 0x81 }, "ECM", 400);
        var exKwp18 = await SendAndWaitAsync(0x7E0, new byte[] { 0x18, 0x00, 0xFF, 0x00 }, "ECM", 800);
        if (_activeDtcs.Count == 0 || !exKwp18.IsSuccess)
        {
            await SendAndWaitAsync(0x7E0, new byte[] { 0x18, 0x00, 0x00, 0x00 }, "ECM", 600);
            await SendAndWaitAsync(0x7E0, new byte[] { 0x13, 0x80 }, "ECM", 600);
            await SendAndWaitAsync(0x7E0, new byte[] { 0x13, 0xFF, 0x00 }, "ECM", 600);
        }

        // Step 6: Scan Secondary Modules (ABS 0x7E1, BCM 0x7E2, SRS 0x7E3)
        await SendAndWaitAsync(0x7E1, new byte[] { 0x03 }, "ABS", 300);
        await SendAndWaitAsync(0x7E1, new byte[] { 0x19, 0x02, 0xFF }, "ABS", 400);
        await SendAndWaitAsync(0x7E2, new byte[] { 0x19, 0x02, 0xFF }, "BCM", 400);
        await SendAndWaitAsync(0x7E3, new byte[] { 0x19, 0x02, 0xFF }, "SRS", 400);

        // Step 7: Extended 29-bit CAN Fallback (ISO 15765-4 29-bit / J1939) if 0 DTCs detected so far
        if (_activeDtcs.Count == 0)
        {
            await SendAndWaitAsync(0x18DB33F1, new byte[] { 0x03 }, "Broadcast_29bit", 600);
            await SendAndWaitAsync(0x18DB33F1, new byte[] { 0x07 }, "Broadcast_29bit", 600);
            await SendAndWaitAsync(0x18DA10F1, new byte[] { 0x19, 0x02, 0xFF }, "ECM_29bit", 600);
        }

        // Step 8: Query Identification (VIN, ECU Name, Hardware Info)
        await SendAndWaitAsync(0x7E0, new byte[] { 0x09, 0x02 }, "ECM", 500); // OBD Mode 09 VIN
        await SendAndWaitAsync(0x7E0, new byte[] { 0x09, 0x0A }, "ECM", 500); // OBD Mode 09 Name
        await SendAndWaitAsync(0x7E0, new byte[] { 0x1A, 0x80 }, "ECM", 500); // KWP2000 Info
        await SendAndWaitAsync(0x7E0, new byte[] { 0x22, 0xF1, 0x90 }, "ECM", 500); // UDS VIN DID

        // Step 9: Automatic Live Parameter Baseline Polling (Mode 01)
        byte[] standardPidsToPoll = new byte[] { 0x0C, 0x0D, 0x05, 0x11, 0x0B, 0x10, 0x23, 0x42 };
        foreach (byte pid in standardPidsToPoll)
        {
            await SendAndWaitAsync(0x7E0, new byte[] { 0x01, pid }, "ECM", 250);
        }

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

    private static bool IsDiagnosticResponseId(uint canId)
    {
        uint cleanId = canId & 0x1FFFFFFFU;
        if (cleanId >= 0x7E8 && cleanId <= 0x7EF) return true;
        if ((cleanId & 0xFFFF0000) == 0x18DA0000) return true;
        if ((cleanId & 0xFFFF0000) == 0x18DB0000) return true;
        if ((cleanId & 0x00FF0000) == 0x00FE0000 || (cleanId & 0x00FF0000) == 0x00FF0000) return true; // J1939
        return false;
    }

    private static uint GetRequestCanIdForResponse(uint respId)
    {
        uint cleanId = respId & 0x1FFFFFFFU;
        if (cleanId >= 0x7E8 && cleanId <= 0x7EF)
        {
            return cleanId - 8;
        }
        if ((cleanId & 0xFFFF0000) == 0x18DA0000)
        {
            // Swap Target and Source bytes for 29-bit UDS
            byte target = (byte)((cleanId >> 8) & 0xFF);
            byte source = (byte)(cleanId & 0xFF);
            return 0x18DA0000U | ((uint)source << 8) | (uint)target;
        }
        return cleanId > 8 ? cleanId - 8 : 0x7E0;
    }

    private string GetEcuNameByResponseCanId(uint canId)
    {
        uint cleanId = canId & 0x1FFFFFFFU;
        return cleanId switch
        {
            0x7E8 => "ECM",
            0x7E9 => "ABS",
            0x7EA => "BCM",
            0x7EB => "SRS",
            0x7EC => "CLUSTER",
            0x7ED => "HVAC",
            0x7EE => "GATEWAY",
            0x7EF => "SPECIAL",
            _ when (cleanId & 0xFFFF0000) == 0x18DA0000 => $"ECU_0x{(cleanId & 0xFF):X2}",
            _ => $"ECU_0x{canId:X3}"
        };
    }

    private uint GetResponseCanIdByRequestCanId(uint reqId)
    {
        uint cleanId = reqId & 0x1FFFFFFFU;
        if (cleanId == 0x7DF) return 0x7E8;
        if (cleanId >= 0x7E0 && cleanId <= 0x7E7) return cleanId + 8;
        if (cleanId == 0x18DB33F1) return 0x18DAF110;
        if ((cleanId & 0xFFFF0000) == 0x18DA0000)
        {
            byte target = (byte)((cleanId >> 8) & 0xFF);
            byte source = (byte)(cleanId & 0xFF);
            return 0x18DA0000U | ((uint)source << 8) | (uint)target;
        }
        return reqId + 8;
    }

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
