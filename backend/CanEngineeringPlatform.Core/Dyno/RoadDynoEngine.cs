using System;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Analysis.TimeSeries;
using CanEngineeringPlatform.Core.Vehicles;

namespace CanEngineeringPlatform.Core.Dyno;

public class RoadDynoEngine
{
    private const double G = 9.80665; // Standard gravity m/s^2
    private const double HpToWatts = 745.699872;
    private const double MphToKmh = 1.609344;
    private const double QuarterMileMeters = 402.336;

    /// <summary>
    /// Executes physical road-dyno force balance, power/torque extraction, and atmospheric correction.
    /// </summary>
    public RoadDynoRun CalculateRoadDynoRun(
        IEnumerable<RoadDynoSample> rawSamples,
        VehicleProfile vehicle,
        int gear,
        AmbientConditions ambient,
        AtmosphericStandard standard = AtmosphericStandard.SAE_J1349,
        CoastDownProfile? coastDown = null,
        int smoothingWindow = 7)
    {
        if (rawSamples == null) throw new ArgumentNullException(nameof(rawSamples));
        if (vehicle == null) throw new ArgumentNullException(nameof(vehicle));
        if (ambient == null) ambient = new AmbientConditions();

        var samples = rawSamples.OrderBy(s => s.TimestampMs).ToList();
        if (samples.Count < 5)
        {
            throw new ArgumentException("Road dyno calculation requires at least 5 data samples.");
        }

        if (gear < 1 || gear > vehicle.Transmission.GearRatios.Length)
        {
            gear = Math.Clamp(gear, 1, vehicle.Transmission.GearRatios.Length);
        }

        double gearRatio = vehicle.Transmission.GearRatios[gear - 1];
        double totalGearRatio = gearRatio * vehicle.Transmission.FinalDriveRatio;
        double tireRadius = vehicle.Tires.RollingRadiusMeters;
        double tireCircumference = vehicle.Tires.CircumferenceMeters;
        double totalMassKg = vehicle.TotalMassKg;
        double effectiveMassKg = vehicle.EffectiveMassKg;
        double airDensity = ambient.AirDensityKgM3;
        double dragCd = vehicle.DragCoefficient;
        double frontalAreaA = vehicle.FrontalAreaM2;
        double crr = vehicle.RollingResistanceCoeff;
        bool isTurbo = vehicle.Engine.Aspiration.Contains("Turbo", StringComparison.OrdinalIgnoreCase);
        double correctionFactor = ambient.CalculateCorrectionFactor(standard, isTurbo);

        // 1. Smooth speed and RPM arrays synchronously
        var smoothedSpeedsKmh = SmoothSeries(samples.Select(s => s.GpsSpeedKmh).ToList(), smoothingWindow);
        var smoothedRpms = SmoothSeries(samples.Select(s => s.EngineRpm).ToList(), smoothingWindow);
        var dataPoints = new List<RoadDynoDataPoint>();

        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            double tSec = s.TimestampMs / 1000.0;
            double vKmh = smoothedSpeedsKmh[i];
            double vMps = vKmh / 3.6;

            // Numerical acceleration (m/s^2) via central difference where possible
            double aMps2 = 0.0;
            if (i > 0 && i < samples.Count - 1)
            {
                double dt = (samples[i + 1].TimestampMs - samples[i - 1].TimestampMs) / 1000.0;
                if (dt > 0.001)
                {
                    aMps2 = ((smoothedSpeedsKmh[i + 1] - smoothedSpeedsKmh[i - 1]) / 3.6) / dt;
                }
            }
            else if (i > 0)
            {
                double dt = (samples[i].TimestampMs - samples[i - 1].TimestampMs) / 1000.0;
                if (dt > 0.001) aMps2 = ((smoothedSpeedsKmh[i] - smoothedSpeedsKmh[i - 1]) / 3.6) / dt;
            }
            else if (samples.Count > 1)
            {
                double dt = (samples[1].TimestampMs - samples[0].TimestampMs) / 1000.0;
                if (dt > 0.001) aMps2 = ((smoothedSpeedsKmh[1] - smoothedSpeedsKmh[0]) / 3.6) / dt;
            }

            // Derive RPM if missing or 0 from speed and gear
            double rpm = smoothedRpms[i];
            if (rpm <= 100.0 && vMps > 0 && totalGearRatio > 0 && tireCircumference > 0)
            {
                double wheelRps = vMps / tireCircumference;
                rpm = wheelRps * totalGearRatio * 60.0;
            }
            rpm = Math.Max(500.0, rpm);

            // Forces (Newtons)
            double fInertial = effectiveMassKg * aMps2;

            // Aero drag
            double vAirMps = (vKmh + ambient.HeadwindSpeedKmh) / 3.6;
            double fAero = 0.5 * airDensity * dragCd * frontalAreaA * vAirMps * Math.Abs(vAirMps);

            // Rolling resistance
            double slopePct = s.SlopePct ?? ambient.RoadSlopePct;
            double slopeAngleRad = Math.Atan(slopePct / 100.0);
            double fRolling = crr * totalMassKg * G * Math.Cos(slopeAngleRad);

            // Slope gravity force
            double fSlope = totalMassKg * G * Math.Sin(slopeAngleRad);

            // Total tractive force at road surface
            double fTotal = fInertial + fAero + fRolling + fSlope;

            // Wheel Power (Watts, kW, HP)
            double pWheelWatts = Math.Max(0.0, fTotal * vMps);
            double pWheelKw = pWheelWatts / 1000.0;
            double pWheelHp = pWheelWatts / HpToWatts;

            // Wheel Torque (Nm, lb-ft)
            double tWheelNm = Math.Max(0.0, fTotal * tireRadius);
            double tWheelLbFt = tWheelNm * 0.737562;

            // Drivetrain Loss Calculation
            double pLossHp;
            if (coastDown != null && coastDown.IsMeasured)
            {
                pLossHp = coastDown.CalculateLossHp(vKmh);
            }
            else
            {
                // Empirical loss model: Base mechanical drag + percentage of transmitted wheel power
                double lossPct = vehicle.DrivetrainLossPct > 0 ? vehicle.DrivetrainLossPct : 15.0;
                double speedLossFactor = 0.04 * vKmh; // Aerodynamic & churning driveline loss with speed
                pLossHp = speedLossFactor + (pWheelHp * (lossPct / 100.0));
            }

            // Crank / Flywheel Power & Torque
            double pCrankHp = pWheelHp + pLossHp;
            double pCrankKw = pCrankHp * 0.745699872;

            // Crank Torque from Power: T (Nm) = (P_kW * 9548.8) / RPM
            double tCrankNm = rpm > 0 ? ((pCrankKw * 9548.8) / rpm) : 0.0;
            double tCrankLbFt = tCrankNm * 0.737562;

            // Atmospheric Corrected Values
            double pCorrectedHp = pCrankHp * correctionFactor;
            double tCorrectedNm = tCrankNm * correctionFactor;

            dataPoints.Add(new RoadDynoDataPoint
            {
                TimestampMs = s.TimestampMs,
                SpeedKmh = Math.Round(vKmh, 2),
                Rpm = Math.Round(rpm, 0),
                AccelerationMps2 = Math.Round(aMps2, 3),
                ForceInertialN = Math.Round(fInertial, 1),
                ForceAeroN = Math.Round(fAero, 1),
                ForceRollingN = Math.Round(fRolling, 1),
                ForceSlopeN = Math.Round(fSlope, 1),
                ForceTotalN = Math.Round(fTotal, 1),
                WheelPowerKw = Math.Round(pWheelKw, 2),
                WheelPowerHp = Math.Round(pWheelHp, 2),
                WheelTorqueNm = Math.Round(tWheelNm, 1),
                WheelTorqueLbFt = Math.Round(tWheelLbFt, 1),
                LossPowerHp = Math.Round(pLossHp, 2),
                EnginePowerKw = Math.Round(pCrankKw, 2),
                EnginePowerHp = Math.Round(pCrankHp, 2),
                EngineTorqueNm = Math.Round(tCrankNm, 1),
                EngineTorqueLbFt = Math.Round(tCrankLbFt, 1),
                CorrectionFactor = Math.Round(correctionFactor, 4),
                CorrectedEnginePowerHp = Math.Round(pCorrectedHp, 2),
                CorrectedEngineTorqueNm = Math.Round(tCorrectedNm, 1)
            });
        }

