using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Diagnostics.EcuInfo;

public enum DataAvailability
{
    Supported,
    NotAvailable,
    Unknown
}

public class EcuIdentification
{
    public string EcuName { get; set; } = string.Empty;
    public uint RequestCanId { get; set; }
    public uint ResponseCanId { get; set; }

    public string? Vin { get; set; }
    public DataAvailability VinStatus { get; set; } = DataAvailability.Unknown;

    public string? HardwareNumber { get; set; }
    public DataAvailability HardwareNumberStatus { get; set; } = DataAvailability.Unknown;

    public string? SoftwareNumber { get; set; }
    public DataAvailability SoftwareNumberStatus { get; set; } = DataAvailability.Unknown;

    public string? SoftwareVersion { get; set; }
    public DataAvailability SoftwareVersionStatus { get; set; } = DataAvailability.Unknown;

    public string? SupplierInfo { get; set; }
    public DataAvailability SupplierInfoStatus { get; set; } = DataAvailability.Unknown;

    public string? BootVersion { get; set; }
    public DataAvailability BootVersionStatus { get; set; } = DataAvailability.Unknown;

    public string? SerialNumber { get; set; }
    public DataAvailability SerialNumberStatus { get; set; } = DataAvailability.Unknown;

    public string? SystemName { get; set; }
    public DataAvailability SystemNameStatus { get; set; } = DataAvailability.Unknown;

    public string? ActiveSession { get; set; }
    public DateTime LastQueriedUtc { get; set; } = DateTime.UtcNow;

    public Dictionary<ushort, string> CustomDids { get; set; } = new();
}
