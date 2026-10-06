using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.Calibration;

public interface ICalibrationService
{
    CalibrationProject GetProject();
    CalibrationTable? GetTable(string tableId);
    bool UpdateCell(string tableId, int xIndex, int yIndex, double newValue, string operation = "Edit");
    bool BulkOffset(string tableId, double offsetValue);
    bool BulkScale(string tableId, double scalePercent);
    bool Undo(string tableId);
    bool Redo(string tableId);
    IReadOnlyList<CalibrationDiffCell> ComputeDiff(string tableId);
    IReadOnlyList<CalibrationEditRecord> GetEditHistory(string tableId);
    void ResetTableToOriginal(string tableId);
    void ResetAllToStock();
}

public class CalibrationService : ICalibrationService
{
    private readonly CalibrationProject _project = new();
    private readonly ConcurrentDictionary<string, Stack<CalibrationEditRecord>> _undoStacks = new();
    private readonly ConcurrentDictionary<string, Stack<CalibrationEditRecord>> _redoStacks = new();
    private readonly ConcurrentDictionary<string, List<CalibrationEditRecord>> _historyLogs = new();

    public CalibrationService()
    {
        InitializeStockCalibration();
    }

    public void InitializeStockCalibration()
    {
        _project.Tables.Clear();
        _undoStacks.Clear();
        _redoStacks.Clear();
        _historyLogs.Clear();

        // 1. Ignition Timing Map (Load % vs RPM -> deg BTDC)
        var sparkMap = CreateStockIgnitionTimingMap();
        _project.Tables.Add(sparkMap);

        // 2. Volumetric Efficiency Fuel Map (Load % vs RPM -> VE %)
        var veMap = CreateStockVeFuelMap();
        _project.Tables.Add(veMap);

        // 3. Target Lambda Map (Load % vs RPM -> Lambda)
        var lambdaMap = CreateStockLambdaMap();
        _project.Tables.Add(lambdaMap);

        // 4. Boost Target Map (TPS % vs RPM -> Boost Bar)
        var boostMap = CreateStockBoostTargetMap();
        _project.Tables.Add(boostMap);

        // 5. Torque Limiter Map (Gear vs RPM -> Max Nm)
        var torqueMap = CreateStockTorqueLimiterMap();
        _project.Tables.Add(torqueMap);
    }

    private CalibrationTable CreateStockIgnitionTimingMap()
    {
        double[] rpmBp = { 800, 1200, 1600, 2000, 2400, 2800, 3200, 3600, 4000, 4400, 4800, 5200, 5600, 6000, 6400, 6800 };
        double[] loadBp = { 20, 40, 60, 80, 100, 120, 140, 160, 180, 200 };

        double[][] data = new double[loadBp.Length][];
        double[][] orig = new double[loadBp.Length][];

        for (int y = 0; y < loadBp.Length; y++)
        {
            data[y] = new double[rpmBp.Length];
            orig[y] = new double[rpmBp.Length];
            double load = loadBp[y];

            for (int x = 0; x < rpmBp.Length; x++)
            {
                double rpm = rpmBp[x];
                // Base formula: timing increases with RPM, decreases with Load
                double deg = Math.Round(8.0 + (rpm / 220.0) - (load / 12.0), 1);
                deg = Math.Clamp(deg, 4.0, 38.0);
                data[y][x] = deg;
                orig[y][x] = deg;
            }
        }

        return new CalibrationTable
        {
            Id = "spark_timing_main",
            Name = "Ignition Timing Advance (Main)",
            Description = "Base spark advance angle in degrees Before Top Dead Center (BTDC)",
            Type = CalibrationMapType.IgnitionTiming,
            Unit = "° BTDC",
            MinValue = 0.0,
            MaxValue = 45.0,
            XAxis = new CalibrationAxis { Name = "Engine Speed", Unit = "rpm", Breakpoints = rpmBp },
            YAxis = new CalibrationAxis { Name = "Engine Load", Unit = "%", Breakpoints = loadBp },
            Data = data,
            OriginalData = orig
        };
    }

