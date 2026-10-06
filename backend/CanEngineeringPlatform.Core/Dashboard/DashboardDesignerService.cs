using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.Dashboard;

public interface IDashboardDesignerService
{
    IReadOnlyList<DashboardLayout> GetAllLayouts();
    DashboardLayout? GetLayoutById(string id);
    DashboardLayout SaveLayout(DashboardLayout layout);
    bool DeleteLayout(string id);
    DashboardLayout LoadPreset(string presetKey);
}

public class DashboardDesignerService : IDashboardDesignerService
{
    private readonly ConcurrentDictionary<string, DashboardLayout> _layouts = new();

    public DashboardDesignerService()
    {
        InitializePresets();
    }

    public IReadOnlyList<DashboardLayout> GetAllLayouts() => _layouts.Values.OrderBy(l => l.Name).ToList();

    public DashboardLayout? GetLayoutById(string id)
    {
        _layouts.TryGetValue(id, out var layout);
        return layout;
    }

    public DashboardLayout SaveLayout(DashboardLayout layout)
    {
        if (layout == null) throw new ArgumentNullException(nameof(layout));
        if (string.IsNullOrWhiteSpace(layout.Id)) layout.Id = Guid.NewGuid().ToString("N");
        layout.LastModifiedUtc = DateTime.UtcNow;
        _layouts[layout.Id] = layout;
        return layout;
    }

    public bool DeleteLayout(string id)
    {
        return _layouts.TryRemove(id, out _);
    }

