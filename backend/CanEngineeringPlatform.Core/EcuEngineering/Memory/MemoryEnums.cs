using System;

namespace CanEngineeringPlatform.Core.EcuEngineering.Memory;

/// <summary>
/// Major functional categories of ECU memory.
/// </summary>
public enum MemoryType
{
    Bootloader,
    Application,
    Calibration,
    Eeprom,
    Nvm,
    Configuration,
    Data,
    Reserved,
    Unknown
}

/// <summary>
/// Bit-width of the memory addressing scheme.
/// </summary>
public enum AddressWidth
{
    Width16Bit = 2,
    Width24Bit = 3,
    Width32Bit = 4,
    Width64Bit = 8
}

/// <summary>
/// Read capability and diagnostic privilege classification for a memory region.
/// </summary>
public enum ReadPermission
{
    ReadSupported,
    ReadNotSupported,
    RequiresDiagnosticSession,
    RequiresAuthorization,
    RequiresSecurityAccess,
    RequiresBenchInterface,
    RequiresSpecialHardware,
    Unknown
}

/// <summary>
/// Write capability classification.
/// </summary>
public enum WritePermission
{
    WriteSupported,
    WriteNotSupported,
    RequiresSecurityAccess,
    RequiresBenchInterface,
    ReadOnly
}

/// <summary>
/// Erase capability classification.
/// </summary>
public enum ErasePermission
{
    EraseSupported,
    EraseNotSupported,
    RequiresSecurityAccess,
    Forbidden
}

/// <summary>
/// Diagnostic read mechanism used to extract memory.
/// </summary>
public enum DiagnosticReadMethod
{
    ReadMemoryByAddress_0x23,
    RequestUpload_0x35,
    ReadDataByIdentifier_0x22,
    VendorSpecific,
    None,
    Unknown
}

/// <summary>
/// Strict lifecycle states for the Safe Read Session state machine.
/// </summary>
public enum ReadSessionState
{
    Disconnected,
    Connected,
    Identified,
    ProtocolConfirmed,
    DiagnosticSessionActive,
    ReadAuthorized,
    Reading,
    Verifying,
    Complete,
    Failed
}

/// <summary>
/// Execution environment in which diagnostics/reads are occurring.
/// </summary>
public enum ExecutionEnvironment
{
    Bench,
    Vehicle,
    Simulation
}

/// <summary>
/// Classification of a stored memory image to guarantee version control and immutability.
/// </summary>
public enum ImageClassification
{
    OriginalRead,
    Baseline,
    WorkingCopy,
    Modified,
    Verified,
    Backup
}

/// <summary>
/// Confidence classification for discovered checksum regions.
/// </summary>
public enum ChecksumClassification
{
    Confirmed,
    Candidate,
    Unverified,
    Unknown
}

/// <summary>
/// Clear provenance source for memory layout definitions.
/// </summary>
public enum MemoryRegionProvenance
{
    EcuObserved,
    CatalogDerived,
    UserProvided,
    Inferred
}
