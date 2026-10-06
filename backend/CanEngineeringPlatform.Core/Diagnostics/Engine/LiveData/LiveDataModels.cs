using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.LiveData;

public enum LiveDataProtocolSource
{
    UdsDid,
    ObdPid,
    CanSignal,
    Calculated
}

public class LiveDataParameterDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Engine";
    public LiveDataProtocolSource Source { get; set; } = LiveDataProtocolSource.UdsDid;
    public ushort? DataIdentifier { get; set; }
    public byte? ObdPid { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double Scaling { get; set; } = 1.0;
    public double Offset { get; set; } = 0.0;
    public double MinExpectedValue { get; set; } = double.MinValue;
    public double MaxExpectedValue { get; set; } = double.MaxValue;
    public double? MaxAllowableJumpPerSecond { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class LiveDataSample
{
    public string ParameterId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public byte[] RawBytes { get; set; } = Array.Empty<byte>();
    public double RawValue { get; set; }
    public double ScaledValue { get; set; }
    public string FormattedValue { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public DataQuality Quality { get; set; } = DataQuality.Valid;
    public string? QualityReason { get; set; }
    public LiveDataProtocolSource Source { get; set; }
    public string EcuName { get; set; } = "ECM";
}
