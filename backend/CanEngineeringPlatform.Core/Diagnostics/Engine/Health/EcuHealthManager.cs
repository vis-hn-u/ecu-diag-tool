using System;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.Engine.LiveData;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Plausibility;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Trace;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Health;

public interface IEcuHealthManager
{
    EcuHealthReport EvaluateHealth(string ecuName = "ECM");
}

public class EcuHealthManager : IEcuHealthManager
{
    private readonly IDtcService _dtcService;
    private readonly ILiveDataService _liveDataService;
    private readonly ISensorPlausibilityService _plausibilityService;
    private readonly IDiagnosticTraceService _traceService;

    public EcuHealthManager(
        IDtcService dtcService,
        ILiveDataService liveDataService,
        ISensorPlausibilityService plausibilityService,
        IDiagnosticTraceService traceService)
    {
        _dtcService = dtcService ?? throw new ArgumentNullException(nameof(dtcService));
        _liveDataService = liveDataService ?? throw new ArgumentNullException(nameof(liveDataService));
        _plausibilityService = plausibilityService ?? throw new ArgumentNullException(nameof(plausibilityService));
        _traceService = traceService ?? throw new ArgumentNullException(nameof(traceService));
    }

    public EcuHealthReport EvaluateHealth(string ecuName = "ECM")
    {
        var activeDtcs = _dtcService.GetActiveDtcs(ecuName);
        var plausibility = _plausibilityService.EvaluatePlausibility(ecuName);
        var liveSamples = _liveDataService.GetLatestSamples();
        var traces = _traceService.GetRecentTraces(200);

        var report = new EcuHealthReport
        {
            EcuName = ecuName,
            EvaluatedUtc = DateTime.UtcNow,
            ActiveDtcCount = activeDtcs.Count,
            ConfirmedDtcCount = activeDtcs.Count(d => d.ConfirmedDtc),
            PendingDtcCount = activeDtcs.Count(d => d.PendingDtc),
            PlausibilityAnomalyCount = plausibility.AnomalyCount
        };

        // Voltage
        var voltSample = liveSamples.FirstOrDefault(s => s.ParameterId.Equals("MODULE_VOLTAGE", StringComparison.OrdinalIgnoreCase));
        if (voltSample != null)
        {
            report.SystemVoltage = voltSample.ScaledValue;
        }

        // Trace Stats
        if (traces.Count > 0)
        {
            report.AverageResponseLatencyMs = traces.Average(t => t.LatencyMs);
            report.TotalNrcCount = traces.Count(t => t.Result == DiagnosticExchangeResult.NegativeResponse);
        }

        // Compute Health Score & Status
        double score = 100.0;

        // DTC penalties
        score -= report.ConfirmedDtcCount * 25.0;
        score -= report.PendingDtcCount * 10.0;

        // Plausibility penalties
        score -= plausibility.AnomalyCount * 15.0;

        // Trace penalties (NRCs)
        if (report.TotalNrcCount > 5)
        {
            score -= 10.0;
        }

        if (report.SystemVoltage < 11.5 || report.SystemVoltage > 15.5)
        {
            score -= 20.0;
        }

        report.HealthScorePercent = Math.Clamp(score, 0.0, 100.0);

        if (report.ConfirmedDtcCount > 0 || score < 50.0)
        {
            report.OverallHealth = EcuHealthStatus.Fault;
        }
        else if (report.PendingDtcCount > 0 || plausibility.AnomalyCount > 0 || score < 85.0)
        {
            report.OverallHealth = EcuHealthStatus.Warning;
        }
        else
        {
            report.OverallHealth = EcuHealthStatus.Normal;
        }

        // Populate Observations
        if (report.ConfirmedDtcCount > 0)
        {
            report.HealthObservations.Add($"{report.ConfirmedDtcCount} confirmed DTC(s) detected: {string.Join(", ", activeDtcs.Where(d => d.ConfirmedDtc).Select(d => d.Code))}.");
        }
        if (report.PendingDtcCount > 0)
        {
            report.HealthObservations.Add($"{report.PendingDtcCount} pending fault(s) detected undergoing drive-cycle validation.");
        }
        if (plausibility.AnomalyCount > 0)
        {
            report.HealthObservations.Add($"{plausibility.AnomalyCount} sensor plausibility anomalies identified during multi-signal correlation.");
        }
        if (report.SystemVoltage < 12.0)
        {
            report.HealthObservations.Add($"Low supply voltage observed ({report.SystemVoltage:F2}V). Ensure battery charger is connected.");
        }

        // Correlate Evidence into Root Cause Candidates
        report.RootCauseCandidates = CorrelateRootCauses(activeDtcs, plausibility, liveSamples);

        return report;
    }

