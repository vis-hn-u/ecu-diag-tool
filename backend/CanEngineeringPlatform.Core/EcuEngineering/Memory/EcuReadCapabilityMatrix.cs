using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.EcuEngineering.Memory;

/// <summary>
/// Detailed read privilege specification for a single memory region.
/// </summary>
public class RegionReadCapability
{
    public string RegionName { get; set; } = string.Empty;
    public long StartAddress { get; set; }
    public long LengthBytes { get; set; }
    public DiagnosticReadMethod ReadMethod { get; set; } = DiagnosticReadMethod.ReadMemoryByAddress_0x23;
    public ReadPermission Permission { get; set; } = ReadPermission.Unknown;
    public byte RequiredSession { get; set; } = 0x01;
    public string SessionName { get; set; } = "Default Session (0x01)";
    public bool RequiresSecurityAccess { get; set; }
    public byte? SecurityLevel { get; set; }
    public bool RequiresAuthorization { get; set; } = true;
    public int MaxChunkLengthBytes { get; set; } = 256;
    public string AddressConstraints { get; set; } = string.Empty;
    public string KnownRestrictions { get; set; } = string.Empty;
}

/// <summary>
/// System-wide matrix of read capabilities across all memory regions of an ECU.
/// </summary>
public class EcuReadCapabilityMatrix
{
    public string EcuName { get; set; } = "ECM";
    public bool SupportsReadMemoryByAddress0x23 { get; set; }
    public bool SupportsRequestUpload0x35 { get; set; }
    public bool SupportsReadDataByIdentifier0x22 { get; set; } = true;
    public bool SecurityAccessRequiredOverall { get; set; } = true;
    public List<RegionReadCapability> RegionCapabilities { get; set; } = new();
    public DateTime EvaluatedTimestampUtc { get; set; } = DateTime.UtcNow;
}
