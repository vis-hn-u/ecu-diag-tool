using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Diagnostics.Dtc;

public enum DtcSeverity
{
    Minor,       // Informational or non-emissions minor sensor warning
    Moderate,    // Performance slightly degraded, MIL illuminated steady
    Major,       // Severe power degradation, limp home mode active
    Critical     // Immediate engine stop, flashing MIL (catalyst melting/misfire hazard)
}

public enum DtcFaultCategory
{
    FuelSystem,
    IgnitionCombustion,
    AirInductionTurbo,
    EmissionsCatalyst,
    ElectricalSensors,
    TransmissionDriveline,
    NetworkCommunication
}

public class DtcMetadata
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DtcSeverity Severity { get; set; } = DtcSeverity.Moderate;
    public DtcFaultCategory Category { get; set; } = DtcFaultCategory.FuelSystem;
    public string TechnicalExplanation { get; set; } = string.Empty;
    public List<string> CommonSymptoms { get; set; } = new();
    public List<string> LikelyCauses { get; set; } = new();
    public List<string> DiagnosticSteps { get; set; } = new();
    public List<int> AssociatedVagGroups { get; set; } = new();
    public List<string> AssociatedUdsDids { get; set; } = new();
    public Dictionary<string, string> OemCrossReferences { get; set; } = new();
}

public class SignalAnomalyMetric
{
    public string SignalName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double BaselineMean { get; set; }
    public double FaultMomentValue { get; set; }
    public double DeviationZScore { get; set; }
    public double DeviationPct { get; set; }
    public double CorrelationConfidence { get; set; } // 0.0 to 1.0
    public bool IsPrimarySuspect { get; set; }
    public string AnomalyDescription { get; set; } = string.Empty;
}

public class DtcCorrelationResult
{
    public string DtcCode { get; set; } = string.Empty;
    public string DtcTitle { get; set; } = string.Empty;
    public DateTime FaultTimestampUtc { get; set; } = DateTime.UtcNow;
    public double AnalysisWindowSeconds { get; set; } = 10.0; // +/- 5 seconds around fault
    public List<SignalAnomalyMetric> Anomalies { get; set; } = new();
    public string TopSuspectSignal { get; set; } = string.Empty;
    public string RootCauseHypothesis { get; set; } = string.Empty;
}