        // Apply secondary smoothing on power & torque curves across RPM
        dataPoints = SmoothPowerCurves(dataPoints);

        // Compute peaks
        var maxWheelHpPt = dataPoints.OrderByDescending(p => p.WheelPowerHp).First();
        var maxWheelTqPt = dataPoints.OrderByDescending(p => p.WheelTorqueNm).First();
        var maxCrankHpPt = dataPoints.OrderByDescending(p => p.EnginePowerHp).First();
        var maxCrankTqPt = dataPoints.OrderByDescending(p => p.EngineTorqueNm).First();
        var maxCorrectedHpPt = dataPoints.OrderByDescending(p => p.CorrectedEnginePowerHp).First();
        var maxCorrectedTqPt = dataPoints.OrderByDescending(p => p.CorrectedEngineTorqueNm).First();

        var run = new RoadDynoRun
        {
            Gear = gear,
            Standard = standard,
            CorrectionFactor = Math.Round(correctionFactor, 4),
            StartRpm = dataPoints.First().Rpm,
            EndRpm = dataPoints.Last().Rpm,
            DurationSeconds = Math.Round((dataPoints.Last().TimestampMs - dataPoints.First().TimestampMs) / 1000.0, 2),
            PeakWheelPowerHp = maxWheelHpPt.WheelPowerHp,
            PeakWheelPowerKw = maxWheelHpPt.WheelPowerKw,
            PeakWheelPowerRpm = maxWheelHpPt.Rpm,
            PeakWheelTorqueNm = maxWheelTqPt.WheelTorqueNm,
            PeakWheelTorqueLbFt = maxWheelTqPt.WheelTorqueLbFt,
            PeakWheelTorqueRpm = maxWheelTqPt.Rpm,
            PeakEnginePowerHp = maxCrankHpPt.EnginePowerHp,
            PeakEnginePowerKw = maxCrankHpPt.EnginePowerKw,
            PeakEnginePowerRpm = maxCrankHpPt.Rpm,
            PeakEngineTorqueNm = maxCrankTqPt.EngineTorqueNm,
            PeakEngineTorqueLbFt = maxCrankTqPt.EngineTorqueLbFt,
            PeakEngineTorqueRpm = maxCrankTqPt.Rpm,
            PeakCorrectedPowerHp = maxCorrectedHpPt.CorrectedEnginePowerHp,
            PeakCorrectedTorqueNm = maxCorrectedTqPt.CorrectedEngineTorqueNm,
            Points = dataPoints,
            VehicleSnapshot = vehicle,
            AmbientSnapshot = ambient,
            CoastDown = coastDown
        };

