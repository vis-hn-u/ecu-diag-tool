using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Engine;
using CanEngineeringPlatform.Core.Vehicles;

namespace CanEngineeringPlatform.Core.Dyno;

public interface IDynoAnalysisService
{
    // Simulation Mode (Existing deterministic simulator)
    DynoPullSummary GenerateSimulatedDynoPull(string name = "Simulated Dyno Run", double startRpm = 2000, double endRpm = 6800);
    DynoPullSummary AnalyzePull(IEnumerable<DynoDataPoint> points, string name = "Dyno Pull", string type = "CALCULATED");
    VehiclePerformanceSummary CalculatePerformanceMetrics(DynoPullSummary pull);
    IReadOnlyList<DynoPullSummary> GetSavedPulls();
    void SavePull(DynoPullSummary pull);
    void ClearPulls();

    // Road Dyno Mode (Measurement-based system)
    RoadDynoRun AnalyzeRoadDynoRun(
        IEnumerable<RoadDynoSample> samples,
        VehicleProfile vehicle,
        int gear,
        AmbientConditions ambient,
        AtmosphericStandard standard = AtmosphericStandard.SAE_J1349,
        CoastDownProfile? coastDown = null,
        string name = "Road Dyno Run");

    RoadDynoRun AnalyzeRoadDynoDataSet(
        CanEngineeringPlatform.Core.Analysis.TimeSeries.CanonicalLogDataSet dataset,
        VehicleProfile vehicle,
        int gear,
        AmbientConditions ambient,
        AtmosphericStandard standard = AtmosphericStandard.SAE_J1349,
        CoastDownProfile? coastDown = null,
        string name = "Road Dyno From Log");

    List<RoadDynoSample[]> DetectRuns(IEnumerable<RoadDynoSample> samples, VehicleProfile vehicle);
    CoastDownProfile AnalyzeCoastDown(IEnumerable<RoadDynoSample> coastDownSamples, VehicleProfile vehicle, AmbientConditions ambient);
    DynoRunComparisonResult CompareRoadRuns(RoadDynoRun baseline, RoadDynoRun test);
    IReadOnlyList<RoadDynoRun> GetSavedRoadRuns();
    RoadDynoRun? GetSavedRoadRun(string id);
    void SaveRoadRun(RoadDynoRun run);
}

public class DynoAnalysisService : IDynoAnalysisService
{
    private readonly IEngineSimulator _engineSimulator;
    private readonly RoadDynoEngine _roadDynoEngine = new();
    private readonly ConcurrentDictionary<string, DynoPullSummary> _savedPulls = new();
    private readonly ConcurrentDictionary<string, RoadDynoRun> _savedRoadRuns = new();

    public DynoAnalysisService(IEngineSimulator engineSimulator)
    {
        _engineSimulator = engineSimulator;

        // Pre-populate with default baseline simulated pull
        var defaultPull = GenerateSimulatedDynoPull("Baseline 2.0L Turbo (Stock)", 2000, 6800);
        _savedPulls[defaultPull.PullId] = defaultPull;
    }

    // ==========================================
    // Simulation Mode Methods
    // ==========================================

    public DynoPullSummary GenerateSimulatedDynoPull(string name = "Simulated Dyno Run", double startRpm = 2000, double endRpm = 6800)
    {
        var rawOutputs = _engineSimulator.RunDynoPullSimulation(startRpm, endRpm, 100.0);
        var points = new List<DynoDataPoint>();

        double timeMs = 0;
        foreach (var o in rawOutputs)
        {
            points.Add(new DynoDataPoint
            {
                Rpm = o.Rpm,
                TorqueNm = o.TorqueNm,
                TorqueLbFt = PowerTorqueConverter.NmToLbFt(o.TorqueNm),
                PowerKw = o.PowerKw,
                PowerHp = o.PowerHp,
                BoostBar = o.BoostBar,
                Afr = o.Afr,
                EngineLoadPct = o.EngineLoadPct,
                VehicleSpeedKmh = o.VehicleSpeedKmh,
                TimestampMs = timeMs
            });
            timeMs += 100.0;
        }

        return AnalyzePull(points, name, "SIMULATED");
    }

    public DynoPullSummary AnalyzePull(IEnumerable<DynoDataPoint> points, string name = "Dyno Pull", string type = "CALCULATED")
    {
        var pts = points.OrderBy(p => p.Rpm).ToList();
        var summary = new DynoPullSummary
        {
            Name = name,
            Type = type,
            Points = pts
        };

        if (pts.Count == 0) return summary;

        summary.StartRpm = pts.First().Rpm;
        summary.EndRpm = pts.Last().Rpm;
        summary.DurationSeconds = Math.Round((pts.Last().TimestampMs - pts.First().TimestampMs) / 1000.0, 2);
        if (summary.DurationSeconds <= 0) summary.DurationSeconds = (pts.Count * 0.1);

        // Find peaks
        var maxTorquePoint = pts.OrderByDescending(p => p.TorqueNm).First();
        summary.PeakTorqueNm = maxTorquePoint.TorqueNm;
        summary.PeakTorqueLbFt = PowerTorqueConverter.NmToLbFt(maxTorquePoint.TorqueNm);
        summary.PeakTorqueRpm = maxTorquePoint.Rpm;

        var maxPowerPoint = pts.OrderByDescending(p => p.PowerHp).First();
        summary.PeakPowerHp = maxPowerPoint.PowerHp;
        summary.PeakPowerKw = maxPowerPoint.PowerKw;
        summary.PeakPowerRpm = maxPowerPoint.Rpm;

        return summary;
    }

