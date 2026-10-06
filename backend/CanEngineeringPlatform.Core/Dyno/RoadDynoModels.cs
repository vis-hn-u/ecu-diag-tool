using System;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Vehicles;

namespace CanEngineeringPlatform.Core.Dyno;

public enum AtmosphericStandard
{
    Uncorrected = 0,
    SAE_J1349 = 1,
    DIN_70020 = 2,
    EWG_80_1269 = 3,
    ISO_1585 = 4
}

public class AmbientConditions
{
    public double TemperatureC { get; set; } = 20.0;
    public double BarometricPressureHpa { get; set; } = 1013.25;
    public double RelativeHumidityPct { get; set; } = 50.0;
    public double HeadwindSpeedKmh { get; set; } = 0.0; // Positive = headwind, negative = tailwind
    public double RoadSlopePct { get; set; } = 0.0;     // Positive = uphill (degrees ~ slope / 100)

    /// <summary>
    /// Saturation vapor pressure (hPa) via August-Roche-Magnus formula.
    /// </summary>
    public double VaporPressureHpa
    {
        get
        {
            double es = 6.1078 * Math.Pow(10.0, (7.5 * TemperatureC) / (237.3 + TemperatureC));
            return (RelativeHumidityPct / 100.0) * es;
        }
    }

    /// <summary>
    /// Dry atmospheric pressure (hPa) excluding water vapor.
    /// </summary>
    public double DryAirPressureHpa => Math.Max(500.0, BarometricPressureHpa - VaporPressureHpa);

    /// <summary>
    /// Real atmospheric air density (kg/m^3) taking temperature, dry pressure, and moisture into account.
    /// </summary>
    public double AirDensityKgM3
    {
        get
        {
            double tempK = TemperatureC + 273.15;
            double pDryPa = DryAirPressureHpa * 100.0;
            double pVaporPa = VaporPressureHpa * 100.0;

            const double Rd = 287.058; // J/(kg·K) dry air
            const double Rv = 461.495; // J/(kg·K) water vapor

            return (pDryPa / (Rd * tempK)) + (pVaporPa / (Rv * tempK));
        }
    }

    /// <summary>
    /// Calculates dyno atmospheric correction factor for power and torque.
    /// </summary>
    public double CalculateCorrectionFactor(AtmosphericStandard standard, bool isTurbocharged = true)
    {
        double tempK = TemperatureC + 273.15;
        double pDryHpa = DryAirPressureHpa;
        double pTotalHpa = BarometricPressureHpa;

        double factor = standard switch
        {
            AtmosphericStandard.Uncorrected => 1.0,

            AtmosphericStandard.DIN_70020 =>
                // DIN 70020: 1013 hPa total, 20°C (293.15 K)
                (1013.25 / pTotalHpa) * Math.Sqrt(tempK / 293.15),

            AtmosphericStandard.SAE_J1349 =>
                // SAE J1349: 990 hPa dry, 25°C (298.15 K)
                isTurbocharged
                    ? Math.Pow(990.0 / pDryHpa, 0.7) * Math.Pow(tempK / 298.15, 0.2)
                    : (990.0 / pDryHpa) * Math.Sqrt(tempK / 298.15),

            AtmosphericStandard.EWG_80_1269 =>
                // EWG 80/1269 (European EEC standard)
                isTurbocharged
                    ? Math.Pow(990.0 / pDryHpa, 0.6) * Math.Pow(tempK / 298.15, 0.3)
                    : Math.Pow(990.0 / pDryHpa, 1.0) * Math.Pow(tempK / 298.15, 0.5),

            AtmosphericStandard.ISO_1585 =>
                // ISO 1585 standard
                isTurbocharged
                    ? Math.Pow(990.0 / pDryHpa, 0.6) * Math.Pow(tempK / 298.15, 0.3)
                    : (990.0 / pDryHpa) * Math.Sqrt(tempK / 298.15),

            _ => 1.0
        };

        // Clamp factor to realistic bounds [0.75 .. 1.35] to prevent runaway distortion
        return Math.Max(0.75, Math.Min(1.35, Math.Round(factor, 4)));
    }
}

public class RoadDynoSample
{
    public double TimestampMs { get; set; }
    public double GpsSpeedKmh { get; set; }
    public double EngineRpm { get; set; }
    public double ThrottlePosition { get; set; } = 100.0;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? AltitudeMeters { get; set; }
    public double? SlopePct { get; set; }
}

