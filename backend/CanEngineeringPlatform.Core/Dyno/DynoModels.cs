using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Dyno;

public class DynoDataPoint
{
    public double Rpm { get; set; }
    public double TorqueNm { get; set; }
    public double TorqueLbFt { get; set; }
    public double PowerKw { get; set; }
    public double PowerHp { get; set; }
    public double BoostBar { get; set; }
    public double Afr { get; set; }
    public double EngineLoadPct { get; set; }
    public double VehicleSpeedKmh { get; set; }
    public double TimestampMs { get; set; }
}

public class DynoPullSummary
{
    public string PullId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Dyno Pull #1";
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = "SIMULATED"; // "SIMULATED", "MEASURED", "CALCULATED"
    public double PeakTorqueNm { get; set; }
    public double PeakTorqueLbFt { get; set; }
    public double PeakTorqueRpm { get; set; }
    public double PeakPowerKw { get; set; }
    public double PeakPowerHp { get; set; }
    public double PeakPowerRpm { get; set; }
    public double StartRpm { get; set; }
    public double EndRpm { get; set; }
    public double DurationSeconds { get; set; }
    public List<DynoDataPoint> Points { get; set; } = new();
}

public class VehiclePerformanceSummary
{
    public double ZeroToSixtyMphSeconds { get; set; } = 5.8;
    public double ZeroToHundredKmhSeconds { get; set; } = 6.1;
    public double SixtyToHundredKmhSeconds { get; set; } = 3.2;
    public double EightyToOneTwentyKmhSeconds { get; set; } = 4.1;
    public double QuarterMileSeconds { get; set; } = 14.2;
    public double QuarterMileSpeedKmh { get; set; } = 162.5;
    public string SourceType { get; set; } = "SIMULATED";
}
