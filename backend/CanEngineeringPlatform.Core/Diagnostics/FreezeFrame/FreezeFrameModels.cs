using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Diagnostics.FreezeFrame;

public class FreezeFrameSensorValue
{
    public string ParameterId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Engine";
    public double RawValue { get; set; }
    public double ScaledValue { get; set; }
    public string FormattedValue { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}

public class DiagnosticFreezeFrame
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DtcCode { get; set; } = string.Empty;
    public byte RecordNumber { get; set; } = 0x00;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public ulong TimestampUs { get; set; }
    public string SourceEcu { get; set; } = "ECM";
    public string Protocol { get; set; } = "UDS 0x19 04"; // "UDS 0x19 04" or "OBD-II Mode 02"
    public byte StatusByte { get; set; }
    public string FaultTriggerReason { get; set; } = "Confirmed DTC Trigger";
    public List<FreezeFrameSensorValue> Sensors { get; set; } = new();

    public double? EngineRpm => Sensors.Find(s => s.Name.Contains("RPM", StringComparison.OrdinalIgnoreCase))?.ScaledValue;
    public double? VehicleSpeedKmh => Sensors.Find(s => s.Name.Contains("Speed", StringComparison.OrdinalIgnoreCase))?.ScaledValue;
    public double? CoolantTempC => Sensors.Find(s => s.Name.Contains("Coolant", StringComparison.OrdinalIgnoreCase))?.ScaledValue;
    public double? EngineLoadPct => Sensors.Find(s => s.Name.Contains("Load", StringComparison.OrdinalIgnoreCase))?.ScaledValue;
}