    private List<DiagnosticRootCauseCandidate> CorrelateRootCauses(
        IReadOnlyList<DtcRecord> dtcs,
        SensorPlausibilityReport plausibility,
        IReadOnlyList<LiveDataSample> liveSamples)
    {
        var candidates = new List<DiagnosticRootCauseCandidate>();

        // 1. Air Induction / Vacuum Leak Correlation
        bool hasLeanDtc = dtcs.Any(d => d.Code.StartsWith("P0171") || d.Code.StartsWith("P0174"));
        var combustionCheck = plausibility.Checks.FirstOrDefault(c => c.Category == "Combustion");
        var airflowCheck = plausibility.Checks.FirstOrDefault(c => c.Category == "Air / Induction");

        if (hasLeanDtc || (combustionCheck != null && combustionCheck.State == SensorPlausibilityState.Inconsistent))
        {
            var evidence = new List<string>();
            if (hasLeanDtc) evidence.Add("DTC P0171 / P0174 (System Too Lean) present in ECU memory.");
            if (combustionCheck != null && combustionCheck.State == SensorPlausibilityState.Inconsistent)
                evidence.Add($"Combustion check anomaly: {combustionCheck.Summary}");

            candidates.Add(new DiagnosticRootCauseCandidate
            {
                SuspectComponent = "Intake Manifold / PCV / Vacuum Hoses",
                Description = "Unmetered false air entering intake tract downstream of mass air flow sensor.",
                Confidence = hasLeanDtc && (combustionCheck?.State == SensorPlausibilityState.Inconsistent) ? EvidenceConfidence.Confirmed : EvidenceConfidence.Likely,
                SupportingEvidence = evidence,
                RecommendedPhysicalChecks = new()
                {
                    "Perform smoke test on intake manifold and PCV diaphragm.",
                    "Inspect brake booster vacuum hose and check valve for cracks.",
                    "Verify MAF sensor O-ring and throttle body gasket seating."
                }
            });
        }

        // 2. Misfire Correlation
        var misfireDtcs = dtcs.Where(d => d.Code.StartsWith("P030")).ToList();
        if (misfireDtcs.Count > 0)
        {
            candidates.Add(new DiagnosticRootCauseCandidate
            {
                SuspectComponent = "Ignition Coils / Spark Plugs / Fuel Injectors",
                Description = "Cylinder misfire event triggered by combustion failure.",
                Confidence = EvidenceConfidence.Confirmed,
                SupportingEvidence = misfireDtcs.Select(d => $"DTC {d.Code}: {d.Description}").ToList(),
                RecommendedPhysicalChecks = new()
                {
                    "Inspect spark plug electrode gap and porcelain insulator for carbon tracking.",
                    "Swap ignition coil with adjacent cylinder to observe if misfire follows coil.",
                    "Perform compression test and inspect fuel injector spray pattern."
                }
            });
        }

        // 3. Charging System / Alternator Correlation
        var elecCheck = plausibility.Checks.FirstOrDefault(c => c.Category == "Electrical");
        if (elecCheck != null && (elecCheck.State == SensorPlausibilityState.Inconsistent || elecCheck.State == SensorPlausibilityState.OutOfRange))
        {
            candidates.Add(new DiagnosticRootCauseCandidate
            {
                SuspectComponent = "Alternator / Voltage Regulator / Battery",
                Description = "Charging system defect preventing nominal voltage regulation under engine load.",
                Confidence = EvidenceConfidence.Likely,
                SupportingEvidence = new() { elecCheck.DetailedEvidence },
                RecommendedPhysicalChecks = new()
                {
                    "Measure voltage drop across positive battery terminal and B+ alternator lug.",
                    "Inspect alternator drive belt tension and harmonic balancer condition.",
                    "Perform alternator diode ripple test with oscilloscope."
                }
            });
        }

        // 4. Coolant Temperature Circuit Correlation
        bool hasCoolantDtc = dtcs.Any(d => d.Code.StartsWith("P0115") || d.Code.StartsWith("P0117") || d.Code.StartsWith("P0118"));
        var thermalCheck = plausibility.Checks.FirstOrDefault(c => c.Category == "Thermal");
        if (hasCoolantDtc || (thermalCheck != null && thermalCheck.State != SensorPlausibilityState.Plausible && thermalCheck.State != SensorPlausibilityState.InsufficientData))
        {
            candidates.Add(new DiagnosticRootCauseCandidate
            {
                SuspectComponent = "Engine Coolant Temperature (ECT) Sensor / Wiring",
                Description = "ECT thermistor circuit impedance abnormal or open-circuit.",
                Confidence = hasCoolantDtc ? EvidenceConfidence.Confirmed : EvidenceConfidence.Likely,
                SupportingEvidence = new() { thermalCheck?.DetailedEvidence ?? "Coolant sensor DTC reported." },
                RecommendedPhysicalChecks = new()
                {
                    "Measure resistance across ECT sensor terminals at 20°C and 80°C against OEM resistance curve.",
                    "Verify 5.0V sensor reference supply and signal ground integrity at harness connector.",
                    "Inspect harness for rodent damage or pin corrosion."
                }
            });
        }

        return candidates;
    }
}
