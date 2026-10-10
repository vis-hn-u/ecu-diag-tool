using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CanEngineeringPlatform.Core.Signals;

namespace CanEngineeringPlatform.Core.Analysis.OperatingEnvelope;

public class OperatingGridConfig
{
    public string GridId { get; set; } = "rpm_vs_load_lambda";
    public string Name { get; set; } = "RPM x Load -> Lambda Operating Map";
    public string Description { get; set; } = "Observed operating grid correlating combustion lambda with engine speed and load";
    public string XSignalName { get; set; } = "EngineRPM";
    public double[] XBinEdges { get; set; } = new double[] { 800, 1200, 1600, 2000, 2500, 3000, 3500, 4000, 4500, 5000, 5500, 6000 };
    public string YSignalName { get; set; } = "EngineLoad";
    public double[] YBinEdges { get; set; } = new double[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
    public string ZSignalName { get; set; } = "Lambda";
    public string ZUnit { get; set; } = "λ";
}

public class OperatingCellBucket
{
    public int XIndex { get; set; }
    public int YIndex { get; set; }
    public double XCenter { get; set; }
    public double YCenter { get; set; }
    public long SampleCount { get; set; }
    public double? Mean { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? StdDev { get; set; }
    public double? Latest { get; set; }
    public bool HasData => SampleCount > 0;

    // Internal running accumulator for Welford's algorithm (numerically stable mean & variance)
    private double _m2;

    public void AddSample(double val)
    {
        SampleCount++;
        Latest = val;
        if (!Min.HasValue || val < Min.Value) Min = val;
        if (!Max.HasValue || val > Max.Value) Max = val;

        if (SampleCount == 1)
        {
            Mean = val;
            _m2 = 0.0;
            StdDev = 0.0;
        }
        else
        {
            double delta = val - Mean!.Value;
            Mean += delta / SampleCount;
            double delta2 = val - Mean.Value;
            _m2 += delta * delta2;
            StdDev = Math.Sqrt(_m2 / (SampleCount - 1));
        }
    }

    public void Reset()
    {
        SampleCount = 0;
        Mean = null;
        Min = null;
        Max = null;
        StdDev = null;
        Latest = null;
        _m2 = 0.0;
    }
}

public class LiveOperatingGridSnapshot
{
    public OperatingGridConfig Config { get; set; } = new();
    public long TotalSamples { get; set; }
    public int ActiveCellCount { get; set; }
    public OperatingCellBucket[][] Cells { get; set; } = Array.Empty<OperatingCellBucket[]>();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Disclaimer { get; set; } = "LIVE TELEMETRY OPERATING ENVELOPE ONLY — NOT AN ECU CALIBRATION MAP OR FLASH TABLE";
}

public interface ILiveOperatingGridService
{
    LiveOperatingGridSnapshot GetSnapshot(string gridId = "rpm_vs_load_lambda");
    void ConfigureGrid(OperatingGridConfig config);
    void IngestPoint(string gridId, double xVal, double yVal, double zVal);
    void ResetGrid(string gridId = "rpm_vs_load_lambda");
    string ExportToCsv(string gridId = "rpm_vs_load_lambda");
    IReadOnlyList<OperatingGridConfig> GetAvailableConfigs();
}

public class LiveOperatingGridService : ILiveOperatingGridService
{
    private readonly IUnifiedSignalRegistry? _signalRegistry;
    private readonly ConcurrentDictionary<string, OperatingGridConfig> _configs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, OperatingCellBucket[][]> _gridBuckets = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public LiveOperatingGridService(IUnifiedSignalRegistry? signalRegistry = null)
    {
        _signalRegistry = signalRegistry;

        // Register default presets
        RegisterDefaultConfigs();

        // If signal registry is present, hook into updates
        if (_signalRegistry != null)
        {
            _signalRegistry.OnBatchUpdated += HandleSignalBatch;
        }
    }

