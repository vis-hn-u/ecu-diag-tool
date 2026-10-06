using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Dashboard;

public enum DashboardWidgetType
{
    RadialGauge,       // Circular analog-style needle sweep gauge
    DigitalValue,      // High-contrast large digital readout with min/max hold
    MiniGraph,         // Rolling strip-chart time-series sparkline
    LevelBar,          // Linear horizontal or vertical graduated level bar
    StatusAnnunciator  // Automotive telltale warning lamp / status tile
}

public enum WidgetSignalSource
{
    CanSignal,             // DBC Decoded CAN Signal
    ObdPid,                // Standard OBD-II Parameter ID
    UdsDid,                // UDS Data Identifier
    VagMeasuringBlock,     // VAG 4-Zone Measuring Block
    ManufacturerLiveData,  // OEM-Specific Live Data
    LiveParameter,         // Aggregated Live ECU Parameter
    CalculatedSignal       // Custom Expression Engine Signal
}

public class WidgetGridPosition
{
    public int X { get; set; } = 0;
    public int Y { get; set; } = 0;
    public int W { get; set; } = 4; // Out of 12 columns
    public int H { get; set; } = 3;
}

public class DashboardWidget
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DashboardWidgetType Type { get; set; } = DashboardWidgetType.RadialGauge;
    public string Title { get; set; } = "Engine Speed";
    public string Subtitle { get; set; } = "Tachometer";
    public WidgetSignalSource SourceType { get; set; } = WidgetSignalSource.CanSignal;
    public string SourceIdentifier { get; set; } = "EngineSpeed";
    public double MinValue { get; set; } = 0;
    public double MaxValue { get; set; } = 8000;
    public double? WarnThreshold { get; set; } = 6500;
    public double? DangerThreshold { get; set; } = 7200;
    public string Unit { get; set; } = "RPM";
    public string ColorScheme { get; set; } = "sky"; // "sky", "emerald", "amber", "rose", "purple"
    public int DecimalPlaces { get; set; } = 0;
    public WidgetGridPosition Position { get; set; } = new();
}

public class DashboardLayout
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Track Cockpit Cluster";
    public string Description { get; set; } = "High-contrast driver display with tachometer, boost, oil temp, and lambda";
    public string PresetKey { get; set; } = "track_cockpit";
    public int GridColumns { get; set; } = 12;
    public string Theme { get; set; } = "dark";
    public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
    public List<DashboardWidget> Widgets { get; set; } = new();
}