    public VehiclePerformanceSummary CalculatePerformanceMetrics(DynoPullSummary pull)
    {
        double weightKg = _engineSimulator.Parameters.VehicleWeightKg;
        double peakHp = pull.PeakPowerHp > 0 ? pull.PeakPowerHp : 250.0;
        double wpr = weightKg / peakHp;

        var perf = new VehiclePerformanceSummary
        {
            SourceType = pull.Type,
            ZeroToSixtyMphSeconds = Math.Round(Math.Sqrt(wpr) * 2.35, 2),
            ZeroToHundredKmhSeconds = Math.Round(Math.Sqrt(wpr) * 2.48, 2),
            SixtyToHundredKmhSeconds = Math.Round(Math.Sqrt(wpr) * 1.30, 2),
            EightyToOneTwentyKmhSeconds = Math.Round(Math.Sqrt(wpr) * 1.65, 2),
            QuarterMileSeconds = Math.Round(5.825 * Math.Pow(weightKg / (peakHp * 0.9), 0.333), 2),
            QuarterMileSpeedKmh = Math.Round(234.0 * Math.Pow((peakHp * 0.9) / weightKg, 0.333), 1)
        };

        return perf;
    }

    public IReadOnlyList<DynoPullSummary> GetSavedPulls() => new List<DynoPullSummary>(_savedPulls.Values);

    public void SavePull(DynoPullSummary pull)
    {
        _savedPulls[pull.PullId] = pull;
    }

    public void ClearPulls()
    {
        _savedPulls.Clear();
        _savedRoadRuns.Clear();
    }

    // ==========================================
    // Road Dyno Mode Methods
    // ==========================================

    public RoadDynoRun AnalyzeRoadDynoRun(
        IEnumerable<RoadDynoSample> samples,
        VehicleProfile vehicle,
        int gear,
        AmbientConditions ambient,
        AtmosphericStandard standard = AtmosphericStandard.SAE_J1349,
        CoastDownProfile? coastDown = null,
        string name = "Road Dyno Run")
    {
        var run = _roadDynoEngine.CalculateRoadDynoRun(samples, vehicle, gear, ambient, standard, coastDown);
        run.Name = name;
        SaveRoadRun(run);

        // Also adapt into a DynoPullSummary for backward compatibility with existing pull browsers
        var adaptedPoints = run.Points.Select(p => new DynoDataPoint
        {
            TimestampMs = p.TimestampMs,
            Rpm = p.Rpm,
            VehicleSpeedKmh = p.SpeedKmh,
            PowerHp = p.CorrectedEnginePowerHp,
            PowerKw = p.EnginePowerKw,
            TorqueNm = p.CorrectedEngineTorqueNm,
            TorqueLbFt = p.EngineTorqueLbFt
        }).ToList();

        var adaptedSummary = new DynoPullSummary
        {
            PullId = run.RunId,
            Name = run.Name,
            Type = "MEASURED_ROAD",
            PeakPowerHp = run.PeakCorrectedPowerHp,
            PeakPowerKw = run.PeakEnginePowerKw,
            PeakPowerRpm = run.PeakEnginePowerRpm,
            PeakTorqueNm = run.PeakCorrectedTorqueNm,
            PeakTorqueLbFt = run.PeakEngineTorqueLbFt,
            PeakTorqueRpm = run.PeakEngineTorqueRpm,
            StartRpm = run.StartRpm,
            EndRpm = run.EndRpm,
            DurationSeconds = run.DurationSeconds,
            Points = adaptedPoints
        };
        _savedPulls[adaptedSummary.PullId] = adaptedSummary;

        return run;
    }

    public RoadDynoRun AnalyzeRoadDynoDataSet(
        CanEngineeringPlatform.Core.Analysis.TimeSeries.CanonicalLogDataSet dataset,
        VehicleProfile vehicle,
        int gear,
        AmbientConditions ambient,
        AtmosphericStandard standard = AtmosphericStandard.SAE_J1349,
        CoastDownProfile? coastDown = null,
        string name = "Road Dyno From Log")
    {
        var samples = RoadDynoEngine.ExtractSamples(dataset);
        return AnalyzeRoadDynoRun(samples, vehicle, gear, ambient, standard, coastDown, name);
    }

    public List<RoadDynoSample[]> DetectRuns(IEnumerable<RoadDynoSample> samples, VehicleProfile vehicle)
    {
        return _roadDynoEngine.DetectAccelerationRuns(samples, vehicle);
    }

    public CoastDownProfile AnalyzeCoastDown(IEnumerable<RoadDynoSample> coastDownSamples, VehicleProfile vehicle, AmbientConditions ambient)
    {
        return _roadDynoEngine.AnalyzeCoastDown(coastDownSamples, vehicle, ambient);
    }

    public DynoRunComparisonResult CompareRoadRuns(RoadDynoRun baseline, RoadDynoRun test)
    {
        return _roadDynoEngine.CompareRuns(baseline, test);
    }

    public IReadOnlyList<RoadDynoRun> GetSavedRoadRuns() => new List<RoadDynoRun>(_savedRoadRuns.Values);

    public RoadDynoRun? GetSavedRoadRun(string id)
    {
        _savedRoadRuns.TryGetValue(id, out var run);
        return run;
    }

    public void SaveRoadRun(RoadDynoRun run)
    {
        _savedRoadRuns[run.RunId] = run;
    }
}
