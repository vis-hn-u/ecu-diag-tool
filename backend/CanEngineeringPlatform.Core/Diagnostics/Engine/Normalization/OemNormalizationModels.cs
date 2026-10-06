using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Normalization;

public record OemTargetParameter
{
    public string ParameterName { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public double OemTargetMin { get; init; }
    public double OemTargetMax { get; init; }
    public double MeasuredValue { get; init; }
    public bool IsWithinTarget => MeasuredValue >= OemTargetMin && MeasuredValue <= OemTargetMax;
    public double Deviation => IsWithinTarget ? 0.0 : (MeasuredValue < OemTargetMin ? MeasuredValue - OemTargetMin : MeasuredValue - OemTargetMax);
    public OemTargetSource Source { get; init; } = OemTargetSource.OemDocumented;
    public string Notes { get; init; } = string.Empty;
}

public class OemNormalizationPlan
{
    public string PlanId { get; set; } = Guid.NewGuid().ToString("N");
    public string EcuName { get; set; } = "ECM";
    public string EcuFamily { get; set; } = "Bosch MED17.5";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public bool MeetsAllOemTargets => Discrepancies.Count == 0;
    public List<OemTargetParameter> EvaluatedParameters { get; set; } = new();
    public List<OemTargetParameter> Discrepancies { get; set; } = new();
    public List<string> RecommendedServiceActions { get; set; } = new();
    public string SafetyAttestation { get; set; } = "OEM Normalization preserves original calibration and programming intact. Recommended corrective actions address wiring, sensors, actuators, and mechanical integrity.";
}