    private void RegisterDefaultConfigs()
    {
        // Preset 1: RPM x EngineLoad -> Lambda
        ConfigureGrid(new OperatingGridConfig
        {
            GridId = "rpm_vs_load_lambda",
            Name = "RPM x Load -> Lambda Operating Map",
            Description = "Combustion stoichiometry telemetry across operating speed and load",
            XSignalName = "EngineRPM",
            XBinEdges = new double[] { 800, 1200, 1600, 2000, 2500, 3000, 3500, 4000, 4500, 5000, 5500, 6000 },
            YSignalName = "EngineLoad",
            YBinEdges = new double[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 },
            ZSignalName = "Lambda",
            ZUnit = "λ"
        });

        // Preset 2: RPM x Boost -> Fuel Rail Pressure
        ConfigureGrid(new OperatingGridConfig
        {
            GridId = "rpm_vs_boost_railpressure",
            Name = "RPM x Boost -> Rail Pressure Map",
            Description = "Fuel rail pressure demand envelope across boost and engine speed",
            XSignalName = "EngineRPM",
            XBinEdges = new double[] { 1000, 1500, 2000, 2500, 3000, 3500, 4000, 4500 },
            YSignalName = "BoostPressure",
            YBinEdges = new double[] { 0.2, 0.4, 0.6, 0.8, 1.0, 1.2, 1.4, 1.6, 1.8, 2.0 },
            ZSignalName = "FuelRailPressure",
            ZUnit = "bar"
        });

        // Preset 3: RPM x Throttle -> Engine Torque
        ConfigureGrid(new OperatingGridConfig
        {
            GridId = "rpm_vs_tps_torque",
            Name = "RPM x TPS -> Engine Torque Map",
            Description = "Observed flywheel torque delivery against throttle demand",
            XSignalName = "EngineRPM",
            XBinEdges = new double[] { 800, 1500, 2200, 2900, 3600, 4300, 5000 },
            YSignalName = "ThrottlePosition",
            YBinEdges = new double[] { 10, 25, 50, 75, 100 },
            ZSignalName = "EngineTorque",
            ZUnit = "Nm"
        });
    }

    public void ConfigureGrid(OperatingGridConfig config)
    {
        lock (_lock)
        {
            _configs[config.GridId] = config;

            int yRows = config.YBinEdges.Length;
            int xCols = config.XBinEdges.Length;

            var grid = new OperatingCellBucket[yRows][];
            for (int y = 0; y < yRows; y++)
            {
                grid[y] = new OperatingCellBucket[xCols];
                for (int x = 0; x < xCols; x++)
                {
                    double xVal = config.XBinEdges[x];
                    double yVal = config.YBinEdges[y];
                    grid[y][x] = new OperatingCellBucket
                    {
                        XIndex = x,
                        YIndex = y,
                        XCenter = xVal,
                        YCenter = yVal
                    };
                }
            }
            _gridBuckets[config.GridId] = grid;
        }
    }

    public void IngestPoint(string gridId, double xVal, double yVal, double zVal)
    {
        if (!_configs.TryGetValue(gridId, out var config)) return;
        if (!_gridBuckets.TryGetValue(gridId, out var buckets)) return;

        int xIdx = FindBinIndex(xVal, config.XBinEdges);
        int yIdx = FindBinIndex(yVal, config.YBinEdges);

        if (xIdx >= 0 && xIdx < config.XBinEdges.Length && yIdx >= 0 && yIdx < config.YBinEdges.Length)
        {
            lock (buckets[yIdx][xIdx])
            {
                buckets[yIdx][xIdx].AddSample(zVal);
            }
        }
    }

    private void HandleSignalBatch(IReadOnlyList<CanonicalSignalRecord> records)
    {
        // Try ingesting into all active grids if relevant signals are present
        foreach (var kvp in _configs)
        {
            var config = kvp.Value;
            var xRec = records.FirstOrDefault(r => string.Equals(r.CanonicalId, config.XSignalName, StringComparison.OrdinalIgnoreCase));
            var yRec = records.FirstOrDefault(r => string.Equals(r.CanonicalId, config.YSignalName, StringComparison.OrdinalIgnoreCase));
            var zRec = records.FirstOrDefault(r => string.Equals(r.CanonicalId, config.ZSignalName, StringComparison.OrdinalIgnoreCase));

            if (xRec != null && yRec != null && zRec != null)
            {
                IngestPoint(config.GridId, xRec.Value, yRec.Value, zRec.Value);
            }
        }
    }