    private CalibrationTable CreateStockVeFuelMap()
    {
        double[] rpmBp = { 800, 1200, 1600, 2000, 2500, 3000, 3500, 4000, 4500, 5000, 5500, 6000, 6500, 7000 };
        double[] loadBp = { 20, 40, 60, 80, 100, 120, 140, 160, 180, 200 };

        double[][] data = new double[loadBp.Length][];
        double[][] orig = new double[loadBp.Length][];

        for (int y = 0; y < loadBp.Length; y++)
        {
            data[y] = new double[rpmBp.Length];
            orig[y] = new double[rpmBp.Length];
            double load = loadBp[y];

            for (int x = 0; x < rpmBp.Length; x++)
            {
                double rpm = rpmBp[x];
                double rpmNorm = rpm / 4500.0;
                double ve = Math.Round(50.0 + 40.0 * Math.Exp(-0.5 * Math.Pow((rpmNorm - 1.0) / 0.5, 2)) + (load / 10.0), 1);
                ve = Math.Clamp(ve, 40.0, 115.0);
                data[y][x] = ve;
                orig[y][x] = ve;
            }
        }

        return new CalibrationTable
        {
            Id = "ve_fuel_main",
            Name = "Volumetric Efficiency (VE)",
            Description = "Primary cylinder filling volumetric efficiency percentage",
            Type = CalibrationMapType.VolumetricEfficiency,
            Unit = "%",
            MinValue = 20.0,
            MaxValue = 130.0,
            XAxis = new CalibrationAxis { Name = "Engine Speed", Unit = "rpm", Breakpoints = rpmBp },
            YAxis = new CalibrationAxis { Name = "Engine Load", Unit = "%", Breakpoints = loadBp },
            Data = data,
            OriginalData = orig
        };
    }

    private CalibrationTable CreateStockLambdaMap()
    {
        double[] rpmBp = { 1000, 2000, 3000, 4000, 5000, 6000, 7000 };
        double[] loadBp = { 20, 50, 80, 100, 130, 160, 200 };

        double[][] data = new double[loadBp.Length][];
        double[][] orig = new double[loadBp.Length][];

        for (int y = 0; y < loadBp.Length; y++)
        {
            data[y] = new double[rpmBp.Length];
            orig[y] = new double[rpmBp.Length];
            double load = loadBp[y];

            for (int x = 0; x < rpmBp.Length; x++)
            {
                double lam = (load <= 80) ? 1.0 : Math.Round(1.0 - ((load - 80.0) / 120.0) * 0.20, 2);
                data[y][x] = lam;
                orig[y][x] = lam;
            }
        }

        return new CalibrationTable
        {
            Id = "target_lambda_main",
            Name = "Target Air-Fuel Lambda",
            Description = "Closed-loop / Open-loop target equivalent equivalence ratio (1.0 = 14.7 AFR)",
            Type = CalibrationMapType.TargetLambda,
            Unit = "λ",
            MinValue = 0.70,
            MaxValue = 1.15,
            XAxis = new CalibrationAxis { Name = "Engine Speed", Unit = "rpm", Breakpoints = rpmBp },
            YAxis = new CalibrationAxis { Name = "Engine Load", Unit = "%", Breakpoints = loadBp },
            Data = data,
            OriginalData = orig
        };
    }

