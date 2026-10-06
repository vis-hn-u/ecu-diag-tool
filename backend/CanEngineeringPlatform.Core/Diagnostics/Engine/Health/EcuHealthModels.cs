using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Health;

public record DiagnosticRootCauseCandidate
{
    public string SuspectComponent { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public EvidenceConfidence Confidence { get; init; } = EvidenceConfidence.Possible;
    public List<string> SupportingEvidence { get; init; } = new();
    public List<string> RecommendedPhysicalChecks { get; init; } = new();
}

public class EcuHealthReport
{
    public string EcuName { get; set; } = "ECM";
    public DateTime EvaluatedUtc { get; set; } = DateTime.UtcNow;
    public EcuHealthStatus OverallHealth { get; set; } = EcuHealthStatus.Normal;
    public double HealthScorePercent { get; set; } = 100.0;
    public int ActiveDtcCount { get; set; }
    public int ConfirmedDtcCount { get; set; }
    public int PendingDtcCount { get; set; }
    public int PlausibilityAnomalyCount { get; set; }
    public double SystemVoltage { get; set; } = 13.8;
    public double AverageResponseLatencyMs { get; set; }
    public int TotalNrcCount { get; set; }
    public List<string> HealthObservations { get; set; } = new();
    public List<DiagnosticRootCauseCandidate> RootCauseCandidates { get; set; } = new();
}
