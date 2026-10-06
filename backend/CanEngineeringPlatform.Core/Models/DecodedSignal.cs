using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Models;

public class DecodedSignal
{
    public string SignalName { get; set; } = string.Empty;
    public string MessageName { get; set; } = string.Empty;
    public uint CanId { get; set; }
    public double RawValue { get; set; }
    public double EngineeringValue { get; set; }
    public string FormattedValue => $"{EngineeringValue:0.##} {Unit}".Trim();
    public string Unit { get; set; } = string.Empty;
    public uint TimestampUs { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string TransmitterNode { get; set; } = string.Empty;
    public List<string> ReceiverNodes { get; set; } = new();
    public string? ValueDescription { get; set; }
}
