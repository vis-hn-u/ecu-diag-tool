using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Diagnostics.Obd;

public enum ObdMode : byte
{
    CurrentData = 0x01,
    FreezeFrame = 0x02,
    StoredDTCs = 0x03,
    ClearDTCs = 0x04,
    OxygenSensors = 0x05,
    OnBoardMonitoring = 0x06,
    PendingDTCs = 0x07,
    ControlOperation = 0x08,
    VehicleInformation = 0x09
}

public class ObdPidDefinition
{
    public byte Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public byte ReturnBytes { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public Func<byte[], double>? Decoder { get; set; }
    public Func<byte[], string>? Formatter { get; set; }
}

public class ObdPidValue
{
    public byte Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public double? Value { get; set; }
    public string FormattedValue { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public byte[] RawBytes { get; set; } = Array.Empty<byte>();
    public bool IsSupported { get; set; } = true;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public class ObdResponse
{
    public bool IsPositive { get; set; }
    public ObdMode Mode { get; set; }
    public byte? Pid { get; set; }
    public List<ObdPidValue> PidValues { get; set; } = new();
    public List<string> DtcCodes { get; set; } = new();
    public string? VehicleInfoValue { get; set; }
    public byte[] RawData { get; set; } = Array.Empty<byte>();
    public string DecodedSummary { get; set; } = string.Empty;
}
