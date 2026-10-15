using System;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.EcuEngineering.Compatibility;

public interface IEcuCompatibilityManager
{
    IReadOnlyList<EcuFamilyProfile> GetAllProfiles();
    EcuFamilyProfile? GetProfileById(string familyId);
    EcuFamilyProfile? MatchProfile(string? hardwareNumber, string? softwareNumber, string? ecuName = null);
    EcuVariantProfile? GetVariant(string familyId, string variantId);
    EcuVariantProfile? MatchVariant(string? hardwareNumber, string? softwareNumber);
    void RegisterProfile(EcuFamilyProfile profile);
}

public class EcuCompatibilityManager : IEcuCompatibilityManager
{
    private readonly Dictionary<string, EcuFamilyProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public EcuCompatibilityManager()
    {
        SeedDefaultProfiles();
    }

    public IReadOnlyList<EcuFamilyProfile> GetAllProfiles()
    {
        return _profiles.Values.ToList();
    }

    public EcuFamilyProfile? GetProfileById(string familyId)
    {
        if (string.IsNullOrWhiteSpace(familyId)) return null;
        if (_profiles.TryGetValue(familyId, out var profile)) return profile;
        if (familyId.Equals("DENSO_SH7058_D4D", StringComparison.OrdinalIgnoreCase))
            return _profiles.TryGetValue("DENSO_SH7058", out var p1) ? p1 : null;
        if (familyId.Equals("DENSO_SH7058", StringComparison.OrdinalIgnoreCase))
            return _profiles.TryGetValue("DENSO_SH7058_D4D", out var p2) ? p2 : null;
        return null;
    }

    public EcuVariantProfile? GetVariant(string familyId, string variantId)
    {
        var profile = GetProfileById(familyId);
        if (profile == null || string.IsNullOrWhiteSpace(variantId)) return null;
        return profile.Variants.FirstOrDefault(v => v.VariantId.Equals(variantId, StringComparison.OrdinalIgnoreCase));
    }

    public EcuVariantProfile? MatchVariant(string? hardwareNumber, string? softwareNumber)
    {
        string hw = hardwareNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        string sw = softwareNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(hw) && string.IsNullOrEmpty(sw)) return null;

