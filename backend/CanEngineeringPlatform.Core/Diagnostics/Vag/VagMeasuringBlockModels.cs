using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Diagnostics.Vag;

public enum VagFormulaType : byte
{
    Rpm = 0x01,                  // 0.2 * A * B or A * B / 5
    AbsoluteLoad = 0x02,         // A * 100 / 255 (%)
    ThrottleAngle = 0x03,        // A * 0.392 (%)
    Temperature = 0x05,          // 0.75 * A - 48 (°C)
    Voltage = 0x06,              // 0.001 * A * B (V)
    Speed = 0x07,                // 0.01 * A * B (km/h)
    AirFlow = 0x0A,              // A * 0.01 * B (g/s)
    TimingAngle = 0x14,          // 0.75 * (A - 64) (°BTDC)
    VoltageFine = 0x19,          // 0.02 * A * B or 0.091 * A (V)
    PressureMbar = 0x24,         // 0.1 * A * B (mbar)
    DutyCycle = 0x25,            // A * 100 / 255 (%)
    LambdaRatio = 0x28,          // 0.001 * A * B (λ)
    MassAirFlow = 0x2E,          // 0.01 * A * B (g/s)
    Counter = 0x34,              // A * 256 + B (Count)
    BinaryStatus = 0x3C,         // 8-bit status flags
    Custom = 0xFF
}

public class VagMeasuringBlockZone
{
    public int ZoneNumber { get; set; } // 1, 2, 3, or 4
    public string Label { get; set; } = string.Empty;
    public VagFormulaType FormulaType { get; set; } = VagFormulaType.Custom;
    public double RawA { get; set; }
    public double RawB { get; set; }
    public double Value { get; set; }
    public string FormattedValue { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double? MinNominal { get; set; }
    public double? MaxNominal { get; set; }
    public bool IsOutOfRange => (MinNominal.HasValue && Value < MinNominal.Value) || 
                                (MaxNominal.HasValue && Value > MaxNominal.Value);
}

public class VagMeasuringGroup
{
    public int GroupNumber { get; set; }
    public string GroupKey => $"Group {GroupNumber:D3}";
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EcuSubsystem { get; set; } = "Engine ECM (01)";
    public List<VagMeasuringBlockZone> Zones { get; set; } = new();
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
