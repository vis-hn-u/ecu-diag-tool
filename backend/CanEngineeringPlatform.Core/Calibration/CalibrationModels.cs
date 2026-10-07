using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Calibration;

public enum CalibrationMapType
{
    VolumetricEfficiency,
    IgnitionTiming,
    TargetLambda,
    BoostTarget,
    TorqueLimiter,
    IdleSpeedTarget,
    VariableValveTiming
}

public class CalibrationAxis
{
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double[] Breakpoints { get; set; } = Array.Empty<double>();
}

public class CalibrationTable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CalibrationMapType Type { get; set; }
    public CalibrationAxis XAxis { get; set; } = new();
    public CalibrationAxis? YAxis { get; set; }
    public double[][] Data { get; set; } = Array.Empty<double[]>();
    public double[][] OriginalData { get; set; } = Array.Empty<double[]>();
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class CalibrationDiffCell
{
    public int XIndex { get; set; }
    public int YIndex { get; set; }
    public double XBreakpoint { get; set; }
    public double YBreakpoint { get; set; }
    public double OriginalValue { get; set; }
    public double ModifiedValue { get; set; }
    public double AbsoluteDiff { get; set; }
    public double PercentDiff { get; set; }
    public string State { get; set; } = "UNCHANGED"; // "UNCHANGED", "INCREASED", "DECREASED"
}

public class CalibrationEditRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TableId { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public int XIndex { get; set; }
    public int YIndex { get; set; }
    public double PreviousValue { get; set; }
    public double NewValue { get; set; }
    public string Operation { get; set; } = "Set";
}

public class CalibrationProject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Stage 1 Performance Calibration";
    public string VehicleModel { get; set; } = "Swift Diesel 1.3 DDiS";
    public string EcuType { get; set; } = "Magneti Marelli MJD";
    public string Version { get; set; } = "v1.2.0-tune";
    public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
    public List<CalibrationTable> Tables { get; set; } = new();
}