        foreach (var profile in _profiles.Values)
        {
            foreach (var variant in profile.Variants)
            {
                if (!string.IsNullOrEmpty(variant.HardwareNumber) && hw.Contains(variant.HardwareNumber.ToUpperInvariant()))
                    return variant;
                if (!string.IsNullOrEmpty(variant.SoftwareNumber) && sw.Contains(variant.SoftwareNumber.ToUpperInvariant()))
                    return variant;
            }
        }
        return null;
    }

    public EcuFamilyProfile? MatchProfile(string? hardwareNumber, string? softwareNumber, string? ecuName = null)
    {
        // 1. Direct key lookup if ecuName is a family ID or known key
        if (!string.IsNullOrWhiteSpace(ecuName) && _profiles.TryGetValue(ecuName, out var directProfile))
        {
            return directProfile;
        }

        string hw = hardwareNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        string sw = softwareNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        string name = ecuName?.Trim().ToUpperInvariant() ?? string.Empty;

        // 2. Exact match against declared Hardware / Software patterns
        foreach (var profile in _profiles.Values)
        {
            if (profile.HardwareNumberPatterns.Any(p => hw.Contains(p.ToUpperInvariant())) ||
                profile.SoftwareNumberPatterns.Any(p => sw.Contains(p.ToUpperInvariant())))
            {
                return profile;
            }
        }

        // 3. Magneti Marelli MJD Family
        if (hw.Contains("MJD 9") || sw.Contains("MJD 9") || name.Contains("9DF") || name.Contains("9GF"))
            return GetProfileById("MAGNETI_MARELLI_MJD9");

        if (hw.Contains("MJD 8") || sw.Contains("MJD 8") || name.Contains("8F2") || name.Contains("8F3") || name.Contains("8DF") ||
            hw.Contains("51904061") || hw.Contains("55246942"))
            return GetProfileById("MAGNETI_MARELLI_MJD8");

        if (hw.Contains("MARELLI") || sw.Contains("MARELLI") ||
            hw.Contains("MERELLI") || sw.Contains("MERELLI") ||
            hw.Contains("MAGNETI") || sw.Contains("MAGNETI") ||
            hw.Contains("MJD") || sw.Contains("MJD") ||
            hw.Contains("SWIFT") || sw.Contains("SWIFT") ||
            hw.Contains("6F3") || sw.Contains("3422S") || hw.Contains("33920-84E") ||
            name.Contains("MARELLI") || name.Contains("MERELLI") || name.Contains("MJD") || name.Contains("SWIFT"))
        {
            return GetProfileById("MAGNETI_MARELLI_MJD");
        }

        // 4. Bosch MD1 / MG1 (Next-Gen BS6 / Euro 6d)
        if (hw.Contains("MD1") || sw.Contains("MD1") || name.Contains("MD1") || name.Contains("THAR BS6") || name.Contains("SCORPIO-N DIESEL"))
            return GetProfileById("BOSCH_MD1CS018");

        if (hw.Contains("MG1") || sw.Contains("MG1") || name.Contains("MG1") || name.Contains("MSTALLION"))
            return GetProfileById("BOSCH_MG1CS016");

        // 5. Bosch EDC17 Family
        if (hw.Contains("EDC17C53") || sw.Contains("EDC17C53") || name.Contains("EDC17C53") || name.Contains("MHAWK 2.2") || name.Contains("XUV500") || name.Contains("THAR CRDE"))
            return GetProfileById("BOSCH_EDC17C53");

        if (hw.Contains("EDC17C55") || sw.Contains("EDC17C55") || name.Contains("EDC17C55") ||
            hw.Contains("17C55") || sw.Contains("17C55") || name.Contains("17C55") ||
            hw.Contains("C04") || sw.Contains("C04") || name.Contains("C04") ||
            hw.Contains("C63") || sw.Contains("C63") || name.Contains("C63") ||
            sw.Contains("1037535157") || sw.Contains("1037508746") || sw.Contains("P94455A") ||
            hw.Contains("MEDC17") || sw.Contains("MEDC17") || hw.Contains("TC1767") ||
            name.Contains("BOLERO") || name.Contains("TUV300") || hw.Contains("MA1FA2HR") || sw.Contains("MA1FA2HR"))
        {
            return GetProfileById("BOSCH_EDC17C55");
        }

        if (hw.Contains("EDC17C63") || sw.Contains("EDC17C63") || name.Contains("EDC17C63") || name.Contains("HEXA"))
            return GetProfileById("BOSCH_EDC17C55");

        if (hw.Contains("EDC17CP14") || sw.Contains("EDC17CP14") || name.Contains("EDC17CP14") || hw.Contains("03L906022"))
            return GetProfileById("BOSCH_EDC17CP14");

        if (hw.Contains("EDC17") || sw.Contains("EDC17") || name.Contains("EDC17") || hw.Contains("04L906021"))
            return GetProfileById("BOSCH_EDC17C64");

        // 6. Bosch EDC16 Family
        if (hw.Contains("EDC16C8") || hw.Contains("EDC16C9") || name.Contains("EDC16C8") || name.Contains("INDIGO"))
            return GetProfileById("BOSCH_EDC16C8");

        if (hw.Contains("EDC16") || sw.Contains("EDC16") || name.Contains("EDC16") ||
            hw.Contains("0281013328") || hw.Contains("0281012543") || hw.Contains("0281011550") ||
            name.Contains("SCORPIO 2.6") || name.Contains("SAFARI 2.2"))
        {
            return GetProfileById("BOSCH_EDC16C39");
        }

        // 7. Bosch Gasoline (ME7 / MED9 / MED17)
        if (hw.Contains("ME7") || sw.Contains("ME7") || name.Contains("ME7") || hw.Contains("06A906032"))
            return GetProfileById("BOSCH_ME7_5");

        if (hw.Contains("MED9") || sw.Contains("MED9") || name.Contains("MED9") || hw.Contains("06F906056"))
            return GetProfileById("BOSCH_MED9_1");

        if (hw.Contains("MED17") || sw.Contains("MED17") || name.Contains("MED17") || hw.Contains("06J906026") || sw.Contains("06J906026"))
            return GetProfileById("BOSCH_MED17_5_5");

        // 8. Continental / Siemens PCR & SID & Simos
        if (hw.Contains("PCR2.1") || sw.Contains("PCR2.1") || name.Contains("PCR2") || hw.Contains("03L906023") || sw.Contains("03L906023") || name.Contains("POLO TDI"))
            return GetProfileById("CONTINENTAL_PCR2_1");

        if (hw.Contains("SID807") || sw.Contains("SID807") || name.Contains("SID807") || name.Contains("FIGO") || name.Contains("ECOSPORT"))
            return GetProfileById("CONTINENTAL_SID807");

        if (hw.Contains("SID310") || sw.Contains("SID310") || name.Contains("SID310") || name.Contains("DUSTER") || name.Contains("TERRANO"))
            return GetProfileById("CONTINENTAL_SID310");

        if (hw.Contains("SIMOS") || sw.Contains("SIMOS") || name.Contains("SIMOS") || hw.Contains("5G0906259") || sw.Contains("5G0906259"))
            return GetProfileById("SIEMENS_SIMOS18_1");

        // 9. Delphi DCM Family
        if (hw.Contains("DCM6") || hw.Contains("DCM7") || name.Contains("DCM6") || name.Contains("DCM7") || name.Contains("HARRIER") || name.Contains("SAFARI BS6"))
            return GetProfileById("DELPHI_DCM6_2");

        if (hw.Contains("DCM") || sw.Contains("DCM") || name.Contains("DCM") || name.Contains("MANZA") || name.Contains("I20 CRDI"))
            return GetProfileById("DELPHI_DCM3_7");

        // 10. Denso Family
        if (hw.Contains("GD") || name.Contains("CRYSTA") || name.Contains("FORTUNER") || hw.Contains("89661-0K") || name.Contains("1GD") || name.Contains("2GD"))
            return GetProfileById("DENSO_SH72544_GD");

        if (hw.Contains("ETIOS") || name.Contains("ETIOS") || name.Contains("D-4D") || hw.Contains("89661-02"))
            return GetProfileById("DENSO_SH7058_D4D");

        if (name.Contains("SWIFT PETROL") || name.Contains("BALENO PETROL") || hw.Contains("33920-68K") || hw.Contains("33920-73P"))
            return GetProfileById("DENSO_SUZUKI_PETROL");

        if (hw.Contains("SH7058") || sw.Contains("SH7058") || hw.Contains("DENSO") || sw.Contains("DENSO") || name.Contains("DENSO"))
            return GetProfileById("DENSO_SH7058_D4D");

        return null;
    }

    public void RegisterProfile(EcuFamilyProfile profile)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.FamilyId)) return;
        _profiles[profile.FamilyId] = profile;
    }

    private void SeedDefaultProfiles()
    {
        // =========================================================================
        // 1. MAGNETI MARELLI MJD 6F3 (Maruti Swift 1.3 DDiS / Fiat MultiJet)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "MAGNETI_MARELLI_MJD",
            Name = "Magneti Marelli MJD 6F3 (Swift Diesel 1.3 DDiS)",
            Manufacturer = "Magneti Marelli",
            Category = "Multijet Common Rail Turbo Diesel",
            TypicalPlatforms = new() { "Maruti Suzuki Swift DDiS (1.3L MultiJet)", "Maruti Dzire DDiS", "Maruti Ertiga DDiS", "Fiat Punto / Linea 1.3 Multijet", "Opel Corsa 1.3 CDTI" },
            Mcu = McuArchitecture.NxpPowerPc_MPC55xx,
            McuModel = "MPC563 / MPC555 (PowerPC 32-bit RISC @ 56 MHz)",
            MicroprocessorFamily = "Motorola / Freescale MPC5xx PowerPC Architecture",
            InstructionSet = "PowerPC Book E 32-bit RISC",
            CoreClockMhz = 56,
            ExternalFlashChip = "ST M58BW016DB (2 MB Parallel Flash 16-bit)",
            EepromChip = "ST M95320 / M95640 (32 KB / 64 KB SPI Serial EEPROM)",
            FlashSizeBytes = 2097152, // 2 MB Flash
            EepromSizeBytes = 65536,  // 64 KB SPI EEPROM
            HardwareNumberPatterns = new() { "MJD 6F3.S1", "MJD 6F3.D4", "MJD 6F3.P7", "51826694", "55568600", "33920-84E00", "33920-84E61" },
            SoftwareNumberPatterns = new() { "3422S167", "3423S168", "2944D148", "BC.0084323.A", "3454P766" },
            CalIdPattern = "3422S167_SWIFT_DDiS",
            CvnPattern = "0x4A81E9B3",
            SupportedProtocols = new() { "KWP2000 on CAN (ISO 14230-4)", "UDS on ISO-TP (ISO 14229)", "K-Line ISO 14230-2 Fast Init (10.4 kbps)", "OBD-II (ISO 15765-4)" },
            PrimaryDiagnosticProtocol = "KWP2000 & UDS (ISO 14229) on ISO-TP (ISO 15765-2)",
            DefaultBaudRate = 500000,
            CanAddressingType = "11-bit Standard (Req: 0x7E0, Resp: 0x7E8)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            FunctionalBroadcastCanId = 0x7DF,
            KlineProtocol = "ISO 14230-2 Fast Init (25ms wake pulse @ 10400 baud)",
            SecurityAccessLevels = new()
            {
                ["0x01 / 0x02"] = "Standard Diagnostic Session / Injector Coding / DPF Regeneration (Algorithm: Seed-Key XOR Poly)",
                ["0x03 / 0x04"] = "Dynamometer & Engineering Adaptation Routine",
                ["0x11 / 0x12"] = "Flash Reprogramming / Memory Write Access"
            },
            BenchPinout = new()
            {
                ["Pin 1, 2"] = "+12V Permanent Battery Power (Terminal 30)",
                ["Pin 4, 5"] = "+12V Switched Ignition Power (Terminal 15)",
                ["Pin 3"] = "Ground / Battery Negative (Terminal 31)",
                ["Pin 23"] = "CAN High (CAN-H 500 kbps)",
                ["Pin 33"] = "CAN Low (CAN-L 500 kbps)",
                ["Pin 25"] = "K-Line (ISO 9141/14230 Diagnostic Line)",
                ["BDM Port"] = "Nexus 14-pin PCB header pads (Pads 1-14 on rear board for MPC5xx BDM programmer)"
            },
            ProgrammingMethods = new() { "OBD Virtual Read (Server Baseline)", "OBD Diagnostic Patch Read", "Bench Service Mode (K-Line / CAN pinout)", "BDM 100 Hardware Nexus Probe" },
            BootPinDetails = "BDM Nexus 14-pin interface on PCB rear side",
            BdmJtagDetails = "Motorola BDM 14-pin: VDD, GND, SRESET, HRESET, DSCI, DSCO, DSDI, TCK, TMS",
            ChecksumAlgorithm = "Magneti Marelli Multi-Block 16/32-bit Additive CRC",
            KnownConstraints = new()
            {
                "Seed-Key security access required for ECU flash and adaptation routines",
                "Dual rail pressure regulation (high pressure pump metering unit + rail regulator)",
                "Maruti Suzuki DDiS immobilizer handshake verification (ST95640 sync)",
                "A/C compressor cut-off and radiator fan thermal thresholds monitored"
            },
            RecoveryProcedures = new()
            {
                "BDM / JTAG hardware recovery on bench (MPC55xx Nexus interface)",
                "Full flash restore from clean Marelli baseline binary (.bin)",
                "EEPROM re-alignment for injector code programming and immobilizer sync"
            },
            MemoryRegions = new()
            {
                new() { RegionName = "Bootloader / Hardware Init", StartAddress = 0x00000000, LengthBytes = 65536, IsWritable = false, RequiresSecurityAccess = true, Description = "Internal PowerPC Bootloader & Vector Table" },
                new() { RegionName = "Application Software", StartAddress = 0x00010000, LengthBytes = 1048576, IsWritable = true, RequiresSecurityAccess = true, Description = "Marelli DDiS Operating System & Control Strategy" },
                new() { RegionName = "Calibration Maps", StartAddress = 0x00110000, LengthBytes = 524288, IsWritable = true, RequiresSecurityAccess = true, Description = "Rail Pressure, Injection Timing, Boost Target & Smoke Limiters" },
                new() { RegionName = "EEPROM Emulation / Config", StartAddress = 0x00190000, LengthBytes = 65536, IsWritable = true, RequiresSecurityAccess = true, Description = "Injector IMA Codes, Adaptation Maps, DTC Log & Odometer" }
            },
            OemNormalLimits = new()
            {
                ["BatteryVoltage"] = (11.8, 14.8, "V"),
                ["CoolantTemp"] = (75.0, 100.0, "°C"),
                ["FuelRailPressure"] = (250.0, 1600.0, "bar"),
                ["BoostPressure"] = (0.9, 2.4, "bar abs"),
                ["IntakeAirTemp"] = (10.0, 60.0, "°C"),
                ["IdleRpm"] = (800.0, 900.0, "RPM")
            },
            ReleaseValidation = ReleaseValidationState.DatabaseSourceVerified,
            PinoutValidation = PinoutValidationState.VariantDependent,
            ConflictingPinoutVariantsFound = true,
            ConflictNotice = "MJD 6F3.S1 (Swift 2007-2011) uses Pin 23 CAN-H / Pin 33 CAN-L; MJD 6F3.D4 (Fiat Punto) uses Pin 23 CAN-H / Pin 24 CAN-L. Verify harness connector variant before applying bench power.",
            SourceProvenance = "Magneti Marelli MultiJet Engineering Specification / OEM Wiring Diagram",
            Variants = new()
            {
                new()
                {
                    VariantId = "MJD_6F3_S1_SWIFT",
                    VariantName = "MJD 6F3.S1 (Maruti Swift DDiS 2007-2011)",
                    HardwareNumber = "33920-84E00",
                    SoftwareNumber = "3422S167",
                    ConnectorDesignation = "64-Pin Main Connector A",
                    VehiclePlatform = "Maruti Suzuki Swift DDiS 1.3L",
                    McuModel = "MPC563 / MPC555",
                    BenchPinout = new()
                    {
                        ["Pin 1, 2"] = "+12V Permanent Battery (Terminal 30)",
                        ["Pin 4, 5"] = "+12V Switched Ignition (Terminal 15)",
                        ["Pin 3"] = "Ground (Terminal 31)",
                        ["Pin 23"] = "CAN High (CAN-H 500 kbps)",
                        ["Pin 33"] = "CAN Low (CAN-L 500 kbps)",
                        ["Pin 25"] = "K-Line (ISO 9141/14230)"
                    },
                    PinoutValidation = PinoutValidationState.Confirmed,
                    Evidence = EvidenceState.Confirmed,
                    SourceReference = "Maruti Suzuki Workshop Manual / Swift 1.3 DDiS ECU Pinout"
                },
                new()
                {
                    VariantId = "MJD_6F3_D4_FIAT",
                    VariantName = "MJD 6F3.D4 (Fiat Punto / Linea 1.3 MultiJet)",
                    HardwareNumber = "51826694",
                    SoftwareNumber = "2944D148",
                    ConnectorDesignation = "Fiat 60-Pin Harness Connector",
                    VehiclePlatform = "Fiat Punto 1.3 MultiJet 75HP / Linea 90HP",
                    McuModel = "MPC563",
                    BenchPinout = new()
                    {
                        ["Pin 1, 2"] = "+12V Permanent Battery (Terminal 30)",
                        ["Pin 4, 5"] = "+12V Switched Ignition (Terminal 15)",
                        ["Pin 3"] = "Ground (Terminal 31)",
                        ["Pin 23"] = "CAN High (CAN-H 500 kbps)",
                        ["Pin 24"] = "CAN Low (CAN-L 500 kbps)",
                        ["Pin 11"] = "K-Line"
                    },
                    PinoutValidation = PinoutValidationState.Reference,
                    Evidence = EvidenceState.DatabaseMatch,
                    SourceReference = "Fiat ePER / MultiJet Technical Service Reference"
                }
            }
        });

        // =========================================================================
        // 2. MAGNETI MARELLI MJD 8F2 / 8F3 / 8DF (Maruti Swift/Ertiga/Ciaz BS4)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "MAGNETI_MARELLI_MJD8",
            Name = "Magneti Marelli MJD 8F2 / 8F3 / 8DF (1.3 MultiJet BS4)",
            Manufacturer = "Magneti Marelli",
            Category = "Multijet II Common Rail Turbo Diesel",
            TypicalPlatforms = new() { "Maruti Suzuki Swift BS4", "Maruti Ertiga DDiS", "Maruti Ciaz 1.3 DDiS", "Maruti S-Cross 1.3 DDiS", "Fiat 500 / Punto Evo 1.3" },
            Mcu = McuArchitecture.NxpPowerPc_MPC56xx,
            McuModel = "MPC5565 / MPC5634 (PowerPC e200z6 @ 132 MHz)",
            MicroprocessorFamily = "Freescale / ST Qorivva MPC56xx / MPC55xx",
            InstructionSet = "PowerPC e200z6 32-bit RISC Dual-Issue",
            CoreClockMhz = 132,
            ExternalFlashChip = "Internal 2 MB Micro Flash",
            EepromChip = "ST M95640 (64 KB SPI Serial EEPROM)",
            FlashSizeBytes = 2097152,
            EepromSizeBytes = 65536,
            HardwareNumberPatterns = new() { "MJD 8F3.B2", "MJD 8F2.G1", "MJD 8DF", "51904061", "55246942", "BC.0102554" },
            SoftwareNumberPatterns = new() { "4142D183", "4144B185", "BC.0102554.A", "3645B183" },
            CalIdPattern = "4142D183_8F3_CAL",
            CvnPattern = "0x89D2F144",
            SupportedProtocols = new() { "UDS on CAN (ISO 14229)", "KWP2000 on K-Line", "OBD-II (ISO 15765-4)" },
            PrimaryDiagnosticProtocol = "UDS (ISO 14229) on ISO-TP (ISO 15765-2)",
            DefaultBaudRate = 500000,
            CanAddressingType = "11-bit Standard (Req: 0x7E0, Resp: 0x7E8)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            FunctionalBroadcastCanId = 0x7DF,
            KlineProtocol = "ISO 14230-4 KWP2000 (10.4 kbps)",
            SecurityAccessLevels = new()
            {
                ["0x01 / 0x02"] = "Diagnostic Session & DTC Reset",
                ["0x11 / 0x12"] = "Marelli Cryptographic Flash Unlock (Seed-Key 32-bit)"
            },
            BenchPinout = new()
            {
                ["Pin 1"] = "+12V Battery Power",
                ["Pin 4"] = "+12V Ignition Power",
                ["Pin 2, 3"] = "Chassis Ground",
                ["Pin 50"] = "CAN High (CAN-H)",
                ["Pin 51"] = "CAN Low (CAN-L)",
                ["Pin 28"] = "K-Line Diagnostic"
            },
            ProgrammingMethods = new() { "OBD Virtual Read", "Bench Service Mode", "JTAG Nexus 14-pin Header" },
            ChecksumAlgorithm = "Marelli 8F3 Multi-Block 32-bit Additive CRC"
        });

        // =========================================================================
        // 3. BOSCH EDC16C39 (Mahindra Scorpio 2.6/2.2, Bolero, Tata Safari Dicor)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "BOSCH_EDC16C39",
            Name = "Bosch EDC16C39 (Mahindra / Tata CRDe / Dicor)",
            Manufacturer = "Bosch",
            Category = "Common Rail Turbo Diesel",
            TypicalPlatforms = new() { "Mahindra Scorpio 2.6 CRDe / 2.2 mHawk (early)", "Mahindra Bolero CRDe", "Tata Safari 2.2 / 3.0 Dicor", "Tata Xenon 2.2 Dicor", "Fiat Linea 1.3 Multijet" },
            Mcu = McuArchitecture.NxpPowerPc_MPC55xx,
            McuModel = "Motorola / Freescale MPC562 / MPC564 (PowerPC @ 66 MHz)",
            MicroprocessorFamily = "Motorola MPC5xx PowerPC Architecture",
            InstructionSet = "PowerPC 32-bit RISC with Floating Point Unit",
            CoreClockMhz = 66,
            ExternalFlashChip = "AMD / Spansion AM29BL802CB / S29CD016 (2 MB 16-bit Parallel Flash)",
            EepromChip = "ST M95640 (64 KB SPI Serial EEPROM)",
            FlashSizeBytes = 2097152, // 2 MB Flash
            EepromSizeBytes = 65536,  // 64 KB EEPROM
            HardwareNumberPatterns = new() { "0281013328", "0281012543", "0281011550", "0281015155", "0281012229", "0281013824" },
            SoftwareNumberPatterns = new() { "1037386780", "1037395211", "1037390900", "1037378854", "1037391992" },
            CalIdPattern = "1037386780_MHAWK_CRDE",
            CvnPattern = "0x78B1CA02",
            SupportedProtocols = new() { "KWP2000 on CAN (ISO 14230-4)", "K-Line ISO 14230-2 Fast Init (10.4 kbps)", "OBD-II (ISO 15765-4 500k 11-bit)" },
            PrimaryDiagnosticProtocol = "KWP2000 (ISO 14230-4) on CAN & K-Line",
            DefaultBaudRate = 500000,
            CanAddressingType = "11-bit Standard (Req: 0x7E0, Resp: 0x7E8)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            FunctionalBroadcastCanId = 0x7DF,
            KlineProtocol = "ISO 14230-2 Fast Init (25ms low pulse @ 10400 baud)",
            SecurityAccessLevels = new()
            {
                ["0x01 / 0x02"] = "KWP2000 Seed-Key Routine (Mahindra / Bosch EDC16 Polynomial)",
                ["0x11 / 0x12"] = "Flash Reprogramming / Map Write Access"
            },
            BenchPinout = new()
            {
                ["Pin 1, 2"] = "+12V Permanent Battery (Terminal 30)",
                ["Pin 4"] = "+12V Switched Ignition (Terminal 15)",
                ["Pin 3"] = "Battery Negative / Chassis Ground (Terminal 31)",
                ["Pin 43"] = "CAN High (CAN-H 500 kbps)",
                ["Pin 44"] = "CAN Low (CAN-L 500 kbps)",
                ["Pin 72"] = "K-Line Diagnostic Line",
                ["BDM 100"] = "Standard 10-pin BDM Header on board corner (Pin 1=VFLS0, Pin 2=SRESET, Pin 3=GND, Pin 4=HRESET, Pin 5=VCC, Pin 6=Freeze, Pin 7=DSDI, Pin 8=DSCI, Pin 9=TCK, Pin 10=TDO)"
            },
            ProgrammingMethods = new() { "OBD KWP2000 Read/Write", "BDM 100 Hardware Probe (Full Flash + EEPROM)", "Bench Service Mode" },
            BootPinDetails = "BDM 10-pin header direct Nexus probe",
            BdmJtagDetails = "Motorola BDM 10-pin 2.54mm pitch",
            ChecksumAlgorithm = "Bosch EDC16 MPC5xx 16-bit / 32-bit Additive Sum Block",
            KnownConstraints = new()
            {
                "Bosch EDC16 checksum validation covers OS, Maps, and Configuration sectors",
                "Mahindra immobilizer sync stored in ST95640 EEPROM bytes 0x0180-0x0200",
                "Dual injector bank drive stages (Bank 1 & Bank 2 peak-and-hold solenoid)"
            },
            RecoveryProcedures = new()
            {
                "Full recovery via BDM 100 probe without desoldering",
                "Baseline flash flash-back via external AM29BL802CB programmer"
            },
            MemoryRegions = new()
            {
                new() { RegionName = "Internal MPC5xx Flash / Boot", StartAddress = 0x00000000, LengthBytes = 524288, IsWritable = false, RequiresSecurityAccess = true, Description = "MPC562 Internal Microcode & Bootloader" },
                new() { RegionName = "External Flash / Application", StartAddress = 0x00080000, LengthBytes = 1048576, IsWritable = true, RequiresSecurityAccess = true, Description = "Bosch Operating System Engine Logic" },
                new() { RegionName = "Calibration Maps", StartAddress = 0x00180000, LengthBytes = 524288, IsWritable = true, RequiresSecurityAccess = true, Description = "Rail, SOI, Smoke Limiter, Boost Maps" },
                new() { RegionName = "EEPROM Data", StartAddress = 0x001F0000, LengthBytes = 65536, IsWritable = true, RequiresSecurityAccess = true, Description = "ST95640 Injector Coding & Immobilizer" }
            }
        });

        // =========================================================================
        // 4. BOSCH EDC17C53 (Mahindra Scorpio 2.2 mHawk BS4, XUV500 2.2, Thar CRDe)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "BOSCH_EDC17C53",
            Name = "Bosch EDC17C53 (Mahindra Scorpio mHawk 2.2 / XUV500)",
            Manufacturer = "Bosch",
            Category = "Common Rail Turbo Diesel (Euro 4 / BS4)",
            TypicalPlatforms = new() { "Mahindra Scorpio 2.2 mHawk BS4", "Mahindra XUV500 2.2 mHawk", "Mahindra Thar CRDe", "Tata Safari Storme 2.2 Varicor", "Force Gurkha 2.2 / 2.6" },
            Mcu = McuArchitecture.InfineonTriCore_TC17xx,
            McuModel = "Infineon TriCore TC1767 (32-bit RISC @ 133 MHz)",
            MicroprocessorFamily = "Infineon TriCore AUDO MAX Family",
            InstructionSet = "TriCore 1.3.1 32-bit RISC Dual-Core",
            CoreClockMhz = 133,
            ExternalFlashChip = "Internal 2 MB Flash (TC1767 On-Chip Embedded Flash)",
            EepromChip = "64 KB Emulated EEPROM in DFlash",
            FlashSizeBytes = 2097152, // 2 MB Flash
            EepromSizeBytes = 65536,
            HardwareNumberPatterns = new() { "0281018300", "0281019623", "0281031386", "0281018890", "0281030141" },
            SoftwareNumberPatterns = new() { "1037512211", "1037528340", "1037541299", "1037553310", "1037532100" },
            CalIdPattern = "1037512211_EDC17C53_MHAWK",
            CvnPattern = "0xB904D18E",
            SupportedProtocols = new() { "UDS on ISO-TP (ISO 14229)", "OBD-II (ISO 15765-4 500k 11-bit)", "ASAM MCD-1 XCP on CAN" },
            PrimaryDiagnosticProtocol = "UDS (ISO 14229) on ISO-TP (ISO 15765-2)",
            DefaultBaudRate = 500000,
            CanAddressingType = "11-bit Standard (Req: 0x7E0, Resp: 0x7E8)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            FunctionalBroadcastCanId = 0x7DF,
            KlineProtocol = "Not Connected (Full High-Speed CAN Architecture)",
            SecurityAccessLevels = new()
            {
                ["0x01 / 0x02"] = "Mahindra Diagnostic & Special Service Function (Service 0x27)",
                ["0x03 / 0x04"] = "Development & Dyno Calibration Access",
                ["0x11 / 0x12"] = "Tuning Protection (TPROT) Reprogramming Flash Key"
            },
            BenchPinout = new()
            {
                ["Pin 1, 2"] = "+12V Permanent Battery Power",
                ["Pin 5"] = "+12V Switched Ignition Terminal 15",
                ["Pin 3, 4"] = "Chassis Ground (Terminal 31)",
                ["Pin 67"] = "CAN High (CAN-H 500 kbps)",
                ["Pin 68"] = "CAN Low (CAN-L 500 kbps)",
                ["TC1767 Boot"] = "Boot pin located on PCB underside - connect to Ground via 1kΩ resistor for BSL mode"
            },
            ProgrammingMethods = new() { "Bench Service Mode (No open ECU, GPT Password Probe)", "Boot TriCore BSL (Open ECU, 1kΩ Boot Pin)", "OBD Virtual Read" },
            BootPinDetails = "TC1767 Boot pin (P14.5) to Ground through 1kΩ resistor",
            BdmJtagDetails = "Infineon DAP (Device Access Port) 10-pin pad pattern",
            ChecksumAlgorithm = "Bosch TriCore CRC32 / Multi-Block Additive Sum",
            KnownConstraints = new()
            {
                "Bosch TPROT (Tuning Protection V08-V10) active on EDC17C53 bootloader",
                "Mahindra VGT Turbo and Common Rail (1800 bar) closed-loop monitoring",
                "Injector IMA 7-character calibration codes in DFlash"
            },
            RecoveryProcedures = new()
            {
                "Boot TriCore BSL mode with 1kΩ resistor bypasses corrupted software",
                "Full flash + DFlash backup restores complete ECU identity"
            },
            ReleaseValidation = ReleaseValidationState.DatabaseSourceVerified,
            PinoutValidation = PinoutValidationState.Confirmed,
            ConflictingPinoutVariantsFound = false,
            SourceProvenance = "Bosch Diesel Systems Service Documentation / Mahindra Wiring Diagram",
            Variants = new()
            {
                new()
                {
                    VariantId = "EDC17C53_MHAWK_SCORPIO",
                    VariantName = "Bosch EDC17C53 (Mahindra Scorpio mHawk 2.2 BS4)",
                    HardwareNumber = "0281018300",
                    SoftwareNumber = "1037512211",
                    ConnectorDesignation = "94-pin Engine Harness (A) + 60-pin Vehicle (B)",
                    VehiclePlatform = "Mahindra Scorpio 2.2 mHawk BS4 140PS",
                    McuModel = "Infineon TriCore TC1767",
                    BenchPinout = new()
                    {
                        ["Pin A1, A2"] = "+12V Permanent Battery Power",
                        ["Pin A5"] = "+12V Switched Ignition Terminal 15",
                        ["Pin A3, A4"] = "Chassis Ground",
                        ["Pin A67"] = "CAN High (CAN-H 500 kbps)",
                        ["Pin A68"] = "CAN Low (CAN-L 500 kbps)",
                        ["Boot Pin"] = "TC1767 Boot pin to Ground via 1kΩ resistor"
                    },
                    PinoutValidation = PinoutValidationState.Confirmed,
                    Evidence = EvidenceState.Confirmed,
                    SourceReference = "Mahindra Service Manual EDC17C53 Wiring Architecture"
                },
                new()
                {
                    VariantId = "EDC17C53_STORME_VARICOR",
                    VariantName = "Bosch EDC17C53 (Tata Safari Storme 2.2 Varicor)",
                    HardwareNumber = "0281030141",
                    SoftwareNumber = "1037541299",
                    ConnectorDesignation = "94-pin Engine Harness (A) + 60-pin Vehicle (B)",
                    VehiclePlatform = "Tata Safari Storme 2.2 Varicor 156PS",
                    McuModel = "Infineon TriCore TC1767",
                    BenchPinout = new()
                    {
                        ["Pin A1, A2"] = "+12V Permanent Battery Power",
                        ["Pin A5"] = "+12V Switched Ignition Terminal 15",
                        ["Pin A3, A4"] = "Chassis Ground",
                        ["Pin A67"] = "CAN High (CAN-H 500 kbps)",
                        ["Pin A68"] = "CAN Low (CAN-L 500 kbps)",
                        ["Boot Pin"] = "TC1767 Boot pin to Ground via 1kΩ resistor"
                    },
                    PinoutValidation = PinoutValidationState.Confirmed,
                    Evidence = EvidenceState.DatabaseMatch,
                    SourceReference = "Tata Motors Service Bulletin / Varicor 2.2 Bosch EDC17"
                }
            }
        });

        // =========================================================================
        // 4a. BOSCH EDC17C55 (Mahindra Bolero Power+ / TUV300 / Thar CRDe / Scorpio)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "BOSCH_EDC17C55",
            Name = "Bosch EDC17C55 (Mahindra Bolero Power+ / TUV300 / Thar CRDe)",
            Manufacturer = "Bosch",
            Category = "Common Rail Turbo Diesel (Euro 4 / Euro 5 / BS4)",
            TypicalPlatforms = new() { "Mahindra Bolero Power+ 1.5 mHawk70", "Mahindra TUV300 1.5 mHawk80/mHawk100", "Mahindra Thar CRDe", "Mahindra Scorpio 2.2 mHawk", "Force Gurkha 2.6 CR", "Ashok Leyland Dost 1.5 TD" },
            Mcu = McuArchitecture.InfineonTriCore_TC17xx,
            McuModel = "Infineon TriCore TC1724 / TC1767 (32-bit RISC @ 133 MHz)",
            MicroprocessorFamily = "Infineon TriCore AUDO MAX Family",
            InstructionSet = "TriCore 1.3.1 32-bit RISC Dual-Core",
            CoreClockMhz = 133,
            ExternalFlashChip = "Internal 1.5 MB / 2 MB Flash (TC1724 / TC1767 Embedded Flash)",
            EepromChip = "64 KB Emulated EEPROM in DFlash",
            FlashSizeBytes = 2097152, // 2 MB Flash
            EepromSizeBytes = 65536,
            HardwareNumberPatterns = new() { "0281030085", "0281031353", "0281031386", "0281032338", "0281030438", "0281030439", "0281032483", "0281033282", "0281030436", "0281019623", "0281030086", "C04", "C63", "EDC17C55", "17C55", "MEDC17", "tc1767", "TC1767" },
            SoftwareNumberPatterns = new() { "1037535157", "1037508746", "1037511004", "1037528340", "1037541299", "1037512211", "P94455A", "P94430A" },
            CalIdPattern = "P94455A_1037535157",
            CvnPattern = "0xB904D18E",
            SupportedProtocols = new() { "OBD-II (ISO 15765-4 500k 11-bit)", "KWP2000 on CAN (ISO 14230-4)", "UDS on ISO-TP (ISO 14229)", "ASAM MCD-1 XCP on CAN" },
            PrimaryDiagnosticProtocol = "KWP2000 (ISO 14230-4) & OBD-II (ISO 15765-4)",
            DefaultBaudRate = 500000,
            CanAddressingType = "11-bit Standard (Req: 0x7E0, Resp: 0x7E8)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            FunctionalBroadcastCanId = 0x7DF,
            KlineProtocol = "Not Connected (Full High-Speed CAN Architecture)",
            SecurityAccessLevels = new()
            {
                ["0x01 / 0x02"] = "Mahindra Diagnostic & Special Service Function (Service 0x27)",
                ["0x03 / 0x04"] = "Development & Dyno Calibration Access",
                ["0x11 / 0x12"] = "Tuning Protection (TPROT) Reprogramming Flash Key"
            },
            BenchPinout = new()
            {
                ["Pin 1, 2"] = "+12V Permanent Battery Power (Terminal 30)",
                ["Pin 5"] = "+12V Switched Ignition Terminal 15",
                ["Pin 3, 4"] = "Chassis Ground (Terminal 31)",
                ["Pin 67"] = "CAN High (CAN-H 500 kbps)",
                ["Pin 68"] = "CAN Low (CAN-L 500 kbps)",
                ["TC1767 Boot"] = "Boot pin located on PCB underside - connect to Ground via 1kΩ resistor for BSL mode"
            },
            ProgrammingMethods = new() { "Bench Service Mode (No open ECU, GPT Password Probe)", "Boot TriCore BSL (Open ECU, 1kΩ Boot Pin)", "OBD Virtual Read" },
            BootPinDetails = "TC1767 Boot pin (P14.5) to Ground through 1kΩ resistor",
            BdmJtagDetails = "Infineon DAP (Device Access Port) 10-pin pad pattern",
            ChecksumAlgorithm = "Bosch TriCore CRC32 / Multi-Block Additive Sum",
            KnownConstraints = new()
            {
                "Bosch TPROT (Tuning Protection V08-V11) active on EDC17C55 bootloader",
                "Mahindra VGT Turbo and Common Rail (1800 bar) closed-loop monitoring",
                "Injector IMA 7-character calibration codes in DFlash"
            },
            RecoveryProcedures = new()
            {
                "Boot TriCore BSL mode with 1kΩ resistor bypasses corrupted software",
                "Full flash + DFlash backup restores complete ECU identity"
            },
            ReleaseValidation = ReleaseValidationState.DatabaseSourceVerified,
            PinoutValidation = PinoutValidationState.Confirmed,
            ConflictingPinoutVariantsFound = false,
            SourceProvenance = "Bosch Diesel Systems Service Documentation / Mahindra Wiring Diagram",
            Variants = new()
            {
                new()
                {
                    VariantId = "EDC17C55_C04_BOLERO",
                    VariantName = "Bosch EDC17C55 C04 (Mahindra Bolero Power+ 1.5 mHawk70)",
                    HardwareNumber = "0281030085",
                    SoftwareNumber = "1037535157",
                    ConnectorDesignation = "94-pin Engine Harness (A) + 60-pin Vehicle (B)",
                    VehiclePlatform = "Mahindra Bolero Power+ 1.5 mHawk70",
                    McuModel = "Infineon TriCore TC1767",
                    BenchPinout = new()
                    {
                        ["Pin A1, A2"] = "+12V Permanent Battery Power",
                        ["Pin A5"] = "+12V Switched Ignition Terminal 15",
                        ["Pin A3, A4"] = "Chassis Ground",
                        ["Pin A67"] = "CAN High (CAN-H 500 kbps)",
                        ["Pin A68"] = "CAN Low (CAN-L 500 kbps)",
                        ["Boot Pin"] = "TC1767 Boot pin to Ground via 1kΩ resistor"
                    },
                    PinoutValidation = PinoutValidationState.Confirmed,
                    Evidence = EvidenceState.Confirmed,
                    SourceReference = "Mahindra Service Manual EDC17C55 C04 Wiring Architecture"
                },
                new()
                {
                    VariantId = "EDC17C55_C63_TUV300",
                    VariantName = "Bosch EDC17C55 C63 (Mahindra TUV300 1.5 mHawk100)",
                    HardwareNumber = "0281032483",
                    SoftwareNumber = "1037541299",
                    ConnectorDesignation = "94-pin Engine Harness (A) + 60-pin Vehicle (B)",
                    VehiclePlatform = "Mahindra TUV300 1.5 mHawk100",
                    McuModel = "Infineon TriCore TC1767",
                    BenchPinout = new()
                    {
                        ["Pin A1, A2"] = "+12V Permanent Battery Power",
                        ["Pin A5"] = "+12V Switched Ignition Terminal 15",
                        ["Pin A3, A4"] = "Chassis Ground",
                        ["Pin A67"] = "CAN High (CAN-H 500 kbps)",
                        ["Pin A68"] = "CAN Low (CAN-L 500 kbps)",
                        ["Boot Pin"] = "TC1767 Boot pin to Ground via 1kΩ resistor"
                    },
                    PinoutValidation = PinoutValidationState.Confirmed,
                    Evidence = EvidenceState.Confirmed,
                    SourceReference = "Mahindra Service Manual EDC17C55 C63 Wiring Architecture"
                }
            }
        });

        // =========================================================================
        // 4b. BOSCH EDC17C64 (VAG 1.6 / 2.0 TDI EA288 MQB, Polo TDI, Octavia TDI)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "BOSCH_EDC17C64",
            Name = "Bosch EDC17C64 (VAG 1.6 / 2.0 TDI EA288 MQB)",
            Manufacturer = "Bosch",
            Category = "Common Rail Turbo Diesel (EA288 Clean Diesel)",
            TypicalPlatforms = new() { "VW Polo 1.5 TDI / Golf 7 2.0 TDI", "Skoda Octavia 2.0 TDI EA288", "Audi A3 / A4 2.0 TDI", "Seat Leon 2.0 TDI" },
            Mcu = McuArchitecture.InfineonTriCore_TC17xx,
            McuModel = "Infineon TriCore TC1797 (32-bit RISC @ 180 MHz)",
            MicroprocessorFamily = "Infineon TriCore AUDO-MAX TC179x",
            InstructionSet = "TriCore 1.3.1 32-bit RISC",
            CoreClockMhz = 180,
            ExternalFlashChip = "Internal 4 MB High-Speed Flash",
            EepromChip = "64 KB Emulated Data Flash EEPROM",
            FlashSizeBytes = 4194304, // 4 MB Flash
            EepromSizeBytes = 65536,
            HardwareNumberPatterns = new() { "04L906021", "04L907309", "0281030000", "0281019000" },
            SoftwareNumberPatterns = new() { "1037536900", "1037542000", "1037550100" },
            CalIdPattern = "04L906021M_5420",
            CvnPattern = "0x5C89F123",
            SupportedProtocols = new() { "UDS on ISO-TP (ISO 14229)", "OBD-II (ISO 15765-4 500k 11-bit)", "ASAM MCD-1 XCP on CAN" },
            PrimaryDiagnosticProtocol = "UDS (ISO 14229) on ISO-TP (ISO 15765-2)",
            DefaultBaudRate = 500000,
            CanAddressingType = "11-bit Standard (Req: 0x7E0, Resp: 0x7E8)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            FunctionalBroadcastCanId = 0x7DF,
            BenchPinout = new()
            {
                ["Pin 1, 2"] = "+12V Permanent Battery Power",
                ["Pin 5"] = "+12V Switched Ignition Terminal 15",
                ["Pin 3, 4"] = "Chassis Ground",
                ["Pin 67"] = "CAN High (CAN-H 500 kbps)",
                ["Pin 68"] = "CAN Low (CAN-L 500 kbps)",
                ["Boot Pin"] = "TC1797 Boot pin to Ground via 1kΩ resistor"
            },
            ProgrammingMethods = new() { "Bench Service Mode (GPT / Password)", "Boot TriCore BSL Mode", "OBD Virtual Read" },
            BootPinDetails = "Boot pin on PCB rear to Ground via 1kΩ resistor",
            ChecksumAlgorithm = "Bosch TriCore CRC32 / Additive Multi-Block",
            KnownConstraints = new()
            {
                "Strict DPF soot model and SCR AdBlue metering validation",
                "Multi-pulse piezo/solenoid injection quantity balance",
                "TPROT Level 10+ active"
            },
            RecoveryProcedures = new()
            {
                "Bench BSL / Boot mode with TC1797 boot pin resistor",
                "Full flash restore via verified OEM calibration map"
            },
            MemoryRegions = new()
            {
                new() { RegionName = "Bootloader", StartAddress = 0x80000000, LengthBytes = 65536, IsWritable = false, RequiresSecurityAccess = true, Description = "Internal Bootloader & Hardware Init" },
                new() { RegionName = "Application Software", StartAddress = 0x80010000, LengthBytes = 2097152, IsWritable = true, RequiresSecurityAccess = true, Description = "Operating Engine Management Code" },
                new() { RegionName = "Calibration Maps", StartAddress = 0x80210000, LengthBytes = 1048576, IsWritable = true, RequiresSecurityAccess = true, Description = "Fuel, Boost, EGR, DPF & Rail Pressure Tables" },
                new() { RegionName = "EEPROM Emulation", StartAddress = 0x80310000, LengthBytes = 65536, IsWritable = true, RequiresSecurityAccess = true, Description = "Adaptations, DTC History, Coding, Immo Data" }
            },
            OemNormalLimits = new()
            {
                ["BatteryVoltage"] = (12.0, 14.8, "V"),
                ["CoolantTemp"] = (75.0, 102.0, "°C"),
                ["RailPressure"] = (250.0, 1800.0, "bar"),
                ["BoostPressure"] = (0.9, 2.7, "bar abs")
            }
        });

        // =========================================================================
        // 5. BOSCH MD1CS018 (Mahindra Thar / Scorpio-N / XUV700 2.2 mHawk BS6)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "BOSCH_MD1CS018",
            Name = "Bosch MD1CS018 (Mahindra Thar BS6 / Scorpio-N / XUV700)",
            Manufacturer = "Bosch",
            Category = "BS6 / Euro 6d Common Rail Turbo Diesel with SCR/DPF",
            TypicalPlatforms = new() { "Mahindra Thar 2.2 mHawk BS6", "Mahindra Scorpio-N 2.2 Diesel", "Mahindra XUV700 2.2 Diesel", "Tata Harrier 2.0 BS6 (late)", "Tata Safari 2.0 BS6 (late)" },
            Mcu = McuArchitecture.InfineonTriCore_AURIX_TC3xx,
            McuModel = "TriCore AURIX TC298 / TC399 (Triple 32-bit Core @ 300 MHz)",
            MicroprocessorFamily = "Infineon AURIX Second Generation TC3xx (ASIL-D Automotive)",
            InstructionSet = "TriCore 1.6.2P 32-bit Superscalar Triple-Core",
            CoreClockMhz = 300,
            ExternalFlashChip = "Internal 8 MB PFlash (High-Speed Embedded Program Flash)",
            EepromChip = "512 KB Emulated DFlash (Data Flash EEPROM)",
            FlashSizeBytes = 8388608, // 8 MB Flash
            EepromSizeBytes = 524288,  // 512 KB DFlash
            HardwareNumberPatterns = new() { "0281037110", "0281038225", "0281039400", "0281036890" },
            SoftwareNumberPatterns = new() { "1037612345", "1037628901", "1037639912", "1037645100" },
            CalIdPattern = "1037612345_MD1CS018_THAR_BS6",
            CvnPattern = "0xC1E439A0",
            SupportedProtocols = new() { "UDS on CAN FD (ISO 14229)", "UDS on Classic CAN (ISO 14229)", "ASAM MCD-1 XCP on CAN FD", "OBD-II (ISO 15765-4)" },
            PrimaryDiagnosticProtocol = "UDS (ISO 14229) over CAN FD & Classic CAN",
            DefaultBaudRate = 500000,
            CanAddressingType = "11-bit Standard (Req: 0x7E0, Resp: 0x7E8) & 29-bit CAN FD",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            FunctionalBroadcastCanId = 0x7DF,
            KlineProtocol = "Discontinued (Pure Automotive Ethernet / CAN FD Gateway)",
            SecurityAccessLevels = new()
            {
                ["0x01 / 0x02"] = "Mahindra Service UDS Security Access (AES-128 Challenge-Response)",
                ["0x11 / 0x12"] = "High Security Flash Reprogramming (OEM PKI Certificate Required)"
            },
            BenchPinout = new()
            {
                ["Pin 1, 2"] = "+12V Permanent Battery Power",
                ["Pin 6"] = "+12V Switched Ignition Terminal 15",
                ["Pin 3, 4"] = "Chassis Ground (Terminal 31)",
                ["Pin 48"] = "CAN High (CAN-H 500 kbps / CAN FD)",
                ["Pin 49"] = "CAN Low (CAN-L 500 kbps / CAN FD)"
            },
            ProgrammingMethods = new() { "Bench Service Mode (No Open ECU, High-Speed Bench Harness)", "OBD Virtual Read (Cloud Original Baseline)" },
            ChecksumAlgorithm = "Bosch MD1/MG1 SHA-256 Block Signature with RSA-2048 Hardware Security Module (HSM)",
            KnownConstraints = new()
            {
                "Bosch Hardware Security Module (HSM) actively running secure boot verification",
                "SCR AdBlue dosing and DPF differential pressure closed loop monitoring",
                "Torque security calculation with multi-stage torque monitoring model"
            }
        });

        // =========================================================================
        // 6. CONTINENTAL / SIEMENS PCR 2.1 (VW Polo 1.2/1.6 TDI, Vento, Rapid)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "CONTINENTAL_PCR2_1",
            Name = "Continental / Siemens PCR 2.1 (VAG 1.2 / 1.6 TDI CR)",
            Manufacturer = "Continental / Siemens",
            Category = "Common Rail Turbo Diesel (EA189)",
            TypicalPlatforms = new() { "VW Polo 1.2 TDI / 1.6 TDI CR (6R)", "VW Vento 1.6 TDI CR", "Skoda Rapid 1.6 TDI CR", "Skoda Fabia 1.2 TDI", "Seat Ibiza 1.6 TDI" },
            Mcu = McuArchitecture.InfineonTriCore_TC17xx,
            McuModel = "Infineon TriCore TC1796 (32-bit RISC @ 150 MHz)",
            MicroprocessorFamily = "Infineon AUDO-NG TriCore TC1796",
            InstructionSet = "TriCore 1.3 32-bit RISC",
            CoreClockMhz = 150,
            ExternalFlashChip = "Spansion S29CD016 (2 MB 32-bit Parallel Flash)",
            EepromChip = "ST M95640 / 25640 (64 KB SPI Serial EEPROM)",
            FlashSizeBytes = 2097152, // 2 MB Flash
            EepromSizeBytes = 65536,
            HardwareNumberPatterns = new() { "03L906023", "5WP42890", "5WP42910", "03L906023FS", "03L906023MG" },
            SoftwareNumberPatterns = new() { "CAFR8000", "CAFR8100", "9970", "9971", "9978", "9982" },
            CalIdPattern = "03L906023FS_9970",
            CvnPattern = "0x5E81A204",
            SupportedProtocols = new() { "UDS on ISO-TP (ISO 14229)", "KWP2000 on CAN", "OBD-II (ISO 15765-4 500k 11-bit)" },
            PrimaryDiagnosticProtocol = "UDS (ISO 14229) on ISO-TP (ISO 15765-2)",
            DefaultBaudRate = 500000,
            CanAddressingType = "11-bit Standard (Req: 0x7E0, Resp: 0x7E8)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            FunctionalBroadcastCanId = 0x7DF,
            KlineProtocol = "K-Line Available on Pin 7 for VAG KWP1281 / KWP2000 Diagnostics",
            SecurityAccessLevels = new()
            {
                ["0x01 / 0x02"] = "Standard VAG Diagnostics & Coding (Service 0x27)",
                ["0x11 / 0x12"] = "EEPROM / Bootloader Patch Unlock Access"
            },
            BenchPinout = new()
            {
                ["Pin 1"] = "+12V Permanent Battery Power",
                ["Pin 5"] = "+12V Switched Ignition Terminal 15",
                ["Pin 2"] = "Chassis Ground (Terminal 31)",
                ["Pin 87"] = "CAN High (CAN-H 500 kbps)",
                ["Pin 88"] = "CAN Low (CAN-L 500 kbps)",
                ["Boot Pin"] = "Connect Boot Pin to Ground via 1kΩ resistor while powering on to unlock PCR2.1 MCU on bench"
            },
            ProgrammingMethods = new() { "Bench Unlock via Boot Pin (One-time unlock)", "OBD Virtual Read / OBD Write (Post-unlock)", "Boot Tricore BSL Mode" },
            BootPinDetails = "Two Boot pads on PCB corner - tie via 1kΩ resistor to Ground",
            ChecksumAlgorithm = "Siemens / Continental PCR CRC32 Block & RSA-1024 Signature Bypass",
            KnownConstraints = new()
            {
                "PCR2.1 requires one-time bench boot-pin unlock before OBD write is permitted",
                "Piezo injectors require delicate timing and voltage calibration (200V piezo drive)",
                "Dieselgate software update versions (9970 -> 9978 -> 9982) implement aggressive EGR cycles"
            }
        });

        // =========================================================================
        // 7. CONTINENTAL / SIEMENS SID 807 / SID 807EVO (Ford Figo / EcoSport 1.5)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "CONTINENTAL_SID807",
            Name = "Continental SID 807 / SID 807EVO (Ford Figo / EcoSport 1.5 TDCi)",
            Manufacturer = "Continental",
            Category = "Common Rail Turbo Diesel",
            TypicalPlatforms = new() { "Ford Figo 1.5 TDCi (100PS)", "Ford EcoSport 1.5 TDCi", "Ford Aspire 1.5 TDCi", "Ford Fiesta 1.4/1.5 TDCi", "Peugeot / Citroen 1.6 HDi" },
            Mcu = McuArchitecture.InfineonTriCore_TC17xx,
            McuModel = "Infineon TriCore TC1797 (32-bit RISC @ 180 MHz)",
            MicroprocessorFamily = "Infineon TriCore TC1797 AUDO MAX",
            InstructionSet = "TriCore 1.3.1 32-bit RISC",
            CoreClockMhz = 180,
            ExternalFlashChip = "Internal 4 MB Embedded Flash",
            EepromChip = "64 KB Emulated EEPROM",
            FlashSizeBytes = 4194304, // 4 MB Flash
            EepromSizeBytes = 65536,
            HardwareNumberPatterns = new() { "S180144102", "S180133010", "AV61-12A650", "CV61-12A650" },
            SoftwareNumberPatterns = new() { "EVO807_SW01", "FR3A-14C204", "F1F1-14C204" },
            CalIdPattern = "FR3A-14C204-ANA",
            CvnPattern = "0x981A7F02",
            SupportedProtocols = new() { "UDS on ISO-TP (ISO 14229)", "OBD-II (ISO 15765-4 500k 11-bit)" },
            PrimaryDiagnosticProtocol = "UDS (ISO 14229) on ISO-TP (ISO 15765-2)",
            DefaultBaudRate = 500000,
            CanAddressingType = "11-bit Standard (Req: 0x7E0, Resp: 0x7E8)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            FunctionalBroadcastCanId = 0x7DF,
            BenchPinout = new()
            {
                ["Pin 1"] = "+12V Battery Power",
                ["Pin 3"] = "+12V Ignition Power",
                ["Pin 2"] = "Chassis Ground",
                ["Pin 44"] = "CAN High (CAN-H)",
                ["Pin 45"] = "CAN Low (CAN-L)"
            },
            ProgrammingMethods = new() { "Bench Service Mode", "Boot TriCore BSL Mode", "OBD Virtual Read" },
            ChecksumAlgorithm = "Continental SID Multi-Block 32-bit CRC"
        });

        // =========================================================================
        // 8. DELPHI DCM 3.7AP (Hyundai i20/Verna 1.4 CRDi, Tata Manza Quadrajet)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "DELPHI_DCM3_7",
            Name = "Delphi DCM 3.7AP (Hyundai CRDi / Tata Manza Quadrajet)",
            Manufacturer = "Delphi",
            Category = "Common Rail Turbo Diesel",
            TypicalPlatforms = new() { "Hyundai i20 1.4 CRDi", "Hyundai Verna 1.4 / 1.6 CRDi", "Tata Manza Quadrajet 90PS", "Chevrolet Cruze 2.0 VCDi", "Kia Rio / Ceed 1.4 CRDi" },
            Mcu = McuArchitecture.Renesas_SuperH_SH705x,
            McuModel = "Renesas SuperH SH72513 (32-bit RISC @ 200 MHz)",
            MicroprocessorFamily = "Renesas SH-2A SuperH 32-bit Architecture",
            InstructionSet = "Renesas SuperH 32-bit RISC with Hardware Divider & FPU",
            CoreClockMhz = 200,
            ExternalFlashChip = "Internal 2 MB Flash (SH72513 On-Chip ROM)",
            EepromChip = "ST 25C320 / 95320 (32 KB SPI Serial EEPROM)",
            FlashSizeBytes = 2097152, // 2 MB Flash
            EepromSizeBytes = 32768,
            HardwareNumberPatterns = new() { "28254511", "28267789", "R0411C023A", "28348821" },
            SoftwareNumberPatterns = new() { "U8T4M2B1", "CR4DCM37", "37AP.01", "HYU_DCM37" },
            CalIdPattern = "U8T4M2B1_CR4_CAL",
            CvnPattern = "0x7741C89F",
            SupportedProtocols = new() { "UDS on ISO-TP (ISO 14229)", "KWP2000 on CAN", "OBD-II (ISO 15765-4)" },
            PrimaryDiagnosticProtocol = "UDS (ISO 14229) & KWP2000",
            DefaultBaudRate = 500000,
            CanAddressingType = "11-bit Standard (Req: 0x7E0, Resp: 0x7E8)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            FunctionalBroadcastCanId = 0x7DF,
            BenchPinout = new()
            {
                ["Pin J1-1"] = "+12V Permanent Battery Power",
                ["Pin J1-3"] = "+12V Switched Ignition Terminal 15",
                ["Pin J1-2"] = "Chassis Ground",
                ["Pin J2-15"] = "CAN High (CAN-H 500 kbps)",
                ["Pin J2-16"] = "CAN Low (CAN-L 500 kbps)"
            },
            ProgrammingMethods = new() { "OBD Read/Write", "Bench Service Mode", "JTAG AUD Interface (Renesas SH-2A)" },
            ChecksumAlgorithm = "Delphi SH725xx 32-bit Cyclic Redundancy Check"
        });

        // =========================================================================
        // 9. DENSO SH72544 GD (Toyota Innova Crysta 2.4/2.8 GD, Fortuner 2.8)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "DENSO_SH72544_GD",
            Name = "Denso 1GD / 2GD (Toyota Innova Crysta / Fortuner 2.4 / 2.8 GD)",
            Manufacturer = "Denso",
            Category = "Common Rail Turbo Diesel (GD-Series D-4D with VN Turbo)",
            TypicalPlatforms = new() { "Toyota Innova Crysta 2.4 GD (2GD-FTV)", "Toyota Innova Crysta 2.8 GD (1GD-FTV)", "Toyota Fortuner 2.8 GD (1GD-FTV)", "Toyota Hilux 2.8 GD", "Toyota Land Cruiser Prado 2.8 D-4D" },
            Mcu = McuArchitecture.Renesas_SuperH_SH705x,
            McuModel = "Renesas SuperH SH72544 / RH850 (32-bit RISC @ 200 MHz)",
            MicroprocessorFamily = "Renesas SuperH SH-2A / RH850 Generation",
            InstructionSet = "Renesas SuperH 32-bit RISC High Performance",
            CoreClockMhz = 200,
            ExternalFlashChip = "Internal 2.5 MB / 4 MB Flash (On-Chip Renesas Flash)",
            EepromChip = "32 KB Emulated Data Flash",
            FlashSizeBytes = 2621440, // 2.5 MB Flash
            EepromSizeBytes = 32768,
            HardwareNumberPatterns = new() { "89661-0KG30", "89661-0KM20", "275900-2450", "89661-0KQ10" },
            SoftwareNumberPatterns = new() { "89663-0KG32", "89663-0KM22", "89663-0KQ11" },
            CalIdPattern = "89663-0KG3200",
            CvnPattern = "0x89D204A1",
            SupportedProtocols = new() { "UDS on ISO-TP (ISO 14229)", "Toyota Enhanced Diagnostic Protocol on CAN", "OBD-II (ISO 15765-4 500k 11-bit)" },
            PrimaryDiagnosticProtocol = "UDS (ISO 14229) & Toyota Enhanced OBD-II",
            DefaultBaudRate = 500000,
            CanAddressingType = "11-bit Standard (Req: 0x7E0, Resp: 0x7E8)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            FunctionalBroadcastCanId = 0x7DF,
            BenchPinout = new()
            {
                ["Pin A1"] = "+12V Permanent Battery (+B)",
                ["Pin A3"] = "+12V Switched Ignition (IGSW)",
                ["Pin A2"] = "Battery Negative / Ground (E1)",
                ["Pin B12"] = "CAN High (CAN-H 500 kbps)",
                ["Pin B13"] = "CAN Low (CAN-L 500 kbps)"
            },
            ProgrammingMethods = new() { "OBD Write (CUW / Flash File Update)", "Bench Service Mode", "JTAG / AUD Renesas Interface" },
            ChecksumAlgorithm = "Denso 32-bit Inverted Modular Summation with RSA Authentication"
        });

        // =========================================================================
        // 10. DENSO SH7058 D-4D (Toyota Etios 1.4 D-4D, Innova 2.5 2KD-FTV)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "DENSO_SH7058",
            Name = "Denso SH7058 (Toyota Etios 1.4 D-4D / Innova 2.5 2KD)",
            Manufacturer = "Denso",
            Category = "Common Rail Turbo Diesel (KD / ND Series D-4D)",
            TypicalPlatforms = new() { "Toyota Etios 1.4 D-4D", "Toyota Corolla Altis 1.4 D-4D", "Toyota Innova 2.5 D-4D (2KD-FTV)", "Toyota Fortuner 3.0 D-4D (1KD-FTV)", "Toyota Hilux 2.5 / 3.0 D-4D" },
            Mcu = McuArchitecture.Renesas_SuperH_SH705x,
            McuModel = "Renesas SuperH SH7058 (32-bit RISC @ 80 MHz)",
            MicroprocessorFamily = "Renesas SuperH SH-2 Architecture",
            InstructionSet = "SuperH SH-2 32-bit RISC",
            CoreClockMhz = 80,
            ExternalFlashChip = "Internal 1 MB Flash (SH7058 On-Chip Flash)",
            EepromChip = "ST 93C86 / 25C160 (16 KB Serial EEPROM)",
            FlashSizeBytes = 1048576, // 1 MB Flash
            EepromSizeBytes = 16384,
            HardwareNumberPatterns = new() { "89661-0K040", "89661-02W30", "MB275800-4321", "89661-0K340" },
            SoftwareNumberPatterns = new() { "89663-0K041", "89663-02W31", "89663-0K341" },
            CalIdPattern = "89663-0K04100",
            CvnPattern = "0x2B81F940",
            SupportedProtocols = new() { "ISO 15765-4 OBD-II on CAN", "Toyota Proprietary CAN", "K-Line ISO 14230-4 KWP2000" },
            PrimaryDiagnosticProtocol = "UDS & ISO 15765-4 OBD-II",
            DefaultBaudRate = 500000,
            CanAddressingType = "11-bit Standard (Req: 0x7E0, Resp: 0x7E8)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            FunctionalBroadcastCanId = 0x7DF,
            KlineProtocol = "ISO 14230-4 KWP2000 (10.4 kbps on SIL Pin 7)",
            BenchPinout = new()
            {
                ["Pin E1-1"] = "+12V Permanent Battery (+B)",
                ["Pin E1-2"] = "+12V Switched Ignition (IGSW)",
                ["Pin E1-7"] = "Ground (E1)",
                ["Pin E2-14"] = "CAN High (CAN-H 500 kbps)",
                ["Pin E2-15"] = "CAN Low (CAN-L 500 kbps)",
                ["Pin E2-22"] = "SIL Diagnostic Line (K-Line Pin 7)"
            },
            ProgrammingMethods = new() { "OBD Write", "AUD (Advanced User Debugger) Renesas 14-pin", "Bench Pinout" },
            ChecksumAlgorithm = "Denso 16-bit / 32-bit Inverted Sum"
        });

        // =========================================================================
        // 11. DENSO SUZUKI PETROL (Maruti Swift 1.2 K12M, Baleno 1.2 K12)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "DENSO_SUZUKI_PETROL",
            Name = "Denso 32-bit Suzuki (Swift 1.2 K12M / Baleno 1.2)",
            Manufacturer = "Denso",
            Category = "Naturally Aspirated Gasoline (K-Series VVT)",
            TypicalPlatforms = new() { "Maruti Suzuki Swift 1.2 K12M Petrol", "Maruti Dzire 1.2 Petrol", "Maruti Baleno 1.2 Petrol", "Maruti Ignis 1.2 Petrol", "Maruti WagonR 1.2 K12M" },
            Mcu = McuArchitecture.Renesas_SuperH_SH705x,
            McuModel = "Renesas SuperH SH7058 / SH72543 (32-bit RISC @ 80 MHz)",
            MicroprocessorFamily = "Renesas SuperH SH-2 Architecture",
            InstructionSet = "SuperH 32-bit RISC",
            CoreClockMhz = 80,
            ExternalFlashChip = "Internal 1 MB / 1.5 MB Flash",
            EepromChip = "16 KB SPI EEPROM",
            FlashSizeBytes = 1048576,
            EepromSizeBytes = 16384,
            HardwareNumberPatterns = new() { "33920-68K00", "33920-73P00", "33920-68P10", "33920-55P00" },
            SoftwareNumberPatterns = new() { "8K00", "73P00", "68P10", "55P00" },
            CalIdPattern = "33920-68K00_K12M",
            CvnPattern = "0x44E119B2",
            SupportedProtocols = new() { "UDS on ISO-TP (ISO 14229)", "OBD-II (ISO 15765-4 500k 11-bit)" },
            PrimaryDiagnosticProtocol = "UDS (ISO 14229) over CAN (0x7E0/0x7E8)",
            DefaultBaudRate = 500000,
            CanAddressingType = "11-bit Standard (Req: 0x7E0, Resp: 0x7E8)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            FunctionalBroadcastCanId = 0x7DF,
            BenchPinout = new()
            {
                ["Pin 1"] = "+12V Battery Power",
                ["Pin 5"] = "+12V Ignition Power",
                ["Pin 2"] = "Chassis Ground",
                ["Pin 22"] = "CAN High (CAN-H)",
                ["Pin 23"] = "CAN Low (CAN-L)"
            },
            ProgrammingMethods = new() { "OBD Read/Write", "Bench Service Mode", "AUD Renesas Probe" },
            ChecksumAlgorithm = "Denso Suzuki 32-bit Checksum"
        });

        // =========================================================================
        // 12. BOSCH MED17.5.5 (VAG 1.8 / 2.0 TSI EA888 Gen 1/2, Skoda Laura)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "BOSCH_MED17_5_5",
            Name = "Bosch MED17.5.5 (VAG 1.8 / 2.0 TSI EA888 Gen 1/2)",
            Manufacturer = "Bosch",
            Category = "Gasoline Direct Injection Turbo (TSI / TFSI)",
            TypicalPlatforms = new() { "Skoda Laura 1.8 TSI (Octavia Mk2)", "Skoda Superb 1.8 TSI", "VW Passat 1.8 TSI", "VW Golf 6 GTI 2.0 TSI", "Audi A4 / A5 / Q5 2.0 TFSI" },
            Mcu = McuArchitecture.InfineonTriCore_TC17xx,
            McuModel = "Infineon TriCore TC1766 / TC1767 (32-bit RISC @ 133 MHz)",
            MicroprocessorFamily = "Infineon TriCore AUDO-NG / AUDO-MAX",
            InstructionSet = "TriCore 1.3.1 32-bit RISC",
            CoreClockMhz = 133,
            ExternalFlashChip = "Internal 1.5 MB / 2 MB Flash",
            EepromChip = "32 KB Emulated EEPROM in DFlash",
            FlashSizeBytes = 2097152, // 2 MB Flash
            EepromSizeBytes = 32768,
            HardwareNumberPatterns = new() { "06J906026", "0261S04560", "0261S05820", "06J906027" },
            SoftwareNumberPatterns = new() { "1037398901", "1037505678", "1037512800" },
            CalIdPattern = "06J906026D_505678",
            CvnPattern = "0x8E12F98C",
            SupportedProtocols = new() { "UDS on ISO-TP (ISO 14229)", "OBD-II (ISO 15765-4 500k 11-bit)", "ASAM MCD-1 XCP on CAN" },
            PrimaryDiagnosticProtocol = "UDS (ISO 14229) on ISO-TP (ISO 15765-2)",
            DefaultBaudRate = 500000,
            CanAddressingType = "11-bit Standard (Req: 0x7E0, Resp: 0x7E8)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            FunctionalBroadcastCanId = 0x7DF,
            BenchPinout = new()
            {
                ["Pin 1, 2"] = "+12V Permanent Battery Power",
                ["Pin 5"] = "+12V Switched Ignition Terminal 15",
                ["Pin 3, 4"] = "Chassis Ground",
                ["Pin 67"] = "CAN High (CAN-H 500 kbps)",
                ["Pin 68"] = "CAN Low (CAN-L 500 kbps)",
                ["Boot Pin"] = "TC1766 Boot pin (Pad on PCB rear) to Ground via 1kΩ resistor"
            },
            ProgrammingMethods = new() { "Bench Service Mode (GPT Password Read)", "Boot TriCore BSL Mode", "OBD Virtual Read" },
            ChecksumAlgorithm = "Bosch TriCore CRC32 / Multi-Block Additive Sum",
            MemoryRegions = new()
            {
                new() { RegionName = "Bootloader", StartAddress = 0x80000000, LengthBytes = 65536, IsWritable = false, RequiresSecurityAccess = true, Description = "Internal Bootloader & Hardware Init" },
                new() { RegionName = "Application Software", StartAddress = 0x80010000, LengthBytes = 1048576, IsWritable = true, RequiresSecurityAccess = true, Description = "Operating Engine Management Code" },
                new() { RegionName = "Calibration Maps", StartAddress = 0x80110000, LengthBytes = 524288, IsWritable = true, RequiresSecurityAccess = true, Description = "Fuel, Spark, Boost & Torque Tables" },
                new() { RegionName = "EEPROM Emulation", StartAddress = 0x80190000, LengthBytes = 65536, IsWritable = true, RequiresSecurityAccess = true, Description = "Adaptations, DTC History, Coding, Immo Data" }
            },
            OemNormalLimits = new()
            {
                ["BatteryVoltage"] = (11.8, 14.8, "V"),
                ["CoolantTemp"] = (75.0, 105.0, "°C"),
                ["OilTemp"] = (80.0, 115.0, "°C"),
                ["FuelRailPressure"] = (40.0, 150.0, "bar"),
                ["LambdaTarget"] = (0.82, 1.05, "λ"),
                ["IdleRpm"] = (680.0, 850.0, "RPM")
            }
        });

        // =========================================================================
        // 13. CONTINENTAL / SIEMENS SIMOS 18.1 (VAG 2.0 TSI EA888 Gen 3 MQB)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "SIEMENS_SIMOS18_1",
            Name = "Continental Simos 18.1 / 18.6 (VAG 2.0 TSI EA888 Gen 3)",
            Manufacturer = "Continental / Siemens",
            Category = "Gasoline Direct + Port Dual Injection Turbo (MQB)",
            TypicalPlatforms = new() { "Skoda Octavia vRS 230 / 245 (EA888 Gen 3)", "VW Golf 7 GTI / Golf 7 R", "Audi S3 8V 2.0 TFSI", "Seat Leon Cupra 290/300", "VW Tiguan 2.0 TSI" },
            Mcu = McuArchitecture.InfineonTriCore_AURIX_TC2xx,
            McuModel = "TriCore AURIX TC277 (Triple-Core 32-bit @ 200 MHz)",
            MicroprocessorFamily = "Infineon AURIX First Generation TC2xx (ASIL-D Automotive)",
            InstructionSet = "TriCore 1.6.1P 32-bit Superscalar Triple-Core",
            CoreClockMhz = 200,
            ExternalFlashChip = "Internal 4 MB High-Speed Program Flash",
            EepromChip = "64 KB Emulated Data Flash",
            FlashSizeBytes = 4194304, // 4 MB Flash
            EepromSizeBytes = 65536,
            HardwareNumberPatterns = new() { "5G0906259", "06K907425", "5G0906259A", "5G0906259L" },
            SoftwareNumberPatterns = new() { "SC800G20", "SC800G40", "SC800S50", "0002", "0004" },
            CalIdPattern = "5G0906259A_SC800G40",
            CvnPattern = "0x789A12DF",
            SupportedProtocols = new() { "UDS on ISO-TP (ISO 14229)", "OBD-II (ISO 15765-4 500k 11-bit)", "ASAM MCD-1 XCP on CAN" },
            PrimaryDiagnosticProtocol = "UDS (ISO 14229) on ISO-TP (ISO 15765-2)",
            DefaultBaudRate = 500000,
            CanAddressingType = "11-bit Standard (Req: 0x7E0, Resp: 0x7E8)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            FunctionalBroadcastCanId = 0x7DF,
            BenchPinout = new()
            {
                ["Pin 1, 2"] = "+12V Permanent Battery Power",
                ["Pin 5"] = "+12V Switched Ignition Terminal 15",
                ["Pin 3, 4"] = "Chassis Ground",
                ["Pin 64"] = "CAN High (CAN-H 500 kbps)",
                ["Pin 65"] = "CAN Low (CAN-L 500 kbps)"
            },
            ChecksumAlgorithm = "Simos AURIX SHA-256 + RSA-2048 Digital Signature Verification",
            MemoryRegions = new()
            {
                new() { RegionName = "Bootloader", StartAddress = 0x80000000, LengthBytes = 65536, IsWritable = false, RequiresSecurityAccess = true, Description = "Internal Bootloader & HSM Hardware Security Module" },
                new() { RegionName = "Application Software", StartAddress = 0x80010000, LengthBytes = 2097152, IsWritable = true, RequiresSecurityAccess = true, Description = "Operating Engine Management Code" },
                new() { RegionName = "Calibration Maps", StartAddress = 0x80210000, LengthBytes = 1048576, IsWritable = true, RequiresSecurityAccess = true, Description = "Fuel, Boost, Dual-Injection & Ignition Tables" },
                new() { RegionName = "EEPROM Emulation", StartAddress = 0x80310000, LengthBytes = 65536, IsWritable = true, RequiresSecurityAccess = true, Description = "Adaptations, Learned Octane, DTC History" }
            },
            OemNormalLimits = new()
            {
                ["BatteryVoltage"] = (12.0, 14.7, "V"),
                ["CoolantTemp"] = (80.0, 108.0, "°C"),
                ["HighPressureFuel"] = (100.0, 200.0, "bar"),
                ["LowPressureFuel"] = (4.0, 7.5, "bar"),
                ["BoostPressure"] = (0.9, 2.3, "bar abs")
            }
        });

        // =========================================================================
        // 14. BOSCH ME7.5 (VAG 1.8T 20V, Skoda Octavia vRS Mk1)
        // =========================================================================
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "BOSCH_ME7_5",
            Name = "Bosch ME7.5 / ME7.1 (VAG 1.8T 20V Turbo)",
            Manufacturer = "Bosch",
            Category = "Multi-Port Turbo Gasoline Injection",
            TypicalPlatforms = new() { "Skoda Octavia vRS Mk1 (1.8T 20V 180PS)", "VW Golf 4 GTI 1.8T", "Audi TT 1.8T (180/225PS)", "Audi A4 B5/B6 1.8T", "Seat Leon 1.8T Cupra" },
            Mcu = McuArchitecture.STMicroelectronics_ST10_C167,
            McuModel = "Siemens C167CR / ST10F275 (16-bit CISC @ 25 MHz)",
            MicroprocessorFamily = "Infineon C166 / STMicroelectronics ST10 16-bit",
            InstructionSet = "16-bit CISC Pipeline Architecture",
            CoreClockMhz = 25,
            ExternalFlashChip = "AMD AM29F800BB / AM29F400BT (512 KB / 1 MB Parallel Flash)",
            EepromChip = "ST 95040 (4 KB SPI Serial EEPROM for Immo 3)",
            FlashSizeBytes = 1048576, // 1 MB (1048576 bytes) or 512 KB
            EepromSizeBytes = 4096,
            HardwareNumberPatterns = new() { "06A906032", "0261206890", "0261207900", "06A906032HJ", "06A906032HN" },
            SoftwareNumberPatterns = new() { "1037354100", "1037360200", "1037363908" },
            CalIdPattern = "06A906032HN_0002",
            CvnPattern = "0x4A12B088",
            SupportedProtocols = new() { "KWP1281 on K-Line (ISO 9141-2)", "KWP2000 on K-Line (ISO 14230-4)", "OBD-II (ISO 9141-2 CARBand)" },
            PrimaryDiagnosticProtocol = "KWP2000 & KWP1281 over K-Line (ISO 9141-2)",
            DefaultBaudRate = 10400,
            CanAddressingType = "K-Line Serial Interface (OBD Pin 7)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            KlineProtocol = "ISO 9141-2 5-Baud Address Init (0x33, 0x01, Sync 0x55 @ 10400 baud)",
            BenchPinout = new()
            {
                ["Pin 3, 62"] = "+12V Permanent Battery Power",
                ["Pin 21"] = "+12V Switched Ignition Terminal 15",
                ["Pin 1, 2"] = "Chassis Ground",
                ["Pin 43"] = "K-Line Diagnostic Line",
                ["Boot Pin"] = "Ground Pin 24 of AM29F800BB flash chip via 1kΩ resistor while powering on to enter Boot Mode"
            },
            ProgrammingMethods = new() { "OBD Slow KWP2000 Read/Write", "Bench Boot Mode (Pin 24 Ground)", "Desolder Flash Programmer" },
            BootPinDetails = "Flash Pin 24 (A19 / OE) to Ground via 1kΩ resistor",
            ChecksumAlgorithm = "Bosch ME7 16-bit Additive Summation over 6 blocks"
        });
    }
}
