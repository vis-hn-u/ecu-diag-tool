using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Diagnostics.Uds;
using CanEngineeringPlatform.Core.Diagnostics.Obd;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;

namespace CanEngineeringPlatform.Core.Diagnostics.Simulator;

public interface IEcuSimulator
{
    bool IsEnabled { get; set; }
    IReadOnlyList<VirtualEcu> GetVirtualEcus();
    VirtualEcu? GetEcuByRequestCanId(uint canId);
    byte[]? ProcessDiagnosticRequest(uint requestCanId, byte[] requestPayload);
    Task<byte[]?> ProcessAndPublishSimulationResponseAsync(uint requestCanId, byte[] requestPayload, uint timestampUs = 0);
    void SetDtcFault(string ecuName, string dtcCode, bool active);
    void SetSimulatedVoltage(string ecuName, double voltage);
    void SetSimulatedDid(string ecuName, ushort did, byte[] data);
    void SetNrcOverride(uint requestCanId, byte serviceId, byte nrc);
    void ClearNrcOverrides();
    void ResetAll();
}

public class VirtualEcu
{
    public string Name { get; set; } = string.Empty;
    public uint RequestCanId { get; set; }
    public uint ResponseCanId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Vin { get; set; } = "1FA6P8CF5H5123456";
    public string HardwareNumber { get; set; } = string.Empty;
    public string SoftwareVersion { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public byte ActiveSession { get; set; } = 0x01; // 0x01 = Default
    public bool SecurityUnlocked { get; set; } = false;
    public Dictionary<ushort, byte[]> DataIdentifiers { get; set; } = new();
    public List<DiagnosticTroubleCode> TroubleCodes { get; set; } = new();
}

public class EcuSimulator : IEcuSimulator
{
    private readonly ICanPipeline _pipeline;
    private readonly IIsoTpRouter _isoTpRouter;
    private readonly ConcurrentDictionary<uint, VirtualEcu> _ecus = new();
    private readonly ConcurrentDictionary<(uint CanId, byte ServiceId), byte> _nrcOverrides = new();
    public bool IsEnabled { get; set; } = true;

    public EcuSimulator(ICanPipeline pipeline, IIsoTpRouter isoTpRouter)
    {
        _pipeline = pipeline;
        _isoTpRouter = isoTpRouter;

        InitializeDefaultVirtualEcus();
    }