public class CoastDownProfile
{
    public List<RoadDynoSample> Samples { get; set; } = new();
    public double CoeffA { get; set; } = 0.05; // Loss power linear with speed: P_loss = a * v + b * v^2
    public double CoeffB { get; set; } = 0.0015;
    public double MeanDecelerationMps2 { get; set; }
    public bool IsMeasured { get; set; } = false;

    public double CalculateLossHp(double speedKmh)
    {
        if (speedKmh <= 0) return 0.0;
        double lossHp = (CoeffA * speedKmh) + (CoeffB * speedKmh * speedKmh);
        return Math.Max(1.0, lossHp);
    }
}

public class RoadDynoDataPoint
{
    public double TimestampMs { get; set; }
    public double SpeedKmh { get; set; }
    public double Rpm { get; set; }
    public double AccelerationMps2 { get; set; }

    // Physical Forces (Newtons)
    public double ForceInertialN { get; set; }
    public double ForceAeroN { get; set; }
    public double ForceRollingN { get; set; }
    public double ForceSlopeN { get; set; }
    public double ForceTotalN { get; set; }

    // Wheel Power & Torque
    public double WheelPowerKw { get; set; }
    public double WheelPowerHp { get; set; }
    public double WheelTorqueNm { get; set; }
    public double WheelTorqueLbFt { get; set; }

    // Driveline Losses & Crank Outputs
    public double LossPowerHp { get; set; }
    public double EnginePowerKw { get; set; }
    public double EnginePowerHp { get; set; }
    public double EngineTorqueNm { get; set; }
    public double EngineTorqueLbFt { get; set; }

    // Corrected Engine Outputs
    public double CorrectionFactor { get; set; } = 1.0;
    public double CorrectedEnginePowerHp { get; set; }
    public double CorrectedEngineTorqueNm { get; set; }
}

public class RunQualityMetrics
{
    public double SmoothnessScore { get; set; } = 95.0; // 0-100%
    public double JerkRms { get; set; }
    public double GpsSampleRateHz { get; set; } = 20.0;
    public double AverageSlopePct { get; set; }
    public double MaxSlopeDeltaPct { get; set; }
    public double GearRatioVariancePct { get; set; }
    public int DetectedGear { get; set; } = 3;
    public string QualityRating { get; set; } = "EXCELLENT"; // EXCELLENT, GOOD, MARGINAL, REJECTED
    public List<string> Warnings { get; set; } = new();
}

public class AccelerationPerformanceMetrics
{
    public double? ZeroToSixtyMphSec { get; set; }
    public double? ZeroToHundredKmhSec { get; set; }
    public double? SixtyToOneTwentyKmhSec { get; set; }
    public double? HundredToTwoHundredKmhSec { get; set; }
    public double? QuarterMileSec { get; set; }
    public double? QuarterMileTrapSpeedKmh { get; set; }
    public double? QuarterMileTrapSpeedMph { get; set; }
    public double? FiftyToEightyKmhSec { get; set; }
    public double? EightyToOneTwentyKmhSec { get; set; }
}

public class RoadDynoRun
{
    public string RunId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Road Dyno Run #1";
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public int Gear { get; set; } = 3;
    public AtmosphericStandard Standard { get; set; } = AtmosphericStandard.SAE_J1349;
    public double CorrectionFactor { get; set; } = 1.0;

    // Peak Wheel Metrics
    public double PeakWheelPowerHp { get; set; }
    public double PeakWheelPowerKw { get; set; }
    public double PeakWheelPowerRpm { get; set; }
    public double PeakWheelTorqueNm { get; set; }
    public double PeakWheelTorqueLbFt { get; set; }
    public double PeakWheelTorqueRpm { get; set; }

    // Peak Engine / Crank Metrics (Uncorrected)
    public double PeakEnginePowerHp { get; set; }
    public double PeakEnginePowerKw { get; set; }
    public double PeakEnginePowerRpm { get; set; }
    public double PeakEngineTorqueNm { get; set; }
    public double PeakEngineTorqueLbFt { get; set; }
    public double PeakEngineTorqueRpm { get; set; }

    // Peak Corrected Metrics
    public double PeakCorrectedPowerHp { get; set; }
    public double PeakCorrectedTorqueNm { get; set; }

    public double StartRpm { get; set; }
    public double EndRpm { get; set; }
    public double DurationSeconds { get; set; }

    public RunQualityMetrics Quality { get; set; } = new();
    public AccelerationPerformanceMetrics Performance { get; set; } = new();
    public List<RoadDynoDataPoint> Points { get; set; } = new();

