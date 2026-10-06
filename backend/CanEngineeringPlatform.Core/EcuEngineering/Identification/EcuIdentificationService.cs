using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.EcuInfo;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Uds;
using CanEngineeringPlatform.Core.EcuEngineering.Compatibility;
using CanEngineeringPlatform.Core.EcuEngineering.Core;

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
    private readonly Dictionary<string, EcuIdentificationProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public EcuIdentificationService(
        IDiagnosticSessionService diagnosticSession,
        IEcuCompatibilityManager compatibilityManager)
    {
        _diagnosticSession = diagnosticSession ?? throw new ArgumentNullException(nameof(diagnosticSession));
        _compatibilityManager = compatibilityManager ?? throw new ArgumentNullException(nameof(compatibilityManager));
    }

    public async Task<EcuIdentificationProfile> IdentifyEcuAsync(uint requestCanId = 0x7E0, uint responseCanId = 0x7E8, string ecuName = "ECM")
    {
        var profile = new EcuIdentificationProfile
        {
            EcuName = ecuName,
            RequestCanId = requestCanId,
            ResponseCanId = responseCanId,
            IdentificationTimestampUtc = DateTime.UtcNow
        };

        // Standard UDS Identification DIDs (ISO 14229-1)
        // 0xF190: VIN
        // 0xF187: Spare Part Number
        // 0xF191: ECU Hardware Number
        // 0xF194: Software Number
        // 0xF189: Software Version
        // 0xF180: Boot Software ID
        // 0xF18C: Serial Number
        // 0xF19E: ASAM / ODX Name

        string? partNum = null;
        string? hwNum = null;
        string? swNum = null;
        string? swVer = null;
        string? vin = null;
        string? bootVer = null;

        // Query VIN (0xF190)
        var vinResp = await QueryDidAsync(requestCanId, 0xF190, ecuName);
        if (vinResp.Success && !string.IsNullOrWhiteSpace(vinResp.Text))
        {
            vin = vinResp.Text.Trim();
            var evidence = EvidenceSource.Create("UdsDid", "0xF190", "UDS ReadDataByIdentifier: Vehicle Identification Number", vinResp.RawHex);
            profile.Vin = FieldWithConfidence<string>.Confirmed(vin, evidence);
            profile.AllEvidence.Add(evidence);
        }

        // Query Part Number (0xF187)
        var partResp = await QueryDidAsync(requestCanId, 0xF187, ecuName);
        if (partResp.Success && !string.IsNullOrWhiteSpace(partResp.Text))
        {
            partNum = partResp.Text.Trim();
            var evidence = EvidenceSource.Create("UdsDid", "0xF187", "UDS ReadDataByIdentifier: Vehicle Manufacturer Spare Part Number", partResp.RawHex);
            profile.EcuPartNumber = FieldWithConfidence<string>.Confirmed(partNum, evidence);
            profile.AllEvidence.Add(evidence);
        }

        // Query Hardware Number (0xF191)
        var hwResp = await QueryDidAsync(requestCanId, 0xF191, ecuName);
        if (hwResp.Success && !string.IsNullOrWhiteSpace(hwResp.Text))
        {
            hwNum = hwResp.Text.Trim();
            var evidence = EvidenceSource.Create("UdsDid", "0xF191", "UDS ReadDataByIdentifier: Vehicle Manufacturer ECU Hardware Number", hwResp.RawHex);
            profile.HardwareNumber = FieldWithConfidence<string>.Confirmed(hwNum, evidence);
            profile.AllEvidence.Add(evidence);
        }

        // Query Software Number (0xF194)
        var swResp = await QueryDidAsync(requestCanId, 0xF194, ecuName);
        if (swResp.Success && !string.IsNullOrWhiteSpace(swResp.Text))
        {
            swNum = swResp.Text.Trim();
            var evidence = EvidenceSource.Create("UdsDid", "0xF194", "UDS ReadDataByIdentifier: System Supplier ECU Software Number", swResp.RawHex);
            profile.SoftwareNumber = FieldWithConfidence<string>.Confirmed(swNum, evidence);
            profile.AllEvidence.Add(evidence);
        }

        // Query Software Version (0xF189)
        var verResp = await QueryDidAsync(requestCanId, 0xF189, ecuName);
        if (verResp.Success && !string.IsNullOrWhiteSpace(verResp.Text))
        {
            swVer = verResp.Text.Trim();
            var evidence = EvidenceSource.Create("UdsDid", "0xF189", "UDS ReadDataByIdentifier: Application Software Version", verResp.RawHex);
            profile.SoftwareVersion = FieldWithConfidence<string>.Confirmed(swVer, evidence);
            profile.AllEvidence.Add(evidence);
        }

        // Query Bootloader Version (0xF180)
        var bootResp = await QueryDidAsync(requestCanId, 0xF180, ecuName);
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

    private async Task<(bool Success, string Text, string RawHex)> QueryDidAsync(uint canId, ushort did, string ecuName)
    {
        byte[] request = new byte[] { 0x22, (byte)(did >> 8), (byte)(did & 0xFF) };
        try
        {
            var exchange = await _diagnosticSession.SendRequestAsync(canId, request, ecuName, isSimulation: true);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!exchange.IsComplete && sw.ElapsedMilliseconds < 250)
            {
                await Task.Delay(10);
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
