using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Plausibility;

public record SensorPlausibilityCheckResult
{
    public string CheckName { get; init; } = string.Empty;
    public string Category { get; init; } = "Engine Dynamics";
    public SensorPlausibilityState State { get; init; } = SensorPlausibilityState.Unknown;
    public string Summary { get; init; } = string.Empty;
    public string DetailedEvidence { get; init; } = string.Empty;
    public List<string> InvolvedSignals { get; init; } = new();
    public Dictionary<string, double> MeasuredValues { get; init; } = new();
    public DateTime EvaluatedUtc { get; init; } = DateTime.UtcNow;
}

public class SensorPlausibilityReport
{
    public string EcuName { get; set; } = "ECM";
    public DateTime EvaluatedUtc { get; set; } = DateTime.UtcNow;
    public SensorPlausibilityState OverallState { get; set; } = SensorPlausibilityState.Unknown;
    public int TotalChecksCount => Checks.Count;
    public int PlausibleCount => Checks.Count(c => c.State == SensorPlausibilityState.Plausible);
    public int AnomalyCount => Checks.Count(c => c.State == SensorPlausibilityState.Questionable || c.State == SensorPlausibilityState.OutOfRange || c.State == SensorPlausibilityState.Inconsistent);
    public List<SensorPlausibilityCheckResult> Checks { get; set; } = new();
}
