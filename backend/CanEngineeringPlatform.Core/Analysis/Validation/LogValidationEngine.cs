using System;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Analysis.TimeSeries;

namespace CanEngineeringPlatform.Core.Analysis.Validation;

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

public enum ValidationIssueType
{
    MissingSamples,
    TimestampJumps,
    DuplicateTimestamps,
    SpikesOutliers,
    ImpossibleValues,
    NoiseJitter
}

public class LogValidationIssue
{
    public ValidationIssueType Type { get; set; }
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Warning;
    public string ChannelName { get; set; } = string.Empty;
    public double TimestampMs { get; set; }
    public string Description { get; set; } = string.Empty;
    public double? ObservedValue { get; set; }
    public string? ExpectedRange { get; set; }
}

public class LogValidationReport
{
    public int TotalChannels { get; set; }
    public long TotalSamples { get; set; }
    public double DurationMs { get; set; }
    public List<LogValidationIssue> Issues { get; set; } = new();

    public int MissingGapsCount => Issues.Count(i => i.Type == ValidationIssueType.MissingSamples);
    public int TimestampJumpsCount => Issues.Count(i => i.Type == ValidationIssueType.TimestampJumps);
    public int DuplicatesCount => Issues.Count(i => i.Type == ValidationIssueType.DuplicateTimestamps);
    public int SpikesCount => Issues.Count(i => i.Type == ValidationIssueType.SpikesOutliers);
    public int ImpossibleValuesCount => Issues.Count(i => i.Type == ValidationIssueType.ImpossibleValues);

    public Dictionary<string, double> ChannelNoiseScore { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsValid => !Issues.Any(i => i.Severity == ValidationSeverity.Error);
    public string Summary { get; set; } = string.Empty;
}

public class LogValidationEngine
{
    // Automotive physical plausibility limits
    private static readonly Dictionary<string, (double Min, double Max, double MaxRatePerMs)> ChannelLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rpm"] = (0.0, 12000.0, 50.0),                     // max 50 RPM per ms (50,000 RPM/sec)
        ["enginespeed"] = (0.0, 12000.0, 50.0),
        ["speed"] = (0.0, 450.0, 0.1),                      // max 100 km/h per sec = 0.1 km/h per ms
        ["vehiclespeed"] = (0.0, 450.0, 0.1),
        ["boost"] = (-1.5, 6.0, 0.05),                      // max 50 bar/sec
        ["boostbar"] = (-1.5, 6.0, 0.05),
        ["coolant"] = (-40.0, 150.0, 0.01),                 // thermal cannot change > 10 C/sec
        ["coolanttemp"] = (-40.0, 150.0, 0.01),
        ["oiltemp"] = (-40.0, 160.0, 0.01),
        ["throttle"] = (0.0, 105.0, 1.0),                   // max 100% in 100ms
        ["throttleposition"] = (0.0, 105.0, 1.0),
        ["afr"] = (6.0, 25.0, 0.1),
        ["lambda"] = (0.5, 2.0, 0.01)
    };

    public LogValidationReport Validate(CanonicalLogDataSet dataset)
    {
        if (dataset == null) throw new ArgumentNullException(nameof(dataset));

        var report = new LogValidationReport
        {
            TotalChannels = dataset.Channels.Count
        };

        long totalSamples = 0;
        double minTime = double.MaxValue;
        double maxTime = double.MinValue;

        foreach (var kvp in dataset.Channels)
        {
            string chanName = kvp.Key;
            var chan = kvp.Value;
            totalSamples += chan.SampleCount;

            if (chan.SampleCount == 0) continue;

            if (chan.TimestampsMs.First() < minTime) minTime = chan.TimestampsMs.First();
            if (chan.TimestampsMs.Last() > maxTime) maxTime = chan.TimestampsMs.Last();

            ValidateChannel(chan, report);
        }

        report.TotalSamples = totalSamples;
        report.DurationMs = (maxTime > minTime && minTime != double.MaxValue) ? (maxTime - minTime) : 0.0;

        int totalErrors = report.Issues.Count(i => i.Severity == ValidationSeverity.Error);
        int totalWarnings = report.Issues.Count(i => i.Severity == ValidationSeverity.Warning);

        report.Summary = totalErrors == 0 && totalWarnings == 0
            ? "Log validation passed cleanly with 0 anomalies."
            : $"Validation detected {totalErrors} errors and {totalWarnings} warnings across {report.TotalChannels} channels.";

        return report;
    }