    private void InitializeDefaultVirtualEcus()
    {
        _ecus.Clear();

        // 1. ECM (Engine Control Module): 0x7E0 / 0x7E8
        var ecm = new VirtualEcu
        {
            Name = "ECM",
            RequestCanId = 0x7E0,
            ResponseCanId = 0x7E8,
            Description = "Engine Control Module (Bosch EDC17C55 C04 / C63 Mahindra 1.5 mHawk)",
            Vin = "1FA6P8CF5H5123456",
            HardwareNumber = "0281030085 (EDC17C55 C04 / C63)",
            SoftwareVersion = "1037535157",
            SystemName = "Bosch EDC17C55 (Mahindra 1.5 mHawk C04/C63)"
        };
        ecm.DataIdentifiers[0xF190] = Encoding.ASCII.GetBytes(ecm.Vin);
        ecm.DataIdentifiers[0xF18A] = Encoding.ASCII.GetBytes(ecm.HardwareNumber);
        ecm.DataIdentifiers[0xF191] = Encoding.ASCII.GetBytes(ecm.HardwareNumber);
        ecm.DataIdentifiers[0xF189] = Encoding.ASCII.GetBytes(ecm.SoftwareVersion);
        ecm.DataIdentifiers[0xF194] = Encoding.ASCII.GetBytes(ecm.SoftwareVersion);
        ecm.DataIdentifiers[0xF187] = Encoding.ASCII.GetBytes("0281030085");
        ecm.DataIdentifiers[0xF197] = Encoding.ASCII.GetBytes(ecm.SystemName);
        ecm.DataIdentifiers[0xF186] = new byte[] { 0x01 }; // Default session
        ecm.DataIdentifiers[0x0100] = new byte[] { 0x07, 0xD0 }; // RPM = 2000 (0x07D0)
        ecm.DataIdentifiers[0x0101] = new byte[] { 0x58 }; // Coolant = 88 C
        ecm.DataIdentifiers[0x0102] = new byte[] { 0x35, 0xE8 }; // Battery Voltage = 13.80V (13800 mV)
        ecm.DataIdentifiers[0x0103] = new byte[] { 0x40 }; // Throttle = 25% (64)
        ecm.DataIdentifiers[0x0104] = new byte[] { 0x06, 0x0E }; // MAF = 15.5 g/s (1550)
        ecm.DataIdentifiers[0x0105] = new byte[] { 0x64 }; // MAP = 100 kPa
        ecm.DataIdentifiers[0x0106] = new byte[] { 0x03, 0xE8 }; // Lambda = 1.000 (1000)
        ecm.DataIdentifiers[0x0107] = new byte[] { 0x80 }; // STFT = 0.0% (128)
        ecm.DataIdentifiers[0x0108] = new byte[] { 0x80 }; // LTFT = 0.0% (128)
        ecm.DataIdentifiers[0x0109] = new byte[] { 0x41 }; // Speed = 65 km/h
        ecm.DataIdentifiers[0x010A] = new byte[] { 0x41 }; // IAT = 25 C (65)

        // Bosch EDC17C55 C04 / C63 Bench Reference Fault Codes (P2122, P2127, P0122, P0222, P0103, P0107, P0118, P0113)
        ecm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "P2122",
            RawDtcNumber = 0x212216,
            StatusByte = 0x2F,
            Description = DtcDecoder.GetDescription("P2122"),
            EcuName = "ECM",
            CanId = 0x7E8
        });
        ecm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "P2127",
            RawDtcNumber = 0x212716,
            StatusByte = 0x2F,
            Description = DtcDecoder.GetDescription("P2127"),
            EcuName = "ECM",
            CanId = 0x7E8
        });
        ecm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "P0122",
            RawDtcNumber = 0x012216,
            StatusByte = 0x2F,
            Description = DtcDecoder.GetDescription("P0122"),
            EcuName = "ECM",
            CanId = 0x7E8
        });
        ecm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "P0222",
            RawDtcNumber = 0x022216,
            StatusByte = 0x2F,
            Description = DtcDecoder.GetDescription("P0222"),
            EcuName = "ECM",
            CanId = 0x7E8
        });
        ecm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "P0103",
            RawDtcNumber = 0x010317,
            StatusByte = 0x2F,
            Description = DtcDecoder.GetDescription("P0103"),
            EcuName = "ECM",
            CanId = 0x7E8
        });
        ecm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "P0107",
            RawDtcNumber = 0x010716,
            StatusByte = 0x2F,
            Description = DtcDecoder.GetDescription("P0107"),
            EcuName = "ECM",
            CanId = 0x7E8
        });
        ecm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "P0118",
            RawDtcNumber = 0x011817,
            StatusByte = 0x2F,
            Description = DtcDecoder.GetDescription("P0118"),
            EcuName = "ECM",
            CanId = 0x7E8
        });
        ecm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "P0113",
            RawDtcNumber = 0x011317,
            StatusByte = 0x2F,
            Description = DtcDecoder.GetDescription("P0113"),
            EcuName = "ECM",
            CanId = 0x7E8
        });

        // Baseline DTCs for testing
        ecm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "P0300",
            RawDtcNumber = 0x030000,
            StatusByte = 0x2F,
            Description = "Random/Multiple Cylinder Misfire Detected",
            EcuName = "ECM",
            CanId = 0x7E8
        });
        ecm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "P0115",
            RawDtcNumber = 0x011500,
            StatusByte = 0x29,
            Description = "Engine Coolant Temperature Circuit Malfunction",
            EcuName = "ECM",
            CanId = 0x7E8
        });
        ecm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "P0235",
            RawDtcNumber = 0x023500,
            StatusByte = 0x2F,
            Description = "BOOST PRESSURE SENSOR CRT (Boost Pressure Sensor Circuit Malfunction)",
            EcuName = "ECM",
            CanId = 0x7E8
        });
        ecm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "P0180",
            RawDtcNumber = 0x018000,
            StatusByte = 0x2F,
            Description = "FUEL TEMP SEN CRT (Fuel Temperature Sensor A Circuit Malfunction)",
            EcuName = "ECM",
            CanId = 0x7E8
        });
        ecm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "P1530",
            RawDtcNumber = 0x153000,
            StatusByte = 0x2F,
            Description = "A/C COMPRESSOR RELAY CIRCUIT (Air Conditioning Compressor Relay Circuit)",
            EcuName = "ECM",
            CanId = 0x7E8
        });
        ecm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "P1481",
            RawDtcNumber = 0x148100,
            StatusByte = 0x2F,
            Description = "RAD FAN OUTPUT 1 CRT MALF (Radiator Fan Relay 1 Control Circuit)",
            EcuName = "ECM",
            CanId = 0x7E8
        });
        ecm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "P0190",
            RawDtcNumber = 0x019000,
            StatusByte = 0x2F,
            Description = "FUEL RAIL PRESS SENS CRT (Fuel Rail Pressure Sensor Circuit)",
            EcuName = "ECM",
            CanId = 0x7E8
        });

        _ecus[ecm.RequestCanId] = ecm;

        // 2. ABS (Anti-lock Braking System): 0x7E1 / 0x7E9
        var abs = new VirtualEcu
        {
            Name = "ABS",
            RequestCanId = 0x7E1,
            ResponseCanId = 0x7E9,
            Description = "Anti-Lock Braking & Stability Control",
            Vin = "1FA6P8CF5H5123456",
            HardwareNumber = "HW-ABS-BOSCH9",
            SoftwareVersion = "v2.10-esp",
            SystemName = "ABS/ESC Dynamic Module"
        };
        abs.DataIdentifiers[0xF190] = Encoding.ASCII.GetBytes(abs.Vin);
        abs.DataIdentifiers[0xF18A] = Encoding.ASCII.GetBytes(abs.HardwareNumber);
        abs.DataIdentifiers[0xF189] = Encoding.ASCII.GetBytes(abs.SoftwareVersion);
        abs.DataIdentifiers[0xF197] = Encoding.ASCII.GetBytes(abs.SystemName);
        abs.DataIdentifiers[0xF186] = new byte[] { 0x01 };

        abs.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "C0035",
            RawDtcNumber = 0x403500,
            StatusByte = 0x28,
            Description = "Left Front Wheel Speed Circuit Malfunction",
            EcuName = "ABS",
            CanId = 0x7E9
        });

        _ecus[abs.RequestCanId] = abs;

        // 3. BCM (Body Control Module): 0x7E2 / 0x7EA
        var bcm = new VirtualEcu
        {
            Name = "BCM",
            RequestCanId = 0x7E2,
            ResponseCanId = 0x7EA,
            Description = "Body Control & Lighting Gateway",
            Vin = "1FA6P8CF5H5123456",
            HardwareNumber = "HW-BCM-GEN3",
            SoftwareVersion = "v1.85-bcm",
            SystemName = "Body Gateway Electronics"
        };
        bcm.DataIdentifiers[0xF190] = Encoding.ASCII.GetBytes(bcm.Vin);
        bcm.DataIdentifiers[0xF18A] = Encoding.ASCII.GetBytes(bcm.HardwareNumber);
        bcm.DataIdentifiers[0xF189] = Encoding.ASCII.GetBytes(bcm.SoftwareVersion);
        bcm.DataIdentifiers[0xF197] = Encoding.ASCII.GetBytes(bcm.SystemName);
        bcm.DataIdentifiers[0xF186] = new byte[] { 0x01 };

        bcm.TroubleCodes.Add(new DiagnosticTroubleCode
        {
            Code = "B1325",
            RawDtcNumber = 0x932500,
            StatusByte = 0x24,
            Description = "Device Power 1 Circuit Voltage Low",
            EcuName = "BCM",
            CanId = 0x7EA
        });

        _ecus[bcm.RequestCanId] = bcm;

        // Functional Broadcast: 0x7DF routes to ECM
        _ecus[0x7DF] = ecm;
    }

    public IReadOnlyList<VirtualEcu> GetVirtualEcus() => new List<VirtualEcu>(_ecus.Values);

    public VirtualEcu? GetEcuByRequestCanId(uint canId)
    {
        _ecus.TryGetValue(canId, out var ecu);
        return ecu;
    }

    public byte[]? ProcessDiagnosticRequest(uint requestCanId, byte[] requestPayload)
    {
        if (!IsEnabled || requestPayload == null || requestPayload.Length == 0)
        {
            return null;
        }

        if (!_ecus.TryGetValue(requestCanId, out var ecu))
        {
            return null;
        }

        byte sid = requestPayload[0];

        // Check NRC override
        if (_nrcOverrides.TryGetValue((requestCanId, sid), out var overrideNrc))
        {
            return CreateNegativeResponse((UdsServiceId)sid, (NegativeResponseCode)overrideNrc);
        }

        // 1. Check if OBD-II Request (SID 0x01 .. 0x09)
        if (sid >= 0x01 && sid <= 0x09)
        {
            return ProcessObdRequest(ecu, (ObdMode)sid, requestPayload);
        }

        // 2. Check if UDS Request (ISO 14229)
        return ProcessUdsRequest(ecu, (UdsServiceId)sid, requestPayload);
    }

    private byte[] ProcessObdRequest(VirtualEcu ecu, ObdMode mode, byte[] requestPayload)
    {
        switch (mode)
        {
            case ObdMode.CurrentData:
            case ObdMode.FreezeFrame:
                if (requestPayload.Length < 2)
                {
                    return new byte[] { 0x7F, (byte)mode, (byte)NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat };
                }
                byte pid = requestPayload[1];
                return GenerateMode01Response(pid);

            case ObdMode.StoredDTCs:
            case ObdMode.PendingDTCs:
            case ObdMode.PermanentDTCs:
                // Response: 0x43/0x47/0x4A <DTC1_H> <DTC1_L> ...
                var dtcBytes = new List<byte> { (byte)(0x40 + (byte)mode) };
                foreach (var dtc in ecu.TroubleCodes)
                {
                    if (dtc.Code.StartsWith("P") && dtc.RawDtcNumber > 0)
                    {
                        dtcBytes.Add((byte)((dtc.RawDtcNumber >> 16) & 0xFF));
                        dtcBytes.Add((byte)((dtc.RawDtcNumber >> 8) & 0xFF));
                    }
                }
                if (dtcBytes.Count == 1)
                {
                    dtcBytes.Add(0x00);
                    dtcBytes.Add(0x00);
                }
                return dtcBytes.ToArray();

            case ObdMode.ClearDTCs:
                ecu.TroubleCodes.Clear();
                return new byte[] { 0x44 }; // Positive response

            case ObdMode.VehicleInformation:
                if (requestPayload.Length >= 2 && requestPayload[1] == 0x02) // VIN
                {
                    byte[] vinBytes = Encoding.ASCII.GetBytes(ecu.Vin);
                    var resp = new byte[2 + vinBytes.Length];
                    resp[0] = 0x49;
                    resp[1] = 0x02;
                    Array.Copy(vinBytes, 0, resp, 2, vinBytes.Length);
                    return resp;
                }
                return new byte[] { 0x7F, (byte)mode, (byte)NegativeResponseCode.SubFunctionNotSupported };

            default:
                return new byte[] { 0x7F, (byte)mode, (byte)NegativeResponseCode.ServiceNotSupported };
        }
    }

    private byte[] GenerateMode01Response(byte pid)
    {
        // Positive Response SID: 0x41 <PID> <Data...>
        switch (pid)
        {
            case 0x00: // Supported PIDs 01-20 (Bitmap: EngineLoad, CoolantTemp, RPM, Speed, Throttle, etc.)
                return new byte[] { 0x41, 0x00, 0xBE, 0x1F, 0xA8, 0x13 };

            case 0x04: // Engine Load (35% -> Raw = 0x59)
                return new byte[] { 0x41, 0x04, 0x59 };

            case 0x05: // Engine Coolant Temp (88 C -> Raw = 88 + 40 = 128 = 0x80)
                return new byte[] { 0x41, 0x05, 0x80 };

            case 0x0C: // Engine RPM (2000 rpm -> Raw = 2000 * 4 = 8000 = 0x1F40)
                return new byte[] { 0x41, 0x0C, 0x1F, 0x40 };

            case 0x0D: // Vehicle Speed (65 km/h -> 0x41)
                return new byte[] { 0x41, 0x0D, 0x41 };

            case 0x0F: // Intake Air Temp (25 C -> Raw = 65 = 0x41)
                return new byte[] { 0x41, 0x0F, 0x41 };

            case 0x10: // MAF Rate (15.50 g/s -> Raw = 1550 = 0x060E)
                return new byte[] { 0x41, 0x10, 0x06, 0x0E };

            case 0x11: // Throttle Position (25% -> Raw = 64 = 0x40)
                return new byte[] { 0x41, 0x11, 0x40 };

            case 0x1F: // Run Time (450 sec -> Raw = 0x01C2)
                return new byte[] { 0x41, 0x1F, 0x01, 0xC2 };

            case 0x2F: // Fuel Level (65% -> Raw = 166 = 0xA6)
                return new byte[] { 0x41, 0x2F, 0xA6 };

            case 0x42: // Module Voltage (13.80V -> Raw = 13800 = 0x35E8)
                return new byte[] { 0x41, 0x42, 0x35, 0xE8 };

            default:
                return new byte[] { 0x7F, 0x01, (byte)NegativeResponseCode.RequestOutOfRange };
        }
    }

    private byte[] ProcessUdsRequest(VirtualEcu ecu, UdsServiceId service, byte[] requestPayload)
    {
        switch (service)
        {
            case UdsServiceId.DiagnosticSessionControl:
                if (requestPayload.Length < 2)
                {
                    return CreateNegativeResponse(service, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat);
                }
                byte sessionType = requestPayload[1];
                if (sessionType >= 0x01 && sessionType <= 0x04)
                {
                    ecu.ActiveSession = sessionType;
                    // Response: 0x50 <sessionType> <P2Server_MSB> <P2Server_LSB> <P2*Server_MSB> <P2*Server_LSB>
                    return new byte[] { 0x50, sessionType, 0x00, 0x32, 0x01, 0xF4 }; // P2=50ms, P2*=5000ms
                }
                return CreateNegativeResponse(service, NegativeResponseCode.SubFunctionNotSupported);

            case UdsServiceId.EcuReset:
                if (requestPayload.Length < 2)
                {
                    return CreateNegativeResponse(service, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat);
                }
                byte resetType = requestPayload[1];
                if (resetType == 0x01 || resetType == 0x03) // Hard or Soft Reset
                {
                    ecu.ActiveSession = 0x01;
                    ecu.SecurityUnlocked = false;
                    return new byte[] { 0x51, resetType };
                }
                return CreateNegativeResponse(service, NegativeResponseCode.SubFunctionNotSupported);

            case UdsServiceId.ClearDiagnosticInformation:
                ecu.TroubleCodes.Clear();
                return new byte[] { 0x54 }; // Positive response

            case UdsServiceId.ReadDTCInformation:
                if (requestPayload.Length < 2)
                {
                    return CreateNegativeResponse(service, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat);
                }
                byte subfunc = requestPayload[1];
                if (subfunc == 0x02) // ReportDTCByStatusMask
                {
                    var respList = new List<byte> { 0x59, 0x02, 0xFF }; // Status Availability Mask = 0xFF
                    foreach (var dtc in ecu.TroubleCodes)
                    {
                        respList.Add((byte)((dtc.RawDtcNumber >> 16) & 0xFF));
                        respList.Add((byte)((dtc.RawDtcNumber >> 8) & 0xFF));
                        respList.Add((byte)(dtc.RawDtcNumber & 0xFF));
                        respList.Add(dtc.StatusByte);
                    }
                    return respList.ToArray();
                }
                return CreateNegativeResponse(service, NegativeResponseCode.SubFunctionNotSupported);

            case UdsServiceId.ReadDataByIdentifier:
                if (requestPayload.Length < 3)
                {
                    return CreateNegativeResponse(service, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat);
                }
                ushort did = (ushort)((requestPayload[1] << 8) | requestPayload[2]);
                if (ecu.DataIdentifiers.TryGetValue(did, out var data))
                {
                    var resp = new byte[3 + data.Length];
                    resp[0] = 0x62;
                    resp[1] = requestPayload[1];
                    resp[2] = requestPayload[2];
                    Array.Copy(data, 0, resp, 3, data.Length);
                    return resp;
                }
                return CreateNegativeResponse(service, NegativeResponseCode.RequestOutOfRange);

            case UdsServiceId.SecurityAccess:
                if (requestPayload.Length < 2)
                {
                    return CreateNegativeResponse(service, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat);
                }
                byte secSub = requestPayload[1];
                if (secSub == 0x01) // Request Seed
                {
                    // Response: 0x67 0x01 <4-byte Seed>
                    return new byte[] { 0x67, 0x01, 0x12, 0x34, 0x56, 0x78 };
                }
                else if (secSub == 0x02) // Send Key
                {
                    if (requestPayload.Length >= 6 && requestPayload[2] == 0x9A && requestPayload[3] == 0xBC)
                    {
                        ecu.SecurityUnlocked = true;
                        return new byte[] { 0x67, 0x02 }; // Key acknowledged
                    }
                    return CreateNegativeResponse(service, NegativeResponseCode.InvalidKey);
                }
                return CreateNegativeResponse(service, NegativeResponseCode.SubFunctionNotSupported);

            case UdsServiceId.WriteDataByIdentifier:
                if (requestPayload.Length < 4)
                {
                    return CreateNegativeResponse(service, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat);
                }
                if (ecu.ActiveSession == 0x01) // Default session cannot write
                {
                    return CreateNegativeResponse(service, NegativeResponseCode.ServiceNotSupportedInActiveSession);
                }
                ushort writeDid = (ushort)((requestPayload[1] << 8) | requestPayload[2]);
                byte[] writeData = requestPayload[3..];
                ecu.DataIdentifiers[writeDid] = writeData;
                return new byte[] { 0x6E, requestPayload[1], requestPayload[2] };

            case UdsServiceId.RoutineControl:
                if (requestPayload.Length < 4)
                {
                    return CreateNegativeResponse(service, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat);
                }
                ushort routineId = (ushort)((requestPayload[2] << 8) | requestPayload[3]);
                if (routineId != 0x0201 && routineId != 0x0202 && routineId != 0x0203)
                {
                    return CreateNegativeResponse(service, NegativeResponseCode.RequestOutOfRange);
                }
                // Response: 0x71 <SubFunc> <RoutineId_H> <RoutineId_L> <RoutineStatus>
                return new byte[] { 0x71, requestPayload[1], requestPayload[2], requestPayload[3], 0x00 };

            case UdsServiceId.TesterPresent:
                if (requestPayload.Length >= 2 && (requestPayload[1] & 0x80) != 0)
                {
                    return Array.Empty<byte>();
                }
                return new byte[] { 0x7E, 0x00 };

            case UdsServiceId.ControlDTCSetting:
                if (requestPayload.Length >= 2)
                {
                    return new byte[] { 0xC5, requestPayload[1] };
                }
                return CreateNegativeResponse(service, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat);

            case UdsServiceId.ReadMemoryByAddress:
                if (requestPayload.Length < 8)
                {
                    return CreateNegativeResponse(service, NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat);
                }
                long readAddr = ((long)requestPayload[2] << 24) | ((long)requestPayload[3] << 16) | ((long)requestPayload[4] << 8) | requestPayload[5];
                int readLen = (requestPayload[6] << 8) | requestPayload[7];

                if (readLen <= 0 || readLen > 1024)
                {
                    return CreateNegativeResponse(service, NegativeResponseCode.RequestOutOfRange);
                }

                // Positive response: 0x63 <data...>
                byte[] simMemoryData = new byte[1 + readLen];
                simMemoryData[0] = 0x63;
                for (int i = 0; i < readLen; i++)
                {
                    long cur = readAddr + i;
                    simMemoryData[1 + i] = (byte)((cur ^ (cur >> 8) ^ 0xA5) & 0xFF);
                }
                return simMemoryData;

            default:
                return CreateNegativeResponse(service, NegativeResponseCode.ServiceNotSupported);
        }
    }

    private static byte[] CreateNegativeResponse(UdsServiceId service, NegativeResponseCode nrc)
    {
        return new byte[] { (byte)UdsServiceId.NegativeResponse, (byte)service, (byte)nrc };
    }

    public async Task<byte[]?> ProcessAndPublishSimulationResponseAsync(uint requestCanId, byte[] requestPayload, uint timestampUs = 0)
    {
        if (!IsEnabled || !_ecus.TryGetValue(requestCanId, out var ecu))
        {
            return null;
        }

        // Publish request CAN frames into pipeline
        var reqFrames = _isoTpRouter.SegmentMessage(requestPayload, requestCanId, timestampUs);
        foreach (var rf in reqFrames)
        {
            await _pipeline.PublishFrameAsync(rf);
        }

        // Generate response
        byte[]? responsePayload = ProcessDiagnosticRequest(requestCanId, requestPayload);
        if (responsePayload != null && responsePayload.Length > 0)
        {
            // Segment response into ISO-TP CAN frames and publish into pipeline
            var respFrames = _isoTpRouter.SegmentMessage(responsePayload, ecu.ResponseCanId, timestampUs + 15000); // 15ms simulated response time
            foreach (var rf in respFrames)
            {
                await _pipeline.PublishFrameAsync(rf);
            }
        }

        return responsePayload;
    }

    public void SetDtcFault(string ecuName, string dtcCode, bool active)
    {
        foreach (var ecu in _ecus.Values)
        {
            if (ecu.Name.Equals(ecuName, StringComparison.OrdinalIgnoreCase))
            {
                if (active)
                {
                    if (!ecu.TroubleCodes.Exists(d => d.Code == dtcCode))
                    {
                        ecu.TroubleCodes.Add(new DiagnosticTroubleCode
                        {
                            Code = dtcCode,
                            RawDtcNumber = EncodeDtcToRawNumber(dtcCode),
                            StatusByte = 0x2F,
                            Description = DtcDecoder.GetDescription(dtcCode),
                            EcuName = ecu.Name,
                            CanId = ecu.ResponseCanId
                        });
                    }
                }
                else
                {
                    ecu.TroubleCodes.RemoveAll(d => d.Code == dtcCode);
                }
            }
        }
    }

    public void SetSimulatedVoltage(string ecuName, double voltage)
    {
        foreach (var ecu in _ecus.Values)
        {
            if (ecu.Name.Equals(ecuName, StringComparison.OrdinalIgnoreCase))
            {
                ushort mv = (ushort)Math.Clamp((int)(voltage * 1000), 0, 65535);
                ecu.DataIdentifiers[0x0102] = new byte[] { (byte)(mv >> 8), (byte)(mv & 0xFF) };
            }
        }
    }

    public void SetSimulatedDid(string ecuName, ushort did, byte[] data)
    {
        foreach (var ecu in _ecus.Values)
        {
            if (ecu.Name.Equals(ecuName, StringComparison.OrdinalIgnoreCase))
            {
                ecu.DataIdentifiers[did] = data;
            }
        }
    }

    public void SetNrcOverride(uint requestCanId, byte serviceId, byte nrc)
    {
        _nrcOverrides[(requestCanId, serviceId)] = nrc;
    }

    public void ClearNrcOverrides()
    {
        _nrcOverrides.Clear();
    }

    public void ResetAll()
    {
        _nrcOverrides.Clear();
        InitializeDefaultVirtualEcus();
    }

    private static uint EncodeDtcToRawNumber(string dtcCode)
    {
        if (string.IsNullOrWhiteSpace(dtcCode) || dtcCode.Length < 5) return 0x030000;
        char prefix = char.ToUpperInvariant(dtcCode[0]);
        byte pBits = prefix switch
        {
            'C' => 1,
            'B' => 2,
            'U' => 3,
            _ => 0
        };
        if (int.TryParse(dtcCode.AsSpan(1, 1), System.Globalization.NumberStyles.HexNumber, null, out int d1) &&
            int.TryParse(dtcCode.AsSpan(2, 1), System.Globalization.NumberStyles.HexNumber, null, out int d2) &&
            int.TryParse(dtcCode.AsSpan(3, 1), System.Globalization.NumberStyles.HexNumber, null, out int d3) &&
            int.TryParse(dtcCode.AsSpan(4, 1), System.Globalization.NumberStyles.HexNumber, null, out int d4))
        {
            byte high = (byte)(((pBits & 0x03) << 6) | ((d1 & 0x03) << 4) | (d2 & 0x0F));
            byte mid = (byte)(((d3 & 0x0F) << 4) | (d4 & 0x0F));
            return (uint)((high << 16) | (mid << 8));
        }
        return 0x030000;
    }
}
