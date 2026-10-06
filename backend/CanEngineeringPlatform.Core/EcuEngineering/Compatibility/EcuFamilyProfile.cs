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

    // OEM Operational Reference Targets
    public Dictionary<string, (double Min, double Max, string Unit)> OemNormalLimits { get; set; } = new();
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
