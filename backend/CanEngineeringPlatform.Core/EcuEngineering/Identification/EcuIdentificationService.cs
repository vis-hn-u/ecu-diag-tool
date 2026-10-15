using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.EcuInfo;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Uds;
using CanEngineeringPlatform.Core.EcuEngineering.Compatibility;
using CanEngineeringPlatform.Core.EcuEngineering.Core;

using CanEngineeringPlatform.Core.Hardware;

namespace CanEngineeringPlatform.Core.EcuEngineering.Identification;

public interface IEcuIdentificationService
{
    Task<EcuIdentificationProfile> IdentifyEcuAsync(uint requestCanId = 0x7E0, uint responseCanId = 0x7E8, string ecuName = "ECM");
    IReadOnlyList<EcuIdentificationProfile> GetAllIdentifiedEcus();
    EcuIdentificationProfile? GetProfile(string ecuName);
}

public class EcuIdentificationService : IEcuIdentificationService
{
    private readonly IDiagnosticSessionService _diagnosticSession;
    private readonly IEcuCompatibilityManager _compatibilityManager;
    private readonly IHardwareManager? _hardwareManager;
    private readonly Dictionary<string, EcuIdentificationProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public EcuIdentificationService(
        IDiagnosticSessionService diagnosticSession,
        IEcuCompatibilityManager compatibilityManager,
        IHardwareManager? hardwareManager = null)
    {
        _diagnosticSession = diagnosticSession ?? throw new ArgumentNullException(nameof(diagnosticSession));
        _compatibilityManager = compatibilityManager ?? throw new ArgumentNullException(nameof(compatibilityManager));
        _hardwareManager = hardwareManager;
    }