    public VehicleProfile? VehicleSnapshot { get; set; }
    public AmbientConditions? AmbientSnapshot { get; set; }
    public CoastDownProfile? CoastDown { get; set; }

    /// <summary>
    /// Bridges the measurement-based RoadDynoRun into the canonical DynoPullSummary model.
    /// </summary>
    public DynoPullSummary ToDynoPullSummary()
    {
        return new DynoPullSummary
        {
            PullId = RunId,
            Name = Name,
            TimestampUtc = TimestampUtc,
            Type = "ROAD_MEASURED",
            PeakPowerHp = PeakCorrectedPowerHp > 0 ? PeakCorrectedPowerHp : PeakEnginePowerHp,
            PeakPowerKw = (PeakCorrectedPowerHp > 0 ? PeakCorrectedPowerHp : PeakEnginePowerHp) * 0.745699872,
            PeakPowerRpm = PeakEnginePowerRpm,
            PeakTorqueNm = PeakCorrectedTorqueNm > 0 ? PeakCorrectedTorqueNm : PeakEngineTorqueNm,
            PeakTorqueLbFt = (PeakCorrectedTorqueNm > 0 ? PeakCorrectedTorqueNm : PeakEngineTorqueNm) * 0.737562,
            PeakTorqueRpm = PeakEngineTorqueRpm,
            StartRpm = StartRpm,
            EndRpm = EndRpm,
            DurationSeconds = DurationSeconds,
            Points = Points.Select(p => new DynoDataPoint
            {
                TimestampMs = p.TimestampMs,
                Rpm = p.Rpm,
                VehicleSpeedKmh = p.SpeedKmh,
                PowerHp = p.CorrectedEnginePowerHp,
                PowerKw = p.CorrectedEnginePowerHp * 0.745699872,
                TorqueNm = p.CorrectedEngineTorqueNm,
                TorqueLbFt = p.CorrectedEngineTorqueNm * 0.737562
            }).ToList()
        };
    }

    /// <summary>
    /// Bridges road acceleration metrics into the canonical VehiclePerformanceSummary.
    /// </summary>
    public VehiclePerformanceSummary ToVehiclePerformanceSummary()
    {
        return new VehiclePerformanceSummary
        {
            ZeroToSixtyMphSeconds = Performance.ZeroToSixtyMphSec ?? 0.0,
            ZeroToHundredKmhSeconds = Performance.ZeroToHundredKmhSec ?? 0.0,
            SixtyToHundredKmhSeconds = Performance.SixtyToOneTwentyKmhSec ?? 0.0,
            EightyToOneTwentyKmhSeconds = Performance.EightyToOneTwentyKmhSec ?? 0.0,
            QuarterMileSeconds = Performance.QuarterMileSec ?? 0.0,
            QuarterMileSpeedKmh = Performance.QuarterMileTrapSpeedKmh ?? 0.0,
            SourceType = "ROAD_MEASURED"
        };
    }
}

public class DynoRunComparisonResult
{
    public string BaselineRunId { get; set; } = string.Empty;
    public string BaselineRunName { get; set; } = "Stock Baseline";
    public string TestRunId { get; set; } = string.Empty;
    public string TestRunName { get; set; } = "Tuned Stage 1";

    public List<double> AlignedRpm { get; set; } = new();

    // Power comparison (HP)
    public List<double> BaselinePowerHp { get; set; } = new();
    public List<double> TestPowerHp { get; set; } = new();
    public List<double> DeltaPowerHp { get; set; } = new();
    public List<double> DeltaPowerPct { get; set; } = new();

    // Torque comparison (Nm)
    public List<double> BaselineTorqueNm { get; set; } = new();
    public List<double> TestTorqueNm { get; set; } = new();
    public List<double> DeltaTorqueNm { get; set; } = new();
    public List<double> DeltaTorquePct { get; set; } = new();

    // Peak Summaries
    public double PeakPowerDeltaHp { get; set; }
    public double PeakPowerDeltaPct { get; set; }
    public double PeakTorqueDeltaNm { get; set; }
    public double PeakTorqueDeltaPct { get; set; }

    // Acceleration Deltas
    public double? ZeroToSixtyDeltaSec { get; set; }
    public double? ZeroToHundredDeltaSec { get; set; }
    public double? QuarterMileDeltaSec { get; set; }
    public double? QuarterMileTrapSpeedDeltaKmh { get; set; }
}
