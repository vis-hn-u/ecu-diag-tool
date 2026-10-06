using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.LiveEcuData;

public enum LiveParameterCategory
{
    Engine,
    AirTurbo,
    Fuel,
    Egr,
    Dpf,
    ScrAdBlue,
    Nox,
    Thermal,
    Vehicle,
    Transmission,
    Diagnostics,
    Other
}

public enum ParameterProvenance
{
    Dbc,
    ObservedCan,
    DiagnosticResponse,
    Simulation,
    Unknown
}

public class LiveParameter
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public LiveParameterCategory Category { get; set; } = LiveParameterCategory.Other;
    public double Value { get; set; }
    public string FormattedValue { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public ParameterProvenance Provenance { get; set; } = ParameterProvenance.Unknown;
    public uint CanId { get; set; }
    public string MessageName { get; set; } = string.Empty;
    public string SignalName { get; set; } = string.Empty;
    public double UpdateRateHz { get; set; }
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public bool IsPinned { get; set; } = false;
    public double MinObserved { get; set; }
    public double MaxObserved { get; set; }
}

public class RequestedVsActualPair
{
    public string Id { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public LiveParameterCategory Category { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double TargetValue { get; set; }
    public double ActualValue { get; set; }
    public double ErrorDelta => Math.Round(ActualValue - TargetValue, 2);
    public double ErrorPercent => TargetValue != 0 ? Math.Round((ErrorDelta / Math.Abs(TargetValue)) * 100.0, 1) : 0.0;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