    public async Task<EcuIdentificationProfile> IdentifyEcuAsync(uint requestCanId = 0x7E0, uint responseCanId = 0x7E8, string ecuName = "ECM")
    {
        bool isSimulation = _hardwareManager == null || _hardwareManager.GetStatus()?.IsConnected != true;

        var profile = new EcuIdentificationProfile
        {
            EcuName = ecuName,
            RequestCanId = requestCanId,
            ResponseCanId = responseCanId,
            IdentificationTimestampUtc = DateTime.UtcNow
        };

        string? partNum = null;
        string? hwNum = null;
        string? swNum = null;
        string? swVer = null;
        string? vin = null;
        string? bootVer = null;

        // Multi-protocol live discovery when hardware is connected
        if (!isSimulation)
        {
            // 1. Query VIN via OBD-II Mode 09 PID 02
            try
            {
                var vinEx = await _diagnosticSession.SendRequestAsync(requestCanId, new byte[] { 0x09, 0x02 }, ecuName, isSimulation: false);
                int wait = 0;
                while (!vinEx.IsComplete && wait < 600) { await Task.Delay(20); wait += 20; }
                if (vinEx.IsSuccess && vinEx.ResponseDataHex != null)
                {
                    byte[] raw = ParseHexBytes(vinEx.ResponseDataHex);
                    if (raw.Length >= 4)
                    {
                        int offset = raw[2] == 0x01 ? 3 : 2;
                        vin = CleanAscii(Encoding.ASCII.GetString(raw, offset, raw.Length - offset));
                        if (!string.IsNullOrWhiteSpace(vin))
                        {
                            var evidence = EvidenceSource.Create("ObdMode09", "PID_0x02", "OBD-II Mode 09 Vehicle Identification Number (VIN)", vinEx.ResponseDataHex);
                            profile.Vin = FieldWithConfidence<string>.Confirmed(vin, evidence);
                            profile.AllEvidence.Add(evidence);
                        }
                    }
                }
            }
            catch { }

            // 2. Query ECU Identification via KWP2000 Service 0x1A 0x80 (MEDC17 info)
            try
            {
                await _diagnosticSession.SendRequestAsync(requestCanId, new byte[] { 0x10, 0x81 }, ecuName, isSimulation: false);
                await Task.Delay(40);

                var kwpEx = await _diagnosticSession.SendRequestAsync(requestCanId, new byte[] { 0x1A, 0x80 }, ecuName, isSimulation: false);
                int wait = 0;
                while (!kwpEx.IsComplete && wait < 800) { await Task.Delay(20); wait += 20; }
                if (kwpEx.IsSuccess && kwpEx.ResponseDataHex != null)
                {
                    byte[] raw = ParseHexBytes(kwpEx.ResponseDataHex);
                    string ascii = CleanAscii(Encoding.ASCII.GetString(raw));
                    var evidence = EvidenceSource.Create("KWP2000", "0x1A_0x80", "KWP2000 ReadECUIdentification: Calibration & Mcu Parameters", kwpEx.ResponseDataHex);
                    profile.AllEvidence.Add(evidence);

                    var swMatch = System.Text.RegularExpressions.Regex.Match(ascii, @"1037\d{6}");
                    if (swMatch.Success)
                    {
                        swNum = swMatch.Value;
                        profile.SoftwareNumber = FieldWithConfidence<string>.Confirmed(swNum, evidence);
                    }

                    if (ascii.Contains("tc1767", StringComparison.OrdinalIgnoreCase))
                    {
                        hwNum = "Infineon TriCore TC1767";
                        profile.HardwareNumber = FieldWithConfidence<string>.Confirmed(hwNum, evidence);
                    }
                    else if (ascii.Contains("MEDC17", StringComparison.OrdinalIgnoreCase))
                    {
                        hwNum = "Bosch MEDC17 (EDC17C55)";
                        profile.HardwareNumber = FieldWithConfidence<string>.Confirmed(hwNum, evidence);
                    }
                }
            }
            catch { }

            // 3. Query ECU Name via OBD-II Mode 09 PID 0A
            try
            {
                var nameEx = await _diagnosticSession.SendRequestAsync(requestCanId, new byte[] { 0x09, 0x0A }, ecuName, isSimulation: false);
                int wait = 0;
                while (!nameEx.IsComplete && wait < 600) { await Task.Delay(20); wait += 20; }
                if (nameEx.IsSuccess && nameEx.ResponseDataHex != null)
                {
                    byte[] raw = ParseHexBytes(nameEx.ResponseDataHex);
                    if (raw.Length >= 4)
                    {
                        int offset = raw[2] == 0x01 ? 3 : 2;
                        string sysName = CleanAscii(Encoding.ASCII.GetString(raw, offset, raw.Length - offset));
                        if (!string.IsNullOrWhiteSpace(sysName))
                        {
                            var evidence = EvidenceSource.Create("ObdMode09", "PID_0x0A", "OBD-II Mode 09 ECU Name", nameEx.ResponseDataHex);
                            profile.EcuPartNumber = FieldWithConfidence<string>.Confirmed(sysName, evidence);
                            profile.AllEvidence.Add(evidence);
                        }
                    }
                }
            }
            catch { }
        }

        // Standard UDS Identification DIDs (ISO 14229-1)
        // Query VIN (0xF190)
        if (string.IsNullOrEmpty(vin))
        {
            var vinResp = await QueryDidAsync(requestCanId, 0xF190, ecuName, isSimulation);
            if (vinResp.Success && !string.IsNullOrWhiteSpace(vinResp.Text))
            {
                vin = vinResp.Text.Trim();
                var evidence = EvidenceSource.Create("UdsDid", "0xF190", "UDS ReadDataByIdentifier: Vehicle Identification Number", vinResp.RawHex);
                profile.Vin = FieldWithConfidence<string>.Confirmed(vin, evidence);
                profile.AllEvidence.Add(evidence);
            }
        }

        // Query Part Number (0xF187)
        if (string.IsNullOrEmpty(partNum))
        {
            var partResp = await QueryDidAsync(requestCanId, 0xF187, ecuName, isSimulation);
            if (partResp.Success && !string.IsNullOrWhiteSpace(partResp.Text))
            {
                partNum = partResp.Text.Trim();
                var evidence = EvidenceSource.Create("UdsDid", "0xF187", "UDS ReadDataByIdentifier: Vehicle Manufacturer Spare Part Number", partResp.RawHex);
                profile.EcuPartNumber = FieldWithConfidence<string>.Confirmed(partNum, evidence);
                profile.AllEvidence.Add(evidence);
            }
        }

        // Query Hardware Number (0xF191)
        if (string.IsNullOrEmpty(hwNum))
        {
            var hwResp = await QueryDidAsync(requestCanId, 0xF191, ecuName, isSimulation);
            if (hwResp.Success && !string.IsNullOrWhiteSpace(hwResp.Text))
            {
                hwNum = hwResp.Text.Trim();
                var evidence = EvidenceSource.Create("UdsDid", "0xF191", "UDS ReadDataByIdentifier: Vehicle Manufacturer ECU Hardware Number", hwResp.RawHex);
                profile.HardwareNumber = FieldWithConfidence<string>.Confirmed(hwNum, evidence);
                profile.AllEvidence.Add(evidence);
            }
        }

        // Query Software Number (0xF194)
        if (string.IsNullOrEmpty(swNum))
        {
            var swResp = await QueryDidAsync(requestCanId, 0xF194, ecuName, isSimulation);
            if (swResp.Success && !string.IsNullOrWhiteSpace(swResp.Text))
            {
                swNum = swResp.Text.Trim();
                var evidence = EvidenceSource.Create("UdsDid", "0xF194", "UDS ReadDataByIdentifier: System Supplier ECU Software Number", swResp.RawHex);
                profile.SoftwareNumber = FieldWithConfidence<string>.Confirmed(swNum, evidence);
                profile.AllEvidence.Add(evidence);
            }
        }

        // Query Software Version (0xF189)
        var verResp = await QueryDidAsync(requestCanId, 0xF189, ecuName, isSimulation);
        if (verResp.Success && !string.IsNullOrWhiteSpace(verResp.Text))
        {
            swVer = verResp.Text.Trim();
            var evidence = EvidenceSource.Create("UdsDid", "0xF189", "UDS ReadDataByIdentifier: Application Software Version", verResp.RawHex);
            profile.SoftwareVersion = FieldWithConfidence<string>.Confirmed(swVer, evidence);
            profile.AllEvidence.Add(evidence);
        }

        // Query Bootloader Version (0xF180)
        var bootResp = await QueryDidAsync(requestCanId, 0xF180, ecuName, isSimulation);
        if (bootResp.Success && !string.IsNullOrWhiteSpace(bootResp.Text))
        {
            bootVer = bootResp.Text.Trim();
            var evidence = EvidenceSource.Create("UdsDid", "0xF180", "UDS ReadDataByIdentifier: Boot Software Identification", bootResp.RawHex);
            profile.BootloaderInfo = FieldWithConfidence<string>.Confirmed(bootVer, evidence);
            profile.AllEvidence.Add(evidence);
        }

        // Correlate with existing Diagnostics session EcuInfo cache if available
        var existingInfo = _diagnosticSession.GetEcuInfo(ecuName);
        if (existingInfo != null)
        {
            if (profile.Vin.Confidence == IdentificationConfidence.Unknown && !string.IsNullOrWhiteSpace(existingInfo.Vin))
            {
                var ev = EvidenceSource.Create("DiagnosticSessionCache", "VIN", "Cached VIN from Diagnostic Session");
                profile.Vin = FieldWithConfidence<string>.Detected(existingInfo.Vin, ev);
                profile.AllEvidence.Add(ev);
                vin = existingInfo.Vin;
            }
            if (profile.HardwareNumber.Confidence == IdentificationConfidence.Unknown && !string.IsNullOrWhiteSpace(existingInfo.HardwareNumber))
            {
                var ev = EvidenceSource.Create("DiagnosticSessionCache", "HardwareNumber", "Cached Hardware Number");
                profile.HardwareNumber = FieldWithConfidence<string>.Detected(existingInfo.HardwareNumber, ev);
                profile.AllEvidence.Add(ev);
                hwNum = existingInfo.HardwareNumber;
            }
            if (profile.SoftwareNumber.Confidence == IdentificationConfidence.Unknown && !string.IsNullOrWhiteSpace(existingInfo.SoftwareNumber))
            {
                var ev = EvidenceSource.Create("DiagnosticSessionCache", "SoftwareNumber", "Cached Software Number");
                profile.SoftwareNumber = FieldWithConfidence<string>.Detected(existingInfo.SoftwareNumber, ev);
                profile.AllEvidence.Add(ev);
                swNum = existingInfo.SoftwareNumber;
            }
        }

        // Correlate with OEM Compatibility Database
        var matchedFamily = _compatibilityManager.MatchProfile(hwNum, swNum, ecuName);
        if (matchedFamily != null)
        {
            var dbEv = EvidenceSource.Create("OemCompatibilityDatabase", matchedFamily.FamilyId, $"Correlated with verified OEM profile {matchedFamily.Name}");
            profile.EcuFamily = FieldWithConfidence<string>.Confirmed(matchedFamily.Name, dbEv);
            profile.Manufacturer = FieldWithConfidence<string>.Confirmed(matchedFamily.Manufacturer, dbEv);
            profile.McuArchitecture = FieldWithConfidence<string>.Inferred($"{matchedFamily.McuModel} ({matchedFamily.Mcu})", dbEv);
            profile.MemoryCharacteristics = FieldWithConfidence<string>.Inferred($"Flash: {matchedFamily.FlashSizeBytes / 1024 / 1024}MB, EEPROM: {matchedFamily.EepromSizeBytes / 1024}KB", dbEv);
            profile.DiagnosticProtocol = FieldWithConfidence<string>.Confirmed(matchedFamily.PrimaryDiagnosticProtocol, dbEv);
            profile.CanBitrate = FieldWithConfidence<uint>.Confirmed(matchedFamily.DefaultBaudRate, dbEv);
            profile.CanAddressing = FieldWithConfidence<string>.Confirmed($"Standard 11-Bit (Req: 0x{matchedFamily.DefaultRequestCanId:X3}, Resp: 0x{matchedFamily.DefaultResponseCanId:X3})", dbEv);

            if (matchedFamily.TypicalPlatforms.Count > 0)
            {
                profile.VehiclePlatform = FieldWithConfidence<string>.Inferred(string.Join(", ", matchedFamily.TypicalPlatforms), dbEv);
            }
            if (profile.HardwareNumber.Confidence == IdentificationConfidence.Unknown && matchedFamily.HardwareNumberPatterns.Count > 0)
            {
                profile.HardwareNumber = FieldWithConfidence<string>.Inferred(matchedFamily.HardwareNumberPatterns[0], dbEv);
            }
            if (profile.SoftwareNumber.Confidence == IdentificationConfidence.Unknown && matchedFamily.SoftwareNumberPatterns.Count > 0)
            {
                profile.SoftwareNumber = FieldWithConfidence<string>.Inferred(matchedFamily.SoftwareNumberPatterns[0], dbEv);
            }
            if (profile.CalibrationId.Confidence == IdentificationConfidence.Unknown && !string.IsNullOrEmpty(matchedFamily.CalIdPattern))
            {
                profile.CalibrationId = FieldWithConfidence<string>.Inferred(matchedFamily.CalIdPattern, dbEv);
            }
            if (matchedFamily.SupportedProtocols.Count > 0)
            {
                profile.SupportedServices = new List<string>(matchedFamily.SupportedProtocols);
            }
            profile.ExtendedParameters["MicroprocessorFamily"] = matchedFamily.MicroprocessorFamily;
            profile.ExtendedParameters["InstructionSet"] = matchedFamily.InstructionSet;
            profile.ExtendedParameters["CoreClock"] = $"{matchedFamily.CoreClockMhz} MHz";
            profile.ExtendedParameters["ExternalFlash"] = matchedFamily.ExternalFlashChip;
            profile.ExtendedParameters["Eeprom"] = matchedFamily.EepromChip;
            profile.ExtendedParameters["ChecksumAlgorithm"] = matchedFamily.ChecksumAlgorithm;
            profile.ExtendedParameters["CanAddressingType"] = matchedFamily.CanAddressingType;
            profile.ExtendedParameters["KlineProtocol"] = matchedFamily.KlineProtocol;
            profile.ExtendedParameters["BootPinDetails"] = matchedFamily.BootPinDetails;
            profile.ExtendedParameters["ProgrammingMethods"] = string.Join("; ", matchedFamily.ProgrammingMethods);
            profile.AllEvidence.Add(dbEv);
        }
        else
        {
            profile.DiagnosticProtocol = FieldWithConfidence<string>.Detected("UDS (ISO 14229) / OBD-II", EvidenceSource.Create("Default", "CAN", "Standard automotive diagnostic protocol"));
            profile.CanBitrate = FieldWithConfidence<uint>.Detected(500000, EvidenceSource.Create("Default", "500k", "Nominal 500 kbps automotive CAN"));
            profile.CanAddressing = FieldWithConfidence<string>.Detected($"Standard 11-Bit (0x{requestCanId:X3} -> 0x{responseCanId:X3})", EvidenceSource.Create("Addressing", "11-bit", "Standard ECM addressing"));
        }

        // Determine Overall Confidence
        if (profile.HardwareNumber.Confidence == IdentificationConfidence.Confirmed &&
            profile.SoftwareNumber.Confidence == IdentificationConfidence.Confirmed)
        {
            profile.OverallConfidence = IdentificationConfidence.Confirmed;
        }
        else if (profile.HardwareNumber.Confidence != IdentificationConfidence.Unknown ||
                 profile.SoftwareNumber.Confidence != IdentificationConfidence.Unknown ||
                 profile.Vin.Confidence != IdentificationConfidence.Unknown)
        {
            profile.OverallConfidence = IdentificationConfidence.Detected;
        }
        else if (profile.EcuFamily.Confidence == IdentificationConfidence.Inferred)
        {
            profile.OverallConfidence = IdentificationConfidence.Inferred;
        }
        else
        {
            profile.OverallConfidence = IdentificationConfidence.Unknown;
        }

        _profiles[ecuName] = profile;
        return profile;
    }