    private CalibrationTable CreateStockBoostTargetMap()
    {
        double[] rpmBp = { 1500, 2000, 2500, 3000, 3500, 4000, 4500, 5000, 5500, 6000, 6500 };
        double[] tpsBp = { 20, 40, 60, 80, 100 };

        double[][] data = new double[tpsBp.Length][];
        double[][] orig = new double[tpsBp.Length][];

        for (int y = 0; y < tpsBp.Length; y++)
        {
            data[y] = new double[rpmBp.Length];
            orig[y] = new double[rpmBp.Length];
            double tps = tpsBp[y];

            for (int x = 0; x < rpmBp.Length; x++)
            {
                double rpm = rpmBp[x];
                double boost = (tps < 40) ? 0.0 : Math.Round(((tps - 40.0) / 60.0) * 1.35 * Math.Clamp((rpm - 1800) / 1000.0, 0, 1), 2);
                data[y][x] = boost;
                orig[y][x] = boost;
            }
        }

        return new CalibrationTable
        {
            Id = "boost_target_main",
            Name = "Boost Pressure Target",
            Description = "Target manifold gauge boost pressure in Bar relative to atmospheric",
            Type = CalibrationMapType.BoostTarget,
            Unit = "bar",
            MinValue = 0.0,
            MaxValue = 2.5,
            XAxis = new CalibrationAxis { Name = "Engine Speed", Unit = "rpm", Breakpoints = rpmBp },
            YAxis = new CalibrationAxis { Name = "Throttle Angle", Unit = "%", Breakpoints = tpsBp },
            Data = data,
            OriginalData = orig
        };
    }

    private CalibrationTable CreateStockTorqueLimiterMap()
    {
        double[] rpmBp = { 1000, 2000, 3000, 4000, 5000, 6000, 7000 };
        double[] gearBp = { 1, 2, 3, 4, 5, 6 };

        double[][] data = new double[gearBp.Length][];
        double[][] orig = new double[gearBp.Length][];

        for (int y = 0; y < gearBp.Length; y++)
        {
            data[y] = new double[rpmBp.Length];
            orig[y] = new double[rpmBp.Length];
            int gear = (int)gearBp[y];

            for (int x = 0; x < rpmBp.Length; x++)
            {
                double tq = (gear == 1) ? 280.0 : (gear == 2) ? 340.0 : 380.0;
                data[y][x] = tq;
                orig[y][x] = tq;
            }
        }

        return new CalibrationTable
        {
            Id = "torque_limiter_gear",
            Name = "Maximum Torque Limiter",
            Description = "Engine output torque ceiling by transmission gear",
            Type = CalibrationMapType.TorqueLimiter,
            Unit = "Nm",
            MinValue = 100.0,
            MaxValue = 600.0,
            XAxis = new CalibrationAxis { Name = "Engine Speed", Unit = "rpm", Breakpoints = rpmBp },
            YAxis = new CalibrationAxis { Name = "Gear", Unit = "gear", Breakpoints = gearBp },
            Data = data,
            OriginalData = orig
        };
    }

    public CalibrationProject GetProject() => _project;

    public CalibrationTable? GetTable(string tableId)
    {
        return _project.Tables.FirstOrDefault(t => t.Id == tableId);
    }

    public bool UpdateCell(string tableId, int xIndex, int yIndex, double newValue, string operation = "Edit")
    {
        var table = GetTable(tableId);
        if (table == null || yIndex < 0 || yIndex >= table.Data.Length || xIndex < 0 || xIndex >= table.Data[yIndex].Length)
        {
            return false;
        }

        double prev = table.Data[yIndex][xIndex];
        double clamped = Math.Clamp(newValue, table.MinValue, table.MaxValue);
        table.Data[yIndex][xIndex] = Math.Round(clamped, 2);

        var record = new CalibrationEditRecord
        {
            TableId = tableId,
            XIndex = xIndex,
            YIndex = yIndex,
            PreviousValue = prev,
            NewValue = clamped,
            Operation = operation,
            TimestampUtc = DateTime.UtcNow
        };

        var undoStack = _undoStacks.GetOrAdd(tableId, _ => new Stack<CalibrationEditRecord>());
        undoStack.Push(record);

        var history = _historyLogs.GetOrAdd(tableId, _ => new List<CalibrationEditRecord>());
        history.Add(record);

        _project.LastModifiedUtc = DateTime.UtcNow;
        return true;
    }

    public bool BulkOffset(string tableId, double offsetValue)
    {
        var table = GetTable(tableId);
        if (table == null) return false;

        for (int y = 0; y < table.Data.Length; y++)
        {
            for (int x = 0; x < table.Data[y].Length; x++)
            {
                UpdateCell(tableId, x, y, table.Data[y][x] + offsetValue, $"Offset {offsetValue:+#.##;-#.##}");
            }
        }
        return true;
    }

