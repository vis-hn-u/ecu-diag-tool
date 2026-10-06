using System;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.EcuEngineering.Compatibility;

public interface IEcuCompatibilityManager
{
    IReadOnlyList<EcuFamilyProfile> GetAllProfiles();
    EcuFamilyProfile? GetProfileById(string familyId);
    EcuFamilyProfile? MatchProfile(string? hardwareNumber, string? softwareNumber, string? ecuName = null);
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
        _profiles.TryGetValue(familyId, out var profile);
        return profile;
    }

    public EcuFamilyProfile? MatchProfile(string? hardwareNumber, string? softwareNumber, string? ecuName = null)
    {
        if (string.IsNullOrWhiteSpace(hardwareNumber) && string.IsNullOrWhiteSpace(softwareNumber))
        {
            if (!string.IsNullOrWhiteSpace(ecuName) && _profiles.TryGetValue(ecuName, out var p))
                return p;
            return null;
        }

        string hw = hardwareNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        string sw = softwareNumber?.Trim().ToUpperInvariant() ?? string.Empty;

        // Pattern-based OEM matching
        if (hw.Contains("MED17") || sw.Contains("MED17") || hw.Contains("06J906026") || sw.Contains("06J906026"))
        {
            return GetProfileById("BOSCH_MED17_5_5");
        }

        if (hw.Contains("EDC17") || sw.Contains("EDC17") || hw.Contains("04L906021") || sw.Contains("04L906021"))
        {
            return GetProfileById("BOSCH_EDC17C64");
        }

        if (hw.Contains("MED9") || sw.Contains("MED9") || hw.Contains("06F906056") || sw.Contains("06F906056"))
        {
            return GetProfileById("BOSCH_MED9_1");
        }

        if (hw.Contains("SIMOS18") || sw.Contains("SIMOS18") || hw.Contains("5G0906259") || sw.Contains("5G0906259"))
        {
            return GetProfileById("SIEMENS_SIMOS18_1");
        }

        if (hw.Contains("PCR2.1") || sw.Contains("PCR2.1") || hw.Contains("03L906023") || sw.Contains("03L906023"))
        {
            return GetProfileById("CONTINENTAL_PCR2_1");
        }

        if (hw.Contains("DCM3") || sw.Contains("DCM3"))
        {
            return GetProfileById("DELPHI_DCM3_7");
        }

        if (hw.Contains("SH7058") || sw.Contains("SH7058") || hw.Contains("DENSO") || sw.Contains("DENSO"))
        {
            return GetProfileById("DENSO_SH7058");
        }

        return null;
    }

    public void RegisterProfile(EcuFamilyProfile profile)
    {
        if (profile == null || string.IsNullOrWhiteSpace(profile.FamilyId)) return;
        _profiles[profile.FamilyId] = profile;
    }

    private void SeedDefaultProfiles()
    {
        // 1. Bosch MED17.5.5
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "BOSCH_MED17_5_5",
            Name = "Bosch MED17.5.5",
            Manufacturer = "Bosch",
            Category = "Gasoline Direct Injection",
            TypicalPlatforms = new() { "VAG 2.0 TSI (EA888 Gen 2)", "Audi A4/A5/Q5", "VW Golf/Passat/Tiguan" },
            Mcu = McuArchitecture.InfineonTriCore_TC17xx,
            McuModel = "TriCore TC1766 / TC1767",
            FlashSizeBytes = 2097152, // 2MB
            EepromSizeBytes = 32768,  // 32KB emulated
            DefaultBaudRate = 500000,
            PrimaryDiagnosticProtocol = "UDS (ISO 14229) on ISO-TP (ISO 15765-2)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            ChecksumAlgorithm = "Bosch TriCore CRC32 / Multi-Block Additive",
            KnownConstraints = new()
            {
                "TPROT (Tuning Protection) active on post-2009 bootloaders",
                "RSA-1024 / RSA-2048 digital signature verification on flash blocks",
                "Monitored torque intervention models"
            },
            RecoveryProcedures = new()
            {
                "Boot-ROM recovery via TC1766 BSL mode",
                "Full flash restore from clean baseline binary",
                "EEPROM synchronization required on ECU replacement"
            },
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

        // 2. Continental / Siemens Simos 18.1
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "SIEMENS_SIMOS18_1",
            Name = "Continental Simos 18.1 / 18.6",
            Manufacturer = "Continental / Siemens",
            Category = "Gasoline Direct + Port Dual Injection",
            TypicalPlatforms = new() { "VAG 2.0 TSI MQB (EA888 Gen 3)", "Audi S3/TTS", "VW Golf 7 GTI/R", "Cupra Leon" },
            Mcu = McuArchitecture.InfineonTriCore_AURIX_TC2xx,
            McuModel = "TriCore AURIX TC277",
            FlashSizeBytes = 4194304, // 4MB
            EepromSizeBytes = 65536,  // 64KB
            DefaultBaudRate = 500000,
            PrimaryDiagnosticProtocol = "UDS (ISO 14229) on ISO-TP (ISO 15765-2)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            ChecksumAlgorithm = "Simos AURIX SHA-256 + RSA Signature Verification",
            KnownConstraints = new()
            {
                "ASIL-D functional safety monitoring",
                "Dual-injection strategy (Direct Injection + Port Fuel Injection)",
                "Electronic wastegate and variable valve lift control"
            },
            RecoveryProcedures = new()
            {
                "JTAG / DAP (Device Access Port) recovery on bench",
                "OEM SGO/FRF container flash recovery via UDS service 0x34/0x36"
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

        // 3. Bosch EDC17C64
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "BOSCH_EDC17C64",
            Name = "Bosch EDC17C64",
            Manufacturer = "Bosch",
            Category = "Common Rail Turbo Diesel",
            TypicalPlatforms = new() { "VAG 1.6 / 2.0 TDI CR (EA288 MQB)", "Golf 7 TDI", "Audi A3 TDI", "Octavia TDI" },
            Mcu = McuArchitecture.InfineonTriCore_TC17xx,
            McuModel = "TriCore TC1797",
            FlashSizeBytes = 4194304, // 4MB
            EepromSizeBytes = 65536,
            DefaultBaudRate = 500000,
            PrimaryDiagnosticProtocol = "UDS (ISO 14229)",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            ChecksumAlgorithm = "Bosch TriCore CRC32 / Additive",
            KnownConstraints = new()
            {
                "Strict DPF soot model and SCR AdBlue metering validation",
                "Multi-pulse piezo/solenoid injection quantity balance",
                "Exhaust gas temperature (EGT) thermal protection triggers"
            },
            RecoveryProcedures = new()
            {
                "Bench BSL / Boot mode with TC1797 boot pin resistor",
                "Full flash restore via verified OEM calibration map"
            }
        });

        // 4. Bosch MED9.1
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "BOSCH_MED9_1",
            Name = "Bosch MED9.1",
            Manufacturer = "Bosch",
            Category = "Gasoline Direct Injection",
            TypicalPlatforms = new() { "VAG 2.0 TFSI (EA113)", "Golf 5 GTI / Edition 30", "Audi S3 8P", "Leon Cupra R" },
            Mcu = McuArchitecture.NxpPowerPc_MPC55xx,
            McuModel = "MPC562 / MPC564",
            FlashSizeBytes = 2097152, // 2MB
            DefaultBaudRate = 500000,
            PrimaryDiagnosticProtocol = "KWP2000 (ISO 14230-4) & UDS on CAN",
            DefaultRequestCanId = 0x7E0,
            DefaultResponseCanId = 0x7E8,
            ChecksumAlgorithm = "Bosch MED9 MPC5xx Additive 16-bit / 32-bit",
            KnownConstraints = new() { "KWP2000 TP2.0 channel setup required on older gateway revisions" }
        });

        // 5. Continental PCR2.1
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "CONTINENTAL_PCR2_1",
            Name = "Continental PCR2.1",
            Manufacturer = "Continental",
            Category = "Common Rail Turbo Diesel",
            TypicalPlatforms = new() { "VAG 1.6 TDI (EA189)", "Polo 6R", "Golf 6", "Ibiza", "Fabia" },
            Mcu = McuArchitecture.InfineonTriCore_TC17xx,
            McuModel = "TriCore TC1796",
            FlashSizeBytes = 2097152,
            PrimaryDiagnosticProtocol = "UDS (ISO 14229)",
            ChecksumAlgorithm = "Siemens PCR CRC32 Block"
        });

        // 6. Delphi DCM3.7
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "DELPHI_DCM3_7",
            Name = "Delphi DCM3.7",
            Manufacturer = "Delphi",
            Category = "Common Rail Turbo Diesel",
            TypicalPlatforms = new() { "VAG 1.2 TDI", "Hyundai / Kia 1.4 / 1.6 CRDi", "Chevrolet Cruze" },
            Mcu = McuArchitecture.Renesas_SuperH_SH705x,
            McuModel = "Renesas SH72513",
            FlashSizeBytes = 2097152,
            PrimaryDiagnosticProtocol = "UDS (ISO 14229) & KWP2000",
            ChecksumAlgorithm = "Delphi SH725xx CRC32"
        });

        // 7. Denso SH7058
        RegisterProfile(new EcuFamilyProfile
        {
            FamilyId = "DENSO_SH7058",
            Name = "Denso SH7058 Engine Control",
            Manufacturer = "Denso",
            Category = "Gasoline / Diesel Multi-Port & Direct",
            TypicalPlatforms = new() { "Toyota / Lexus D-4S", "Subaru FA20 / EJ25", "Nissan Diesel" },
            Mcu = McuArchitecture.Renesas_SuperH_SH705x,
            McuModel = "Renesas SH7058",
            FlashSizeBytes = 1048576, // 1MB
            PrimaryDiagnosticProtocol = "UDS & ISO 15765-4 OBD-II",
            ChecksumAlgorithm = "Denso 16-bit / 32-bit Inverted Sum"
        });
    }
}