    public IReadOnlyList<EcuIdentificationProfile> GetAllIdentifiedEcus()
    {
        return _profiles.Values.ToList();
    }

    public EcuIdentificationProfile? GetProfile(string ecuName)
    {
        if (string.IsNullOrWhiteSpace(ecuName)) return null;
        _profiles.TryGetValue(ecuName, out var profile);
        return profile;
    }

    private async Task<(bool Success, string Text, string RawHex)> QueryDidAsync(uint canId, ushort did, string ecuName, bool isSimulation = true)
    {
        byte[] request = new byte[] { 0x22, (byte)(did >> 8), (byte)(did & 0xFF) };
        try
        {
            var exchange = await _diagnosticSession.SendRequestAsync(canId, request, ecuName, isSimulation: isSimulation);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!exchange.IsComplete && sw.ElapsedMilliseconds < 500)
            {
                await Task.Delay(15);
            }

            if (exchange.IsSuccess && exchange.ResponseDataHex != null)
            {
                string rawHex = exchange.ResponseDataHex;
                // Parse UDS 0x62 DID response: 0x62 [DID_H] [DID_L] [DATA...]
                string[] parts = rawHex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && parts[0] == "62")
                {
                    List<byte> dataBytes = new();
                    for (int i = 3; i < parts.Length; i++)
                    {
                        if (byte.TryParse(parts[i], System.Globalization.NumberStyles.HexNumber, null, out byte b))
                        {
                            dataBytes.Add(b);
                        }
                    }
                    string decodedAscii = CleanAscii(Encoding.ASCII.GetString(dataBytes.ToArray()));
                    return (true, decodedAscii, rawHex);
                }
            }
        }
        catch
        {
            // Ignore failure, return empty
        }
        return (false, string.Empty, string.Empty);
    }

    private static byte[] ParseHexBytes(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return Array.Empty<byte>();
        var parts = hex.Split(new[] { ' ', '-', ':' }, StringSplitOptions.RemoveEmptyEntries);
        var bytes = new List<byte>(parts.Length);
        foreach (var p in parts)
        {
            if (byte.TryParse(p, System.Globalization.NumberStyles.HexNumber, null, out byte b))
            {
                bytes.Add(b);
            }
        }
        return bytes.ToArray();
    }

    private static string CleanAscii(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
        {
            if (c >= 32 && c <= 126) sb.Append(c);
        }
        return sb.ToString().Trim();
    }
}