    public bool BulkScale(string tableId, double scalePercent)
    {
        var table = GetTable(tableId);
        if (table == null) return false;

        double factor = 1.0 + (scalePercent / 100.0);
        for (int y = 0; y < table.Data.Length; y++)
        {
            for (int x = 0; x < table.Data[y].Length; x++)
            {
                UpdateCell(tableId, x, y, table.Data[y][x] * factor, $"Scale {scalePercent:+#.##;-#.##}%");
            }
        }
        return true;
    }

    public bool Undo(string tableId)
    {
        if (!_undoStacks.TryGetValue(tableId, out var undoStack) || undoStack.Count == 0)
        {
            return false;
        }

        var record = undoStack.Pop();
        var table = GetTable(tableId);
        if (table != null)
        {
            table.Data[record.YIndex][record.XIndex] = record.PreviousValue;

            var redoStack = _redoStacks.GetOrAdd(tableId, _ => new Stack<CalibrationEditRecord>());
            redoStack.Push(record);
            return true;
        }

        return false;
    }

    public bool Redo(string tableId)
    {
        if (!_redoStacks.TryGetValue(tableId, out var redoStack) || redoStack.Count == 0)
        {
            return false;
        }

        var record = redoStack.Pop();
        var table = GetTable(tableId);
        if (table != null)
        {
            table.Data[record.YIndex][record.XIndex] = record.NewValue;

            var undoStack = _undoStacks.GetOrAdd(tableId, _ => new Stack<CalibrationEditRecord>());
            undoStack.Push(record);
            return true;
        }

        return false;
    }

    public IReadOnlyList<CalibrationDiffCell> ComputeDiff(string tableId)
    {
        var table = GetTable(tableId);
        var diffList = new List<CalibrationDiffCell>();
        if (table == null) return diffList;

        for (int y = 0; y < table.Data.Length; y++)
        {
            double yBp = table.YAxis != null && y < table.YAxis.Breakpoints.Length ? table.YAxis.Breakpoints[y] : y;
            for (int x = 0; x < table.Data[y].Length; x++)
            {
                double xBp = x < table.XAxis.Breakpoints.Length ? table.XAxis.Breakpoints[x] : x;
                double mod = table.Data[y][x];
                double orig = table.OriginalData[y][x];
                double absDiff = Math.Round(mod - orig, 2);
                double pctDiff = orig != 0 ? Math.Round((absDiff / Math.Abs(orig)) * 100.0, 1) : 0;

                string state = "UNCHANGED";
                if (absDiff > 0.001) state = "INCREASED";
                else if (absDiff < -0.001) state = "DECREASED";

                diffList.Add(new CalibrationDiffCell
                {
                    XIndex = x,
                    YIndex = y,
                    XBreakpoint = xBp,
                    YBreakpoint = yBp,
                    OriginalValue = orig,
                    ModifiedValue = mod,
                    AbsoluteDiff = absDiff,
                    PercentDiff = pctDiff,
                    State = state
                });
            }
        }

        return diffList;
    }

    public IReadOnlyList<CalibrationEditRecord> GetEditHistory(string tableId)
    {
        if (_historyLogs.TryGetValue(tableId, out var list))
        {
            return list.ToList();
        }
        return Array.Empty<CalibrationEditRecord>();
    }

    public void ResetTableToOriginal(string tableId)
    {
        var table = GetTable(tableId);
        if (table == null) return;

        for (int y = 0; y < table.OriginalData.Length; y++)
        {
            for (int x = 0; x < table.OriginalData[y].Length; x++)
            {
                table.Data[y][x] = table.OriginalData[y][x];
            }
        }
        _undoStacks.TryRemove(tableId, out _);
        _redoStacks.TryRemove(tableId, out _);
    }

    public void ResetAllToStock()
    {
        InitializeStockCalibration();
    }
}