        run.Quality = EvaluateRunQuality(dataPoints, gear, vehicle);
        run.Performance = CalculatePerformanceSplits(samples);

        return run;
    }

    /// <summary>
    /// Automatically detects WOT acceleration runs from a continuous GPS/CAN time-series.
    /// </summary>
    public List<RoadDynoSample[]> DetectAccelerationRuns(
        IEnumerable<RoadDynoSample> continuousStream,
        VehicleProfile vehicle,
        double minThrottlePct = 80.0,
        double minDurationSec = 2.5)
    {
        var runs = new List<RoadDynoSample[]>();
        var samples = continuousStream.OrderBy(s => s.TimestampMs).ToList();
        if (samples.Count < 10) return runs;

        var currentCandidate = new List<RoadDynoSample>();
        bool inPull = false;

        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            bool isWot = s.ThrottlePosition >= minThrottlePct;

            // Also check speed trend if throttle is not available
            if (!isWot && i > 0)
            {
                double dt = (s.TimestampMs - samples[i - 1].TimestampMs) / 1000.0;
                double dv = (s.GpsSpeedKmh - samples[i - 1].GpsSpeedKmh);
                if (dt > 0.05 && (dv / dt) > 1.0) // Acceleration > 1 m/s^2
                {
                    isWot = true;
                }
            }

            if (isWot)
            {
                if (!inPull)
                {
                    inPull = true;
                    currentCandidate.Clear();
                }
                currentCandidate.Add(s);
            }
            else
            {
                if (inPull)
                {
                    inPull = false;
                    double duration = currentCandidate.Count >= 2
                        ? (currentCandidate.Last().TimestampMs - currentCandidate.First().TimestampMs) / 1000.0
                        : 0.0;

                    double speedGain = currentCandidate.Count >= 2
                        ? (currentCandidate.Last().GpsSpeedKmh - currentCandidate.First().GpsSpeedKmh)
                        : 0.0;

                    if (duration >= minDurationSec && speedGain >= 20.0)
                    {
                        runs.Add(currentCandidate.ToArray());
                    }
                    currentCandidate.Clear();
                }
            }
        }

        if (inPull && currentCandidate.Count > 0)
        {
            double duration = (currentCandidate.Last().TimestampMs - currentCandidate.First().TimestampMs) / 1000.0;
            double speedGain = (currentCandidate.Last().GpsSpeedKmh - currentCandidate.First().GpsSpeedKmh);
            if (duration >= minDurationSec && speedGain >= 20.0)
            {
                runs.Add(currentCandidate.ToArray());
            }
        }

        return runs;
    }

    /// <summary>
    /// Analyzes neutral deceleration samples to construct a measurement-based driveline loss curve.
    /// </summary>
    public CoastDownProfile AnalyzeCoastDown(
        IEnumerable<RoadDynoSample> coastDownSamples,
        VehicleProfile vehicle,
        AmbientConditions ambient)
    {
        var samples = coastDownSamples.OrderBy(s => s.TimestampMs).ToList();
        var profile = new CoastDownProfile { Samples = samples };
        if (samples.Count < 5) return profile;

        double totalMassKg = vehicle.TotalMassKg;
        double airDensity = ambient.AirDensityKgM3;
        double dragCd = vehicle.DragCoefficient;
        double frontalAreaA = vehicle.FrontalAreaM2;

        var speedsKmh = SmoothSeries(samples.Select(s => s.GpsSpeedKmh).ToList(), 5);
        double totalLossPower = 0.0;
        int count = 0;
        double sumDecel = 0.0;

        for (int i = 1; i < samples.Count; i++)
        {
            double dt = (samples[i].TimestampMs - samples[i - 1].TimestampMs) / 1000.0;
            if (dt <= 0.01) continue;

            double vKmh = speedsKmh[i];
            double vMps = vKmh / 3.6;
            double decelMps2 = -((speedsKmh[i] - speedsKmh[i - 1]) / 3.6) / dt;
            if (decelMps2 <= 0) continue; // Skip non-decelerating points

            sumDecel += decelMps2;

            // Total retarding force = mass * decel
            double fRetarding = totalMassKg * decelMps2;

            // Subtract aero drag to isolate mechanical + rolling losses
            double fAero = 0.5 * airDensity * dragCd * frontalAreaA * vMps * vMps;
            double fMechanical = Math.Max(0.0, fRetarding - fAero);

            double pLossHp = (fMechanical * vMps) / HpToWatts;
            totalLossPower += pLossHp;
            count++;
        }

        if (count > 0)
        {
            profile.IsMeasured = true;
            profile.MeanDecelerationMps2 = Math.Round(sumDecel / count, 3);
            double avgSpeed = speedsKmh.Average();
            if (avgSpeed > 10.0)
            {
                double avgHp = totalLossPower / count;
                // Fit P_loss(v) = a * v + b * v^2
                profile.CoeffA = Math.Round((avgHp * 0.6) / avgSpeed, 5);
                profile.CoeffB = Math.Round((avgHp * 0.4) / (avgSpeed * avgSpeed), 6);
            }
        }

        return profile;
    }

    /// <summary>
    /// Calculates high-precision acceleration splits (0-60 mph, 0-100 km/h, 60-120 km/h, 100-200 km/h, 1/4-mile).
    /// </summary>
    public AccelerationPerformanceMetrics CalculatePerformanceSplits(IEnumerable<RoadDynoSample> samplesList)
    {
        var samples = samplesList.OrderBy(s => s.TimestampMs).ToList();
        var perf = new AccelerationPerformanceMetrics();
        if (samples.Count < 2) return perf;

        double t0 = samples[0].TimestampMs;
        var times = samples.Select(s => (s.TimestampMs - t0) / 1000.0).ToArray();
        var speeds = samples.Select(s => s.GpsSpeedKmh).ToArray();

        // 1. Calculate distance integration for 1/4-mile
        double[] distances = new double[samples.Count];
        distances[0] = 0.0;
        for (int i = 1; i < samples.Count; i++)
        {
            double dt = times[i] - times[i - 1];
            double avgV = ((speeds[i] + speeds[i - 1]) / 2.0) / 3.6;
            distances[i] = distances[i - 1] + (avgV * dt);
        }

        // 0-60 mph (96.5606 km/h)
        perf.ZeroToSixtyMphSec = CalculateSplit(times, speeds, 0.0, 96.5606);

        // 0-100 km/h
        perf.ZeroToHundredKmhSec = CalculateSplit(times, speeds, 0.0, 100.0);

        // 60-120 km/h
        perf.SixtyToOneTwentyKmhSec = CalculateSplit(times, speeds, 60.0, 120.0);

        // 100-200 km/h
        perf.HundredToTwoHundredKmhSec = CalculateSplit(times, speeds, 100.0, 200.0);

        // 50-80 km/h
        perf.FiftyToEightyKmhSec = CalculateSplit(times, speeds, 50.0, 80.0);

        // 80-120 km/h
        perf.EightyToOneTwentyKmhSec = CalculateSplit(times, speeds, 80.0, 120.0);

        // 1/4-mile (402.336 m)
        for (int i = 1; i < distances.Length; i++)
        {
            if (distances[i] >= QuarterMileMeters && distances[i - 1] < QuarterMileMeters)
            {
                double d0 = distances[i - 1];
                double d1 = distances[i];
                double frac = (QuarterMileMeters - d0) / (d1 - d0);
                double qmTime = times[i - 1] + frac * (times[i] - times[i - 1]);
                double qmSpeedKmh = speeds[i - 1] + frac * (speeds[i] - speeds[i - 1]);

                perf.QuarterMileSec = Math.Round(qmTime, 2);
                perf.QuarterMileTrapSpeedKmh = Math.Round(qmSpeedKmh, 1);
                perf.QuarterMileTrapSpeedMph = Math.Round(qmSpeedKmh / MphToKmh, 1);
                break;
            }
        }

        return perf;
    }

    /// <summary>
    /// Computes run quality metrics: jerk smoothness, gear ratio consistency, and slope variance.
    /// </summary>
    public RunQualityMetrics EvaluateRunQuality(
        IEnumerable<RoadDynoDataPoint> pointsList,
        int expectedGear,
        VehicleProfile vehicle)
    {
        var pts = pointsList.ToList();
        var quality = new RunQualityMetrics { DetectedGear = expectedGear };
        if (pts.Count < 5)
        {
            quality.QualityRating = "REJECTED";
            quality.Warnings.Add("Insufficient data points for quality evaluation.");
            return quality;
        }

        // 1. Calculate sample rate (Hz)
        double totalTimeSec = (pts.Last().TimestampMs - pts.First().TimestampMs) / 1000.0;
        quality.GpsSampleRateHz = totalTimeSec > 0 ? Math.Round(pts.Count / totalTimeSec, 1) : 0.0;

        // 2. Jerk calculation (da/dt) across interior points to avoid boundary discretization transients
        double sumJerkSq = 0.0;
        int jerkCount = 0;
        int startIdx = Math.Min(2, pts.Count - 1);
        int endIdx = Math.Max(startIdx, pts.Count - 2);

        for (int i = startIdx; i <= endIdx; i++)
        {
            double dt = (pts[i].TimestampMs - pts[i - 1].TimestampMs) / 1000.0;
            if (dt > 0.005)
            {
                double jerk = (pts[i].AccelerationMps2 - pts[i - 1].AccelerationMps2) / dt;
                sumJerkSq += jerk * jerk;
                jerkCount++;
            }
        }

        quality.JerkRms = jerkCount > 0 ? Math.Round(Math.Sqrt(sumJerkSq / jerkCount), 2) : 0.0;
        // Map Jerk RMS to 0-100% smoothness
        quality.SmoothnessScore = Math.Clamp(Math.Round(100.0 - (quality.JerkRms * 2.0), 1), 20.0, 100.0);

        // 3. Gear ratio consistency (RPM / Speed ratio) to detect clutch slip
        double expectedRatio = (vehicle.Transmission.GearRatios[expectedGear - 1] * vehicle.Transmission.FinalDriveRatio);
        var ratios = pts.Where(p => p.SpeedKmh > 20.0).Select(p => p.Rpm / (p.SpeedKmh / 3.6)).ToList();
        if (ratios.Count > 0)
        {
            double meanRatio = ratios.Average();
            double variance = ratios.Max() - ratios.Min();
            quality.GearRatioVariancePct = Math.Round((variance / meanRatio) * 100.0, 2);

            if (quality.GearRatioVariancePct > 8.0)
            {
                quality.Warnings.Add($"Gear ratio variation ({quality.GearRatioVariancePct}%) indicates clutch slip or wheel spin.");
            }
        }

        // Quality rating categorization
        if (quality.SmoothnessScore >= 75.0 && quality.GearRatioVariancePct <= 7.0)
        {
            quality.QualityRating = "EXCELLENT";
        }
        else if (quality.SmoothnessScore >= 60.0 && quality.GearRatioVariancePct <= 10.0)
        {
            quality.QualityRating = "GOOD";
        }
        else if (quality.SmoothnessScore >= 50.0)
        {
            quality.QualityRating = "MARGINAL";
            quality.Warnings.Add("Surface bumps or noisy acceleration detected; use smoothing.");
        }
        else
        {
            quality.QualityRating = "POOR";
            quality.Warnings.Add("Excessive jerk or irregular speed trace; pull accuracy compromised.");
        }

        return quality;
    }

    /// <summary>
    /// Compares Baseline Run vs Test Run across a uniform RPM grid, computing delta curves and performance gains.
    /// </summary>
    public DynoRunComparisonResult CompareRuns(RoadDynoRun baseline, RoadDynoRun test)
    {
        if (baseline == null) throw new ArgumentNullException(nameof(baseline));
        if (test == null) throw new ArgumentNullException(nameof(test));

        var comp = new DynoRunComparisonResult
        {
            BaselineRunId = baseline.RunId,
            BaselineRunName = baseline.Name,
            TestRunId = test.RunId,
            TestRunName = test.Name
        };

        // Align on uniform 100 RPM grid between common RPM range
        double minRpm = Math.Max(baseline.StartRpm, test.StartRpm);
        double maxRpm = Math.Min(baseline.EndRpm, test.EndRpm);
        if (minRpm >= maxRpm)
        {
            minRpm = 2000.0;
            maxRpm = 6500.0;
        }

        for (double rpm = minRpm; rpm <= maxRpm; rpm += 100.0)
        {
            double baseHp = InterpolateAtRpm(baseline.Points, p => p.CorrectedEnginePowerHp, rpm);
            double testHp = InterpolateAtRpm(test.Points, p => p.CorrectedEnginePowerHp, rpm);
            double baseTq = InterpolateAtRpm(baseline.Points, p => p.CorrectedEngineTorqueNm, rpm);
            double testTq = InterpolateAtRpm(test.Points, p => p.CorrectedEngineTorqueNm, rpm);

            double dHp = testHp - baseHp;
            double dHpPct = baseHp > 0 ? (dHp / baseHp) * 100.0 : 0.0;

            double dTq = testTq - baseTq;
            double dTqPct = baseTq > 0 ? (dTq / baseTq) * 100.0 : 0.0;

            comp.AlignedRpm.Add(rpm);
            comp.BaselinePowerHp.Add(Math.Round(baseHp, 1));
            comp.TestPowerHp.Add(Math.Round(testHp, 1));
            comp.DeltaPowerHp.Add(Math.Round(dHp, 1));
            comp.DeltaPowerPct.Add(Math.Round(dHpPct, 1));

            comp.BaselineTorqueNm.Add(Math.Round(baseTq, 1));
            comp.TestTorqueNm.Add(Math.Round(testTq, 1));
            comp.DeltaTorqueNm.Add(Math.Round(dTq, 1));
            comp.DeltaTorquePct.Add(Math.Round(dTqPct, 1));
        }

        // Peak comparisons
        comp.PeakPowerDeltaHp = Math.Round(test.PeakCorrectedPowerHp - baseline.PeakCorrectedPowerHp, 1);
        comp.PeakPowerDeltaPct = baseline.PeakCorrectedPowerHp > 0
            ? Math.Round((comp.PeakPowerDeltaHp / baseline.PeakCorrectedPowerHp) * 100.0, 1)
            : 0.0;

        comp.PeakTorqueDeltaNm = Math.Round(test.PeakCorrectedTorqueNm - baseline.PeakCorrectedTorqueNm, 1);
        comp.PeakTorqueDeltaPct = baseline.PeakCorrectedTorqueNm > 0
            ? Math.Round((comp.PeakTorqueDeltaNm / baseline.PeakCorrectedTorqueNm) * 100.0, 1)
            : 0.0;

        // Acceleration comparisons
        if (baseline.Performance.ZeroToSixtyMphSec.HasValue && test.Performance.ZeroToSixtyMphSec.HasValue)
        {
            comp.ZeroToSixtyDeltaSec = Math.Round(test.Performance.ZeroToSixtyMphSec.Value - baseline.Performance.ZeroToSixtyMphSec.Value, 2);
        }
        if (baseline.Performance.ZeroToHundredKmhSec.HasValue && test.Performance.ZeroToHundredKmhSec.HasValue)
        {
            comp.ZeroToHundredDeltaSec = Math.Round(test.Performance.ZeroToHundredKmhSec.Value - baseline.Performance.ZeroToHundredKmhSec.Value, 2);
        }
        if (baseline.Performance.QuarterMileSec.HasValue && test.Performance.QuarterMileSec.HasValue)
        {
            comp.QuarterMileDeltaSec = Math.Round(test.Performance.QuarterMileSec.Value - baseline.Performance.QuarterMileSec.Value, 2);
        }
        if (baseline.Performance.QuarterMileTrapSpeedKmh.HasValue && test.Performance.QuarterMileTrapSpeedKmh.HasValue)
        {
            comp.QuarterMileTrapSpeedDeltaKmh = Math.Round(test.Performance.QuarterMileTrapSpeedKmh.Value - baseline.Performance.QuarterMileTrapSpeedKmh.Value, 1);
        }

        return comp;
    }

    // ==========================================
    // Helpers
    // ==========================================

    private static double? CalculateSplit(double[] times, double[] speeds, double targetStartKmh, double targetEndKmh)
    {
        double? tStart = null;
        double? tEnd = null;

        if (targetStartKmh <= 0.0)
        {
            tStart = times[0];
        }

        for (int i = 1; i < speeds.Length; i++)
        {
            if (!tStart.HasValue && speeds[i] >= targetStartKmh && speeds[i - 1] < targetStartKmh)
            {
                double frac = (targetStartKmh - speeds[i - 1]) / (speeds[i] - speeds[i - 1]);
                tStart = times[i - 1] + frac * (times[i] - times[i - 1]);
            }

            if (tStart.HasValue && !tEnd.HasValue && speeds[i] >= targetEndKmh && speeds[i - 1] < targetEndKmh)
            {
                double frac = (targetEndKmh - speeds[i - 1]) / (speeds[i] - speeds[i - 1]);
                tEnd = times[i - 1] + frac * (times[i] - times[i - 1]);
                break;
            }
        }

        if (tStart.HasValue && tEnd.HasValue)
        {
            return Math.Round(tEnd.Value - tStart.Value, 2);
        }
        return null;
    }

    private static List<double> SmoothSeries(List<double> values, int window)
    {
        if (values.Count <= window || window <= 1) return new List<double>(values);
        var smoothed = new List<double>(values.Count);
        int half = window / 2;

        for (int i = 0; i < values.Count; i++)
        {
            int start = Math.Max(0, i - half);
            int end = Math.Min(values.Count - 1, i + half);
            double sum = 0.0;
            for (int j = start; j <= end; j++) sum += values[j];
            smoothed.Add(sum / (end - start + 1));
        }

        return smoothed;
    }

    private static List<RoadDynoDataPoint> SmoothPowerCurves(List<RoadDynoDataPoint> pts)
    {
        if (pts.Count < 5) return pts;
        const int window = 5;
        int half = window / 2;

        for (int i = 0; i < pts.Count; i++)
        {
            int start = Math.Max(0, i - half);
            int end = Math.Min(pts.Count - 1, i + half);
            int count = end - start + 1;

            double sumWhp = 0.0, sumBhp = 0.0, sumWtq = 0.0, sumBtq = 0.0, sumCorHp = 0.0, sumCorTq = 0.0;
            for (int j = start; j <= end; j++)
            {
                sumWhp += pts[j].WheelPowerHp;
                sumBhp += pts[j].EnginePowerHp;
                sumWtq += pts[j].WheelTorqueNm;
                sumBtq += pts[j].EngineTorqueNm;
                sumCorHp += pts[j].CorrectedEnginePowerHp;
                sumCorTq += pts[j].CorrectedEngineTorqueNm;
            }

            pts[i].WheelPowerHp = Math.Round(sumWhp / count, 1);
            pts[i].EnginePowerHp = Math.Round(sumBhp / count, 1);
            pts[i].WheelTorqueNm = Math.Round(sumWtq / count, 1);
            pts[i].EngineTorqueNm = Math.Round(sumBtq / count, 1);
            pts[i].CorrectedEnginePowerHp = Math.Round(sumCorHp / count, 1);
            pts[i].CorrectedEngineTorqueNm = Math.Round(sumCorTq / count, 1);
        }

        return pts;
    }

    private static double InterpolateAtRpm(List<RoadDynoDataPoint> pts, Func<RoadDynoDataPoint, double> selector, double targetRpm)
    {
        if (pts.Count == 0) return 0.0;
        if (targetRpm <= pts[0].Rpm) return selector(pts[0]);
        if (targetRpm >= pts[pts.Count - 1].Rpm) return selector(pts[pts.Count - 1]);

        for (int i = 1; i < pts.Count; i++)
        {
            if (pts[i].Rpm >= targetRpm && pts[i - 1].Rpm <= targetRpm)
            {
                double dRpm = pts[i].Rpm - pts[i - 1].Rpm;
                if (dRpm <= 0) return selector(pts[i]);
                double frac = (targetRpm - pts[i - 1].Rpm) / dRpm;
                return selector(pts[i - 1]) + frac * (selector(pts[i]) - selector(pts[i - 1]));
            }
        }
        return selector(pts[pts.Count - 1]);
    }

    /// <summary>
    /// Extracts synchronous RoadDynoSample records from a canonical log dataset.
    /// Matches RPM, vehicle/GPS speed, throttle position, and GPS location channels.
    /// </summary>
    public static List<RoadDynoSample> ExtractSamples(CanonicalLogDataSet dataset)
    {
        if (dataset == null) return new List<RoadDynoSample>();

        // Find primary speed and RPM channels
        TimeSeriesChannel? rpmChannel = dataset.Channels.Values.FirstOrDefault(c => 
            c.Name.Contains("RPM", StringComparison.OrdinalIgnoreCase) || 
            c.Name.Contains("Engine Speed", StringComparison.OrdinalIgnoreCase));

        TimeSeriesChannel? speedChannel = dataset.Channels.Values.FirstOrDefault(c => 
            c.Name.Contains("Speed", StringComparison.OrdinalIgnoreCase) || 
            c.Name.Contains("VSS", StringComparison.OrdinalIgnoreCase));

        TimeSeriesChannel? throttleChannel = dataset.Channels.Values.FirstOrDefault(c => 
            c.Name.Contains("Throttle", StringComparison.OrdinalIgnoreCase) || 
            c.Name.Contains("TPS", StringComparison.OrdinalIgnoreCase) ||
            c.Name.Contains("Pedal", StringComparison.OrdinalIgnoreCase));

        var samples = new List<RoadDynoSample>();

        // If GPS track exists with speeds, align with GPS points
        if (dataset.GpsTrack.Count > 0 && speedChannel == null)
        {
            foreach (var gps in dataset.GpsTrack)
            {
                double rpm = 2500.0;
                if (rpmChannel != null && rpmChannel.SampleCount > 0)
                {
                    rpm = InterpolateChannelAt(rpmChannel, gps.TimestampMs);
                }

                samples.Add(new RoadDynoSample
                {
                    TimestampMs = gps.TimestampMs,
                    GpsSpeedKmh = gps.SpeedKmh ?? 0.0,
                    EngineRpm = rpm,
                    ThrottlePosition = 100.0,
                    Latitude = gps.Latitude,
                    Longitude = gps.Longitude,
                    AltitudeMeters = gps.AltitudeMeters
                });
            }
            return samples;
        }

        if (speedChannel == null || speedChannel.SampleCount == 0) return samples;

        for (int i = 0; i < speedChannel.SampleCount; i++)
        {
            double t = speedChannel.TimestampsMs[i];
            double speed = speedChannel.Values[i];
            double rpm = rpmChannel != null ? InterpolateChannelAt(rpmChannel, t) : 0.0;
            double tps = throttleChannel != null ? InterpolateChannelAt(throttleChannel, t) : 100.0;

            samples.Add(new RoadDynoSample
            {
                TimestampMs = t,
                GpsSpeedKmh = speed,
                EngineRpm = rpm,
                ThrottlePosition = tps
            });
        }

        return samples;
    }

    private static double InterpolateChannelAt(TimeSeriesChannel channel, double timestampMs)
    {
        if (channel.SampleCount == 0) return 0.0;
        if (channel.SampleCount == 1 || timestampMs <= channel.TimestampsMs[0]) return channel.Values[0];
        if (timestampMs >= channel.TimestampsMs[channel.TimestampsMs.Count - 1]) return channel.Values[channel.Values.Count - 1];

        for (int i = 1; i < channel.SampleCount; i++)
        {
            if (channel.TimestampsMs[i] >= timestampMs)
            {
                double t0 = channel.TimestampsMs[i - 1];
                double t1 = channel.TimestampsMs[i];
                double dt = t1 - t0;
                if (dt <= 0) return channel.Values[i];
                double frac = (timestampMs - t0) / dt;
                return channel.Values[i - 1] + frac * (channel.Values[i] - channel.Values[i - 1]);
            }
        }
        return channel.Values[channel.Values.Count - 1];
    }
}