    private static int FindBinIndex(double val, double[] edges)
    {
        if (edges == null || edges.Length == 0) return -1;
        // Nearest bin center
        int bestIdx = 0;
        double bestDist = Math.Abs(val - edges[0]);
        for (int i = 1; i < edges.Length; i++)
        {
            double dist = Math.Abs(val - edges[i]);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    public LiveOperatingGridSnapshot GetSnapshot(string gridId = "rpm_vs_load_lambda")
    {
        if (!_configs.TryGetValue(gridId, out var config))
        {
            config = _configs.Values.FirstOrDefault() ?? new OperatingGridConfig();
        }

        if (!_gridBuckets.TryGetValue(config.GridId, out var buckets))
        {
            ConfigureGrid(config);
            buckets = _gridBuckets[config.GridId];
        }

        long total = 0;
        int active = 0;

        // Clone current cells to produce thread-safe snapshot
        int yRows = buckets.Length;
        var cloned = new OperatingCellBucket[yRows][];
        for (int y = 0; y < yRows; y++)
        {
            int xCols = buckets[y].Length;
            cloned[y] = new OperatingCellBucket[xCols];
            for (int x = 0; x < xCols; x++)
            {
                var src = buckets[y][x];
                lock (src)
                {
                    cloned[y][x] = new OperatingCellBucket
                    {
                        XIndex = src.XIndex,
                        YIndex = src.YIndex,
                        XCenter = src.XCenter,
                        YCenter = src.YCenter,
                        SampleCount = src.SampleCount,
                        Mean = src.Mean.HasValue ? Math.Round(src.Mean.Value, 3) : null,
                        Min = src.Min.HasValue ? Math.Round(src.Min.Value, 3) : null,
                        Max = src.Max.HasValue ? Math.Round(src.Max.Value, 3) : null,
                        StdDev = src.StdDev.HasValue ? Math.Round(src.StdDev.Value, 3) : null,
                        Latest = src.Latest.HasValue ? Math.Round(src.Latest.Value, 3) : null
                    };

                    total += src.SampleCount;
                    if (src.SampleCount > 0) active++;
                }
            }
        }

        return new LiveOperatingGridSnapshot
        {
            Config = config,
            TotalSamples = total,
            ActiveCellCount = active,
            Cells = cloned,
            TimestampUtc = DateTime.UtcNow
        };
    }

    public void ResetGrid(string gridId = "rpm_vs_load_lambda")
    {
        if (_gridBuckets.TryGetValue(gridId, out var buckets))
        {
            foreach (var row in buckets)
            {
                foreach (var cell in row)
                {
                    lock (cell)
                    {
                        cell.Reset();
                    }
                }
            }
        }
    }

    public string ExportToCsv(string gridId = "rpm_vs_load_lambda")
    {
        var snap = GetSnapshot(gridId);
        var sb = new StringBuilder();

        sb.AppendLine($"# {snap.Config.Name}");
        sb.AppendLine($"# {snap.Disclaimer}");
        sb.AppendLine($"# Exported at {snap.TimestampUtc:u}");
        sb.AppendLine($"# X: {snap.Config.XSignalName}, Y: {snap.Config.YSignalName}, Z: {snap.Config.ZSignalName} ({snap.Config.ZUnit})");

        // Header row: Y \ X_bins...
        sb.Append($"{snap.Config.YSignalName}\\{snap.Config.XSignalName},");
        sb.AppendLine(string.Join(",", snap.Config.XBinEdges.Select(x => x.ToString(CultureInfo.InvariantCulture))));

        for (int y = 0; y < snap.Cells.Length; y++)
        {
            double yVal = snap.Config.YBinEdges[y];
            sb.Append($"{yVal.ToString(CultureInfo.InvariantCulture)},");

            var cellVals = new List<string>();
            for (int x = 0; x < snap.Cells[y].Length; x++)
            {
                var c = snap.Cells[y][x];
                cellVals.Add(c.HasData ? c.Mean!.Value.ToString("F3", CultureInfo.InvariantCulture) : "NO DATA");
            }
            sb.AppendLine(string.Join(",", cellVals));
        }

        return sb.ToString();
    }

    public IReadOnlyList<OperatingGridConfig> GetAvailableConfigs() => _configs.Values.ToList();
}
