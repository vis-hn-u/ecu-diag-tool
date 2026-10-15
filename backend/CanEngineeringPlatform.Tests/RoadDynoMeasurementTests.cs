using System;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Dyno;
using CanEngineeringPlatform.Core.Vehicles;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class RoadDynoMeasurementTests
{
    private readonly RoadDynoEngine _engine = new();

    private VehicleProfile CreateTestVehicle()
    {
        return new VehicleProfile
        {
            Name = "Golf R 2.0T",
            CurbWeightKg = 1450.0,
            TestWeightKg = 1530.0,
            DragCoefficient = 0.31,
            FrontalAreaM2 = 2.20,
            RollingResistanceCoeff = 0.013,
            DrivetrainType = "AWD",
            DrivetrainLossPct = 16.0,
            RotationalMassFactor = 1.08,
            Engine = new EngineSpecs
            {
                DisplacementLiters = 2.0,
                Aspiration = "Turbocharged",
                RedlineRpm = 6800.0
            },
            Transmission = new TransmissionSpecs
            {
                FinalDriveRatio = 3.944,
                GearRatios = new double[] { 3.769, 2.087, 1.457, 1.109, 0.974, 0.733 }
            },
            Tires = new TireSpecs
            {
                SizeSpec = "235/40R18",
                WidthMm = 235.0,
                AspectRatioPct = 40.0,
                RimDiameterInches = 18.0
            }
        };
    }

    // ==========================================
    // 1. Physical Forces & Power Equation Tests
    // ==========================================

    [Fact]
    public void RoadDyno_PhysicalForcesAndPower_MatchPhysicsEquation()
    {
        var vehicle = CreateTestVehicle();
        var ambient = new AmbientConditions
        {
            TemperatureC = 20.0,
            BarometricPressureHpa = 1013.25,
            RelativeHumidityPct = 50.0,
            HeadwindSpeedKmh = 0.0,
            RoadSlopePct = 0.0
        };

        // Create constant acceleration pull: 50 km/h to 130 km/h over 4.0 seconds (a = 5.55 m/s^2)
        var samples = new List<RoadDynoSample>();
        for (int i = 0; i <= 40; i++)
        {
            double tMs = i * 100.0;
            double vKmh = 50.0 + (80.0 * (i / 40.0));
            double rpm = vehicle.CalculateRpmAtSpeed(3, vKmh);
            samples.Add(new RoadDynoSample
            {
                TimestampMs = tMs,
                GpsSpeedKmh = vKmh,
                EngineRpm = rpm,
                ThrottlePosition = 100.0
            });
        }

        var run = _engine.CalculateRoadDynoRun(samples, vehicle, 3, ambient, AtmosphericStandard.Uncorrected);

        Assert.NotNull(run);
        Assert.NotEmpty(run.Points);
        Assert.True(run.PeakWheelPowerHp > 100.0);
        Assert.True(run.PeakEnginePowerHp > run.PeakWheelPowerHp); // Engine power > wheel power
        Assert.True(run.PeakEngineTorqueNm > 150.0);

        // Verify forces at mid-point (~90 km/h = 25 m/s)
        var midPt = run.Points[20];
        Assert.True(midPt.ForceInertialN > 0.0);
        Assert.True(midPt.ForceAeroN > 0.0);
        Assert.True(midPt.ForceRollingN > 0.0);
        Assert.Equal(0.0, midPt.ForceSlopeN, 1); // Flat road
        Assert.Equal(midPt.ForceTotalN, midPt.ForceInertialN + midPt.ForceAeroN + midPt.ForceRollingN + midPt.ForceSlopeN, 1);
    }

    [Fact]
    public void RoadDyno_WhenVehicleSpeedIsZeroOrMissing_DerivesSpeedFromRpm()
    {
        var vehicle = CreateTestVehicle();
        var ambient = new AmbientConditions();

        // Create samples with 0 km/h (bench test where ECU only outputs RPM)
        var samples = new List<RoadDynoSample>();
        for (int i = 0; i <= 40; i++)
        {
            double tMs = i * 100.0;
            double rpm = 2000.0 + (4500.0 * (i / 40.0));
            samples.Add(new RoadDynoSample
            {
                TimestampMs = tMs,
                GpsSpeedKmh = 0.0, // ABS speed unavailable on bench
                EngineRpm = rpm,
                ThrottlePosition = 100.0
            });
        }

        var run = _engine.CalculateRoadDynoRun(samples, vehicle, 3, ambient, AtmosphericStandard.SAE_J1349);

        Assert.NotNull(run);
        Assert.NotEmpty(run.Points);
        Assert.True(run.PeakWheelPowerHp > 50.0, "Wheel power should be computed via kinematics even with 0 GPS speed");
        Assert.True(run.PeakEnginePowerHp > run.PeakWheelPowerHp, "Crank power should exceed wheel power");
        Assert.True(run.Points.Last().SpeedKmh > 50.0, "End speed should be derived from gear ratio and RPM");
    }

    // ==========================================
    // 2. Atmospheric Correction Standards Tests
    // ==========================================

    [Fact]
    public void RoadDyno_AtmosphericCorrections_CalculateAccurately()
    {
        // Standard atmospheric conditions: 20C, 1013.25 hPa, 50% RH
        var stdAmbient = new AmbientConditions
        {
            TemperatureC = 20.0,
            BarometricPressureHpa = 1013.25,
            RelativeHumidityPct = 50.0
        };

        // DIN 70020 at 20°C & 1013.25 hPa should be exactly 1.00
        double cfDin = stdAmbient.CalculateCorrectionFactor(AtmosphericStandard.DIN_70020, true);
        Assert.InRange(cfDin, 0.99, 1.01);

        // SAE J1349 at 25°C & 990 hPa dry
        var saeAmbient = new AmbientConditions
        {
            TemperatureC = 25.0,
            BarometricPressureHpa = 1000.0,
            RelativeHumidityPct = 35.0
        };
        double cfSae = saeAmbient.CalculateCorrectionFactor(AtmosphericStandard.SAE_J1349, true);
        Assert.InRange(cfSae, 0.98, 1.03);

        // High Altitude & Heat: 35°C, 850 hPa (high density altitude) -> Power is lower, so CF > 1.0 to correct back to standard
        var altitudeAmbient = new AmbientConditions
        {
            TemperatureC = 35.0,
            BarometricPressureHpa = 850.0,
            RelativeHumidityPct = 40.0
        };
        double cfAltitude = altitudeAmbient.CalculateCorrectionFactor(AtmosphericStandard.SAE_J1349, true);
        Assert.True(cfAltitude > 1.05); // Must boost uncorrected figures to standard conditions

        // Cold Winter Day: 0°C, 1030 hPa -> Dense air makes more power, so CF < 1.0 to normalize
        var coldAmbient = new AmbientConditions
        {
            TemperatureC = 0.0,
            BarometricPressureHpa = 1030.0,
            RelativeHumidityPct = 40.0
        };
        double cfCold = coldAmbient.CalculateCorrectionFactor(AtmosphericStandard.SAE_J1349, true);
        Assert.True(cfCold < 1.0);
    }

    // ==========================================
    // 3. Acceleration-Run Detector Tests
    // ==========================================

    [Fact]
    public void RoadDyno_AccelerationRunDetector_IdentifiesWotPullsAndGear()
    {
        var vehicle = CreateTestVehicle();
        var samples = new List<RoadDynoSample>();

        // Phase 1: 3 seconds of cruising at 50 km/h, 20% throttle
        for (int i = 0; i < 30; i++)
        {
            samples.Add(new RoadDynoSample
            {
                TimestampMs = i * 100.0,
                GpsSpeedKmh = 50.0,
                EngineRpm = 2200.0,
                ThrottlePosition = 20.0
            });
        }

        // Phase 2: 4 seconds of WOT acceleration from 50 to 135 km/h, 100% throttle
        for (int i = 30; i <= 70; i++)
        {
            double v = 50.0 + ((i - 30) / 40.0) * 85.0;
            samples.Add(new RoadDynoSample
            {
                TimestampMs = i * 100.0,
                GpsSpeedKmh = v,
                EngineRpm = vehicle.CalculateRpmAtSpeed(3, v),
                ThrottlePosition = 100.0
            });
        }

        // Phase 3: 3 seconds of neutral coast-down
        for (int i = 71; i <= 100; i++)
        {
            double v = 135.0 - ((i - 70) / 30.0) * 15.0;
            samples.Add(new RoadDynoSample
            {
                TimestampMs = i * 100.0,
                GpsSpeedKmh = v,
                EngineRpm = 900.0, // Idle during neutral coast
                ThrottlePosition = 0.0
            });
        }

        var detected = _engine.DetectAccelerationRuns(samples, vehicle, minThrottlePct: 80.0, minDurationSec: 2.5);

        Assert.Single(detected);
        var pull = detected[0];
        Assert.InRange(pull.Length, 35, 45);
        Assert.True(pull.First().GpsSpeedKmh <= 55.0);
        Assert.True(pull.Last().GpsSpeedKmh >= 130.0);
    }

    // ==========================================
    // 4. Coast-Down Loss Measurement Tests
    // ==========================================

    [Fact]
    public void RoadDyno_CoastDownLoss_FitsPolynomialCurve()
    {
        var vehicle = CreateTestVehicle();
        var ambient = new AmbientConditions();
        var coastSamples = new List<RoadDynoSample>();

        // Simulate neutral deceleration from 140 km/h down to 60 km/h over 10 seconds
        for (int i = 0; i <= 100; i++)
        {
            double t = i * 100.0;
            double v = 140.0 - (80.0 * (i / 100.0));
            coastSamples.Add(new RoadDynoSample
            {
                TimestampMs = t,
                GpsSpeedKmh = v,
                EngineRpm = 850.0,
                ThrottlePosition = 0.0
            });
        }

        var profile = _engine.AnalyzeCoastDown(coastSamples, vehicle, ambient);

        Assert.NotNull(profile);
        Assert.True(profile.IsMeasured);
        Assert.True(profile.MeanDecelerationMps2 > 0.0);
        Assert.True(profile.CoeffA > 0.0);

        // Loss power should increase with road speed
        double lossAt60 = profile.CalculateLossHp(60.0);
        double lossAt120 = profile.CalculateLossHp(120.0);
        Assert.True(lossAt120 > lossAt60);
    }

    // ==========================================
    // 5. Performance Splits & 1/4-Mile Tests
    // ==========================================

    [Fact]
    public void RoadDyno_PerformanceSplits_CalculatesSubTenthTimes()
    {
        var samples = new List<RoadDynoSample>();

        // Standstill to 200 km/h acceleration pull over 15.0 seconds
        // Fast sports car: 0-100 in ~4.8s, 1/4 mile in ~12.8s
        for (int i = 0; i <= 150; i++)
        {
            double tSec = i * 0.1;
            // Velocity curve modeled as v(t) = 220 * (1 - e^(-t / 6.0))
            double vKmh = 220.0 * (1.0 - Math.Exp(-tSec / 6.0));
            samples.Add(new RoadDynoSample
            {
                TimestampMs = i * 100.0,
                GpsSpeedKmh = vKmh,
                EngineRpm = 1000 + (vKmh * 30.0)
            });
        }

        var perf = _engine.CalculatePerformanceSplits(samples);

        Assert.NotNull(perf.ZeroToSixtyMphSec);
        Assert.NotNull(perf.ZeroToHundredKmhSec);
        Assert.NotNull(perf.SixtyToOneTwentyKmhSec);
        Assert.NotNull(perf.HundredToTwoHundredKmhSec);
        Assert.NotNull(perf.QuarterMileSec);
        Assert.NotNull(perf.QuarterMileTrapSpeedKmh);

        // Verify logical physics order: 0-60 mph (96.5 km/h) < 0-100 km/h
        Assert.True(perf.ZeroToSixtyMphSec.Value < perf.ZeroToHundredKmhSec.Value);
        Assert.InRange(perf.ZeroToHundredKmhSec.Value, 3.5, 6.5);
        Assert.InRange(perf.QuarterMileSec.Value, 11.0, 15.0);
        Assert.True(perf.QuarterMileTrapSpeedKmh.Value > 140.0);
    }

    // ==========================================
    // 6. Run Quality & Gear Slip Tests
    // ==========================================

    [Fact]
    public void RoadDyno_RunQuality_FlagsGearSlipAndSlope()
    {
        var vehicle = CreateTestVehicle();
        var ambient = new AmbientConditions();

        // 1. Clean Run
        var cleanSamples = new List<RoadDynoSample>();
        for (int i = 0; i <= 30; i++)
        {
            double v = 60.0 + i * 2.0;
            cleanSamples.Add(new RoadDynoSample
            {
                TimestampMs = i * 100.0,
                GpsSpeedKmh = v,
                EngineRpm = vehicle.CalculateRpmAtSpeed(3, v),
                ThrottlePosition = 100.0
            });
        }
        var cleanRun = _engine.CalculateRoadDynoRun(cleanSamples, vehicle, 3, ambient);
        Assert.True(cleanRun.Quality.SmoothnessScore >= 80.0);
        Assert.Equal("EXCELLENT", cleanRun.Quality.QualityRating);

        // 2. Run with severe clutch slip: RPM shoots up while speed doesn't increase proportionally
        var slipSamples = new List<RoadDynoSample>();
        for (int i = 0; i <= 30; i++)
        {
            double v = 60.0 + i * 1.0; // speed climbs slowly
            double rpm = (i > 10 && i < 20) ? 6500.0 : vehicle.CalculateRpmAtSpeed(3, v); // sudden RPM flare
            slipSamples.Add(new RoadDynoSample
            {
                TimestampMs = i * 100.0,
                GpsSpeedKmh = v,
                EngineRpm = rpm,
                ThrottlePosition = 100.0
            });
        }
        var slipRun = _engine.CalculateRoadDynoRun(slipSamples, vehicle, 3, ambient);
        Assert.True(slipRun.Quality.GearRatioVariancePct > 8.0);
        Assert.Contains(slipRun.Quality.Warnings, w => w.Contains("clutch slip", StringComparison.OrdinalIgnoreCase));
    }

    // ==========================================
    // 7. Stock vs Run Comparison Tests
    // ==========================================

    [Fact]
    public void RoadDyno_StockComparison_GeneratesDeltaCurves()
    {
        var vehicle = CreateTestVehicle();
        var ambient = new AmbientConditions();

        // Baseline Run (Stock): 200 WHP
        var baseSamples = new List<RoadDynoSample>();
        for (int i = 0; i <= 30; i++)
        {
            double v = 50.0 + i * 2.0;
            baseSamples.Add(new RoadDynoSample
            {
                TimestampMs = i * 100.0,
                GpsSpeedKmh = v,
                EngineRpm = vehicle.CalculateRpmAtSpeed(3, v)
            });
        }
        var baseline = _engine.CalculateRoadDynoRun(baseSamples, vehicle, 3, ambient, AtmosphericStandard.SAE_J1349);
        baseline.Name = "Stock Baseline";

        // Test Run (Stage 1 Tuned): faster acceleration, higher power
        var testSamples = new List<RoadDynoSample>();
        for (int i = 0; i <= 30; i++)
        {
            double v = 50.0 + i * 2.5; // accelerates 25% faster
            testSamples.Add(new RoadDynoSample
            {
                TimestampMs = i * 100.0,
                GpsSpeedKmh = v,
                EngineRpm = vehicle.CalculateRpmAtSpeed(3, v)
            });
        }
        var testRun = _engine.CalculateRoadDynoRun(testSamples, vehicle, 3, ambient, AtmosphericStandard.SAE_J1349);
        testRun.Name = "Stage 1 ECU Flash";

        var comparison = _engine.CompareRuns(baseline, testRun);

        Assert.NotNull(comparison);
        Assert.NotEmpty(comparison.AlignedRpm);
        Assert.Equal("Stock Baseline", comparison.BaselineRunName);
        Assert.Equal("Stage 1 ECU Flash", comparison.TestRunName);

        // Power delta should be positive (Test > Baseline)
        Assert.True(comparison.PeakPowerDeltaHp > 0.0);
        Assert.True(comparison.PeakPowerDeltaPct > 0.0);
        Assert.True(comparison.PeakTorqueDeltaNm > 0.0);
    }
}