    private void ValidateChannel(TimeSeriesChannel chan, LogValidationReport report)
    {
        if (chan.SampleCount < 2) return;

        var limits = FindLimitsForChannel(chan.Name);
        double totalNoise = 0.0;
        int noiseCount = 0;

        for (int i = 0; i < chan.SampleCount; i++)
        {
            double t = chan.TimestampsMs[i];
            double v = chan.Values[i];

            // 1. Impossible physical values
            if (limits.HasValue)
            {
                if (v < limits.Value.Min || v > limits.Value.Max)
                {
                    report.Issues.Add(new LogValidationIssue
                    {
                        Type = ValidationIssueType.ImpossibleValues,
                        Severity = ValidationSeverity.Error,
                        ChannelName = chan.Name,
                        TimestampMs = t,
                        ObservedValue = v,
                        ExpectedRange = $"[{limits.Value.Min}..{limits.Value.Max}]",
                        Description = $"Channel '{chan.Name}' value {v} exceeds physical limits [{limits.Value.Min}..{limits.Value.Max}]."
                    });
                }
            }

            if (i > 0)
            {
                double prevT = chan.TimestampsMs[i - 1];
                double prevV = chan.Values[i - 1];
                double dt = t - prevT;

                // 2. Timestamp jumps (backwards time or huge gap)
                if (dt < 0)
                {
                    report.Issues.Add(new LogValidationIssue
                    {
                        Type = ValidationIssueType.TimestampJumps,
                        Severity = ValidationSeverity.Error,
                        ChannelName = chan.Name,
                        TimestampMs = t,
                        Description = $"Negative time delta: timestamp went backwards from {prevT} ms to {t} ms (dt = {dt} ms)."
                    });
                }
                else if (dt == 0)
                {
                    // 3. Duplicate timestamp
                    report.Issues.Add(new LogValidationIssue
                    {
                        Type = ValidationIssueType.DuplicateTimestamps,
                        Severity = ValidationSeverity.Warning,
                        ChannelName = chan.Name,
                        TimestampMs = t,
                        Description = $"Duplicate timestamp at {t} ms."
                    });
                }
                else if (dt > 250.0) // Gap larger than 250ms in continuous automotive stream
                {
                    report.Issues.Add(new LogValidationIssue
                    {
                        Type = ValidationIssueType.MissingSamples,
                        Severity = ValidationSeverity.Warning,
                        ChannelName = chan.Name,
                        TimestampMs = prevT,
                        Description = $"Missing sample gap: {dt:F0} ms interval between {prevT} ms and {t} ms."
                    });
                }

                // 4. Spike & Outlier detection (rate of change)
                if (dt > 0 && limits.HasValue)
                {
                    double dv = Math.Abs(v - prevV);
                    double ratePerMs = dv / dt;
                    if (ratePerMs > limits.Value.MaxRatePerMs)
                    {
                        report.Issues.Add(new LogValidationIssue
                        {
                            Type = ValidationIssueType.SpikesOutliers,
                            Severity = ValidationSeverity.Warning,
                            ChannelName = chan.Name,
                            TimestampMs = t,
                            ObservedValue = v,
                            Description = $"Step spike on '{chan.Name}': rate {ratePerMs:F1}/ms exceeds plausible gradient {limits.Value.MaxRatePerMs:F1}/ms."
                        });
                    }
                }

                // 5. High-frequency noise accumulation
                if (i > 1 && dt > 0)
                {
                    double prev2V = chan.Values[i - 2];
                    double secondDiff = (v - 2 * prevV + prev2V);
                    totalNoise += Math.Abs(secondDiff);
                    noiseCount++;
                }
            }
        }

        double avgNoise = noiseCount > 0 ? (totalNoise / noiseCount) : 0.0;
        report.ChannelNoiseScore[chan.Name] = Math.Round(avgNoise, 3);
    }

    private static (double Min, double Max, double MaxRatePerMs)? FindLimitsForChannel(string name)
    {
        foreach (var kvp in ChannelLimits)
        {
            if (name.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }
        return null;
    }
}
