using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.EcuEngineering.Core;
using CanEngineeringPlatform.Core.EcuEngineering.Identification;

namespace CanEngineeringPlatform.Core.EcuEngineering.Memory;

/// <summary>
/// Verified definition of a discrete ECU memory region with address boundaries,
/// access permissions, and evidence provenance.
/// </summary>
public class MemoryRegion
{
    public string RegionName { get; set; } = string.Empty;
    public long StartAddress { get; set; }
    public long EndAddress { get; set; }
    public long Length => EndAddress >= StartAddress ? (EndAddress - StartAddress + 1) : 0;
    public MemoryType Type { get; set; } = MemoryType.Unknown;
    public AddressWidth AddressWidth { get; set; } = AddressWidth.Width32Bit;

    // Access capabilities
    public ReadPermission ReadCapability { get; set; } = ReadPermission.Unknown;
    public WritePermission WriteCapability { get; set; } = WritePermission.ReadOnly;
    public ErasePermission EraseCapability { get; set; } = ErasePermission.EraseNotSupported;
    public DiagnosticReadMethod PreferredReadMethod { get; set; } = DiagnosticReadMethod.ReadMemoryByAddress_0x23;

    // Diagnostic requirements
    public byte RequiredSession { get; set; } = 0x01; // 0x01 = Default, 0x03 = Extended, 0x02 = Programming
    public byte? RequiredSecurityLevel { get; set; }  // e.g. 0x01 (Seed/Key) or null if unauthenticated
    public int MaxReadLengthBytes { get; set; } = 256;
    public string AddressConstraints { get; set; } = "32-bit linear flat space";
    public string KnownRestrictions { get; set; } = string.Empty;

    // Evidence & Provenance
    public IdentificationConfidence Confidence { get; set; } = IdentificationConfidence.Unknown;
    public MemoryRegionProvenance Provenance { get; set; } = MemoryRegionProvenance.CatalogDerived;
    public List<EvidenceSource> Citations { get; set; } = new();

    public bool ContainsAddress(long address) => address >= StartAddress && address <= EndAddress;
}
