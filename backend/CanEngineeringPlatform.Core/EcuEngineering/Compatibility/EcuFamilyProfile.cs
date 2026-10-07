using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.EcuEngineering.Compatibility;

/// <summary>
/// Verified OEM specification and architectural profile for an ECU family.
/// </summary>
public class EcuFamilyProfile
{
    public string FamilyId { get; set; } = string.Empty;              // e.g. "BOSCH_MED17_5_5"
    public string Name { get; set; } = string.Empty;                  // e.g. "Bosch MED17.5.5"
    public string Manufacturer { get; set; } = "Bosch";               // Bosch, Continental, Delphi, Denso, Magneti Marelli
    public string Category { get; set; } = "Gasoline Direct Injection"; // Gasoline, Diesel, Hybrid, Transmission
    public List<string> TypicalPlatforms { get; set; } = new();       // VAG (VW/Audi/Seat/Skoda) EA888 Gen 1/2
    public McuArchitecture Mcu { get; set; } = McuArchitecture.InfineonTriCore_TC17xx;
    public string McuModel { get; set; } = "TC1766 / TC1767";

    // Memory Architecture
    public long FlashSizeBytes { get; set; } = 2097152;               // 2 MB (2,097,152 bytes)
    public long EepromSizeBytes { get; set; } = 32768;                // 32 KB Emulated / Serial EEPROM
    public List<MemoryRegionDefinition> MemoryRegions { get; set; } = new();

    // Communication Defaults
    public uint DefaultBaudRate { get; set; } = 500000;
    public string PrimaryDiagnosticProtocol { get; set; } = "UDS (ISO 14229 on ISO-TP)";
    public uint DefaultRequestCanId { get; set; } = 0x7E0;
    public uint DefaultResponseCanId { get; set; } = 0x7E8;

    // Safety & Checksum
    public string ChecksumAlgorithm { get; set; } = "Bosch TriCore CRC32 / Additive Block";
    public List<string> KnownConstraints { get; set; } = new();
    public List<string> RecoveryProcedures { get; set; } = new();

    // Identification & Number Patterns
    public List<string> HardwareNumberPatterns { get; set; } = new();
    public List<string> SoftwareNumberPatterns { get; set; } = new();
    public string CalIdPattern { get; set; } = string.Empty;
    public string CvnPattern { get; set; } = string.Empty;

    // Microprocessor & Architecture Specs
    public string MicroprocessorFamily { get; set; } = string.Empty;
    public string InstructionSet { get; set; } = string.Empty;
    public int CoreClockMhz { get; set; }
    public string ExternalFlashChip { get; set; } = string.Empty;
    public string EepromChip { get; set; } = string.Empty;

    // Protocol & Network Specs
    public List<string> SupportedProtocols { get; set; } = new();
    public string CanAddressingType { get; set; } = "11-bit Standard (0x7E0 / 0x7E8)";
    public uint FunctionalBroadcastCanId { get; set; } = 0x7DF;
    public string KlineProtocol { get; set; } = "ISO 14230-4 KWP2000 Fast Init (10.4 kbps)";
    public Dictionary<string, string> SecurityAccessLevels { get; set; } = new();

    // Bench / Boot / Pinout & Programming Specs
    public Dictionary<string, string> BenchPinout { get; set; } = new();
    public List<string> ProgrammingMethods { get; set; } = new();
    public string BootPinDetails { get; set; } = string.Empty;
    public string BdmJtagDetails { get; set; } = string.Empty;

    // OEM Operational Reference Targets
    public Dictionary<string, (double Min, double Max, string Unit)> OemNormalLimits { get; set; } = new();

    // Release & Provenance Hardening
    public EvidenceState Evidence { get; set; } = EvidenceState.DatabaseMatch;
    public ReleaseValidationState ReleaseValidation { get; set; } = ReleaseValidationState.DatabaseSourceVerified;
    public PinoutValidationState PinoutValidation { get; set; } = PinoutValidationState.Reference;
    public bool ConflictingPinoutVariantsFound { get; set; } = false;
    public string? ConflictNotice { get; set; }
    public string SourceProvenance { get; set; } = "OEM Service Manual / Public Documentation";

    // Variant-Level Records (ECU Family != ECU Variant)
    public List<EcuVariantProfile> Variants { get; set; } = new();
}

public enum EvidenceState
{
    Confirmed,       // Read and validated directly from connected ECU
    Detected,        // Detected via active bus scanning / ISO-TP responses
    DatabaseMatch,   // Matched by verified hardware or software number pattern
    Inferred,        // Deduced from model/engine family without exact part number match
    Unknown          // Not determined / no source evidence
}

public enum ReleaseValidationState
{
    SoftwareVerified,         // Unit/integration tests pass with 100% assertions
    SimulationVerified,       // Verified in software bench / simulated CAN environment
    DatabasePresent,          // Profile definition exists in local catalog
    DatabaseSourceVerified,   // Sourced and cross-checked against OEM specifications
    Inferred,                 // Generic or heuristic match
    NotValidated,             // Not verified on bench or hardware
    HardwareRequired          // Requires physical ECU & microcontroller connection
}

public enum PinoutValidationState
{
    Confirmed,         // Bench wiring validated with physical harness
    Reference,         // Cross-checked against OEM wiring diagrams & tuner pinout documentation
    VariantDependent,  // Multiple connector / pin configurations exist for this ECU family
    Unverified         // Reference pinout only - do not apply power without checking pin voltage
}

public class EcuVariantProfile
{
    public string VariantId { get; set; } = string.Empty;
    public string VariantName { get; set; } = string.Empty;
    public string HardwareNumber { get; set; } = string.Empty;
    public string SoftwareNumber { get; set; } = string.Empty;
    public string ConnectorDesignation { get; set; } = string.Empty;
    public string VehiclePlatform { get; set; } = string.Empty;
    public string McuModel { get; set; } = string.Empty;
    public Dictionary<string, string> BenchPinout { get; set; } = new();
    public PinoutValidationState PinoutValidation { get; set; } = PinoutValidationState.Reference;
    public EvidenceState Evidence { get; set; } = EvidenceState.DatabaseMatch;
    public string SourceReference { get; set; } = string.Empty;
}

public class MemoryRegionDefinition
{
    public string RegionName { get; set; } = "Internal Flash"; // Bootloader, Application, Calibration, EEPROM
    public long StartAddress { get; set; }
    public long LengthBytes { get; set; }
    public bool IsWritable { get; set; }
    public bool RequiresSecurityAccess { get; set; }
    public string Description { get; set; } = string.Empty;
}