    public DashboardLayout LoadPreset(string presetKey)
    {
        var match = _layouts.Values.FirstOrDefault(l => string.Equals(l.PresetKey, presetKey, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // Fallback default
        return _layouts.Values.FirstOrDefault() ?? new DashboardLayout();
    }

    private void InitializePresets()
    {
        // 1. Track Cockpit Cluster
        var trackLayout = new DashboardLayout
        {
            Id = "preset_track_cockpit",
            Name = "Track Cockpit Cluster",
            Description = "High-contrast driver display with tachometer, boost, coolant, lambda, and status lamps",
            PresetKey = "track_cockpit",
            GridColumns = 12,
            Theme = "dark",
            Widgets = new List<DashboardWidget>
            {
                new()
                {
                    Id = "w_tach",
                    Type = DashboardWidgetType.RadialGauge,
                    Title = "ENGINE SPEED",
                    Subtitle = "Crankshaft Tachometer",
                    SourceType = WidgetSignalSource.CanSignal,
                    SourceIdentifier = "EngineSpeed",
                    MinValue = 0,
                    MaxValue = 8000,
                    WarnThreshold = 6500,
                    DangerThreshold = 7200,
                    Unit = "RPM",
                    ColorScheme = "sky",
                    Position = new WidgetGridPosition { X = 0, Y = 0, W = 4, H = 4 }
                },
                new()
                {
                    Id = "w_boost",
                    Type = DashboardWidgetType.RadialGauge,
                    Title = "MANIFOLD BOOST",
                    Subtitle = "Relative Turbo Pressure",
                    SourceType = WidgetSignalSource.CanSignal,
                    SourceIdentifier = "BoostPressure",
                    MinValue = -1.0,
                    MaxValue = 2.5,
                    WarnThreshold = 1.8,
                    DangerThreshold = 2.2,
                    Unit = "bar",
                    ColorScheme = "rose",
                    DecimalPlaces = 2,
                    Position = new WidgetGridPosition { X = 4, Y = 0, W = 4, H = 4 }
                },
                new()
                {
                    Id = "w_speed",
                    Type = DashboardWidgetType.RadialGauge,
                    Title = "VEHICLE SPEED",
                    Subtitle = "Road Velocity",
                    SourceType = WidgetSignalSource.CanSignal,
                    SourceIdentifier = "VehicleSpeed",
                    MinValue = 0,
                    MaxValue = 280,
                    WarnThreshold = 220,
                    DangerThreshold = 260,
                    Unit = "km/h",
                    ColorScheme = "emerald",
                    Position = new WidgetGridPosition { X = 8, Y = 0, W = 4, H = 4 }
                },
                new()
                {
                    Id = "w_lambda",
                    Type = DashboardWidgetType.DigitalValue,
                    Title = "AIR-FUEL LAMBDA",
                    Subtitle = "Bank 1 Wideband O2",
                    SourceType = WidgetSignalSource.CanSignal,
                    SourceIdentifier = "Lambda",
                    MinValue = 0.70,
                    MaxValue = 1.30,
                    WarnThreshold = 1.05,
                    DangerThreshold = 1.15,
                    Unit = "λ",
                    ColorScheme = "amber",
                    DecimalPlaces = 3,
                    Position = new WidgetGridPosition { X = 0, Y = 4, W = 3, H = 2 }
                },
                new()
                {
                    Id = "w_coolant_bar",
                    Type = DashboardWidgetType.LevelBar,
                    Title = "COOLANT TEMP",
                    Subtitle = "Engine Head Sensor",
                    SourceType = WidgetSignalSource.CanSignal,
                    SourceIdentifier = "CoolantTemp",
                    MinValue = 40,
                    MaxValue = 130,
                    WarnThreshold = 105,
                    DangerThreshold = 115,
                    Unit = "°C",
                    ColorScheme = "rose",
                    Position = new WidgetGridPosition { X = 3, Y = 4, W = 3, H = 2 }
                },
                new()
                {
                    Id = "w_trace_spark",
                    Type = DashboardWidgetType.MiniGraph,
                    Title = "BOOST & RPM TREND",
                    Subtitle = "Rolling Telemetry Stream",
                    SourceType = WidgetSignalSource.CanSignal,
                    SourceIdentifier = "BoostPressure",
                    MinValue = 0,
                    MaxValue = 2.5,
                    Unit = "bar",
                    ColorScheme = "sky",
                    Position = new WidgetGridPosition { X = 6, Y = 4, W = 4, H = 2 }
                },
                new()
                {
                    Id = "w_mil_annunciator",
                    Type = DashboardWidgetType.StatusAnnunciator,
                    Title = "CHECK ENGINE",
                    Subtitle = "MIL Diagnostics",
                    SourceType = WidgetSignalSource.ObdPid,
                    SourceIdentifier = "PID_01_MIL",
                    Unit = "State",
                    ColorScheme = "amber",
                    Position = new WidgetGridPosition { X = 10, Y = 4, W = 2, H = 2 }
                }
            }
        };
        _layouts[trackLayout.Id] = trackLayout;

        // 2. Boost & Fuel Diagnostics Dashboard
        var boostFuelLayout = new DashboardLayout
        {
            Id = "preset_boost_fuel",
            Name = "Boost & Fuel Diagnostics",
            Description = "Focused tuner dashboard for logging fuel rail delivery, wastegate duty, and AFR",
            PresetKey = "boost_fuel",
            GridColumns = 12,
            Theme = "dark",
            Widgets = new List<DashboardWidget>
            {
                new()
                {
                    Id = "w_hpfp_gauge",
                    Type = DashboardWidgetType.RadialGauge,
                    Title = "FUEL RAIL PRESSURE",
                    Subtitle = "High Pressure Direct Rail",
                    SourceType = WidgetSignalSource.ManufacturerLiveData,
                    SourceIdentifier = "vag_fuel_rail_act",
                    MinValue = 0,
                    MaxValue = 220,
                    WarnThreshold = 40,
                    DangerThreshold = 25,
                    Unit = "bar",
                    ColorScheme = "amber",
                    DecimalPlaces = 1,
                    Position = new WidgetGridPosition { X = 0, Y = 0, W = 4, H = 4 }
                },
                new()
                {
                    Id = "w_n75_bar",
                    Type = DashboardWidgetType.LevelBar,
                    Title = "WASTEGATE N75 DUTY",
                    Subtitle = "Boost Solenoid PWM",
                    SourceType = WidgetSignalSource.ManufacturerLiveData,
                    SourceIdentifier = "vag_wastegate_n75",
                    MinValue = 0,
                    MaxValue = 100,
                    WarnThreshold = 85,
                    DangerThreshold = 95,
                    Unit = "%",
                    ColorScheme = "rose",
                    Position = new WidgetGridPosition { X = 4, Y = 0, W = 4, H = 4 }
                },
                new()
                {
                    Id = "w_stft_card",
                    Type = DashboardWidgetType.DigitalValue,
                    Title = "SHORT TERM FUEL TRIM",
                    Subtitle = "STFT Bank 1",
                    SourceType = WidgetSignalSource.ObdPid,
                    SourceIdentifier = "PID_06",
                    MinValue = -25,
                    MaxValue = 25,
                    WarnThreshold = 15,
                    DangerThreshold = 22,
                    Unit = "%",
                    ColorScheme = "emerald",
                    DecimalPlaces = 1,
                    Position = new WidgetGridPosition { X = 8, Y = 0, W = 4, H = 2 }
                },
                new()
                {
                    Id = "w_ltft_card",
                    Type = DashboardWidgetType.DigitalValue,
                    Title = "LONG TERM FUEL TRIM",
                    Subtitle = "LTFT Bank 1 Adaptation",
                    SourceType = WidgetSignalSource.ObdPid,
                    SourceIdentifier = "PID_07",
                    MinValue = -25,
                    MaxValue = 25,
                    WarnThreshold = 12,
                    DangerThreshold = 18,
                    Unit = "%",
                    ColorScheme = "sky",
                    DecimalPlaces = 1,
                    Position = new WidgetGridPosition { X = 8, Y = 2, W = 4, H = 2 }
                }
            }
        };
        _layouts[boostFuelLayout.Id] = boostFuelLayout;
    }
}
