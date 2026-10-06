using System;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Analysis.Processing;
using CanEngineeringPlatform.Core.Analysis.TimeSeries;

namespace CanEngineeringPlatform.Core.Analysis.Comparison;

public enum ComparisonAlignmentMode
{
    TimeTriggered, // Align relative time (t=0 at trigger or pull start)
    RpmDomain,     // Resample signals across uniform RPM grid (X-axis = RPM)
    SpeedDomain    // Resample signals across uniform Speed grid (X-axis = Speed)
}

public class ChannelComparisonResult
{
    public string ChannelName { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public List<double> AlignedAxisValues { get; set; } = new();
    public List<double> RunAValues { get; set; } = new();
    public List<double> RunBValues { get; set; } = new();
    public List<double> DeltaValues { get; set; } = new();
    public List<double> DeltaPercent { get; set; } = new();

    public double PeakRunA { get; set; }
    public double PeakRunB { get; set; }
    public double PeakDelta => PeakRunB - PeakRunA;
    public double PeakDeltaPercent => PeakRunA != 0.0 ? (PeakDelta / Math.Abs(PeakRunA)) * 100.0 : 0.0;
    public double MeanDelta => DeltaValues.Count > 0 ? DeltaValues.Average() : 0.0;
}

public class RunComparisonResult
{
    public string RunAId { get; set; } = string.Empty;
    public string RunAName { get; set; } = string.Empty;
    public string RunBId { get; set; } = string.Empty;
    public string RunBName { get; set; } = string.Empty;
    public ComparisonAlignmentMode Alignment { get; set; }
    public Dictionary<string, ChannelComparisonResult> Channels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string SummaryNotes { get; set; } = string.Empty;
}

public class RunComparisonService
{
    private readonly SignalProcessingEngine _processor = new();

    public RunComparisonResult CompareDataSets(
        CanonicalLogDataSet runA,
        CanonicalLogDataSet runB,
        ComparisonAlignmentMode mode = ComparisonAlignmentMode.TimeTriggered,
        string? triggerChannel = null,
        double triggerThreshold = 0.0)
    {
        if (runA == null) throw new ArgumentNullException(nameof(runA));
        if (runB == null) throw new ArgumentNullException(nameof(runB));

        var result = new RunComparisonResult
        {
            RunAId = runA.DatasetId,
            RunAName = runA.Name,
            RunBId = runB.DatasetId,
            RunBName = runB.Name,
            Alignment = mode
        };

        // Find common channels between both runs
        var commonChannels = runA.Channels.Keys.Intersect(runB.Channels.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        if (commonChannels.Count == 0)
        {
            result.SummaryNotes = "No common channels found between Run A and Run B.";
            return result;
        }

        if (mode == ComparisonAlignmentMode.RpmDomain)
        {
            CompareInRpmDomain(runA, runB, commonChannels, result);
        }
        else
        {
            CompareInTimeDomain(runA, runB, commonChannels, result, triggerChannel, triggerThreshold);
        }

        result.SummaryNotes = $"Comparison completed for {result.Channels.Count} channels under {mode} alignment.";
        return result;
    }

    private void CompareInTimeDomain(
        CanonicalLogDataSet runA,
        CanonicalLogDataSet runB,
        List<string> channels,
        RunComparisonResult result,
        string? triggerChannel,
        double triggerThreshold)
    {
        double triggerTimeA = FindTriggerTime(runA, triggerChannel, triggerThreshold);
        double triggerTimeB = FindTriggerTime(runB, triggerChannel, triggerThreshold);

        double targetHz = 50.0; // 50 Hz uniform time grid

        foreach (var chName in channels)
        {
            var origA = runA.Channels[chName];
            var origB = runB.Channels[chName];
            if (origA.SampleCount < 2 || origB.SampleCount < 2) continue;

            // Shift timeline so trigger time is 0
            var shiftedA = ShiftTime(origA, triggerTimeA);
            var shiftedB = ShiftTime(origB, triggerTimeB);

            // Resample both to 50 Hz
            var resampledA = _processor.Resample(shiftedA, targetHz);
            var resampledB = _processor.Resample(shiftedB, targetHz);

            // Find overlapping time window
            double start = Math.Max(resampledA.TimestampsMs.First(), resampledB.TimestampsMs.First());
            double end = Math.Min(resampledA.TimestampsMs.Last(), resampledB.TimestampsMs.Last());
            if (end <= start) continue;

            var comp = new ChannelComparisonResult
            {
                ChannelName = chName,
                Units = origA.Units,
                PeakRunA = origA.Max,
                PeakRunB = origB.Max
            };

            double stepMs = 1000.0 / targetHz;
            for (double t = start; t <= end; t += stepMs)
            {
                double vA = InterpolateValue(resampledA, t);
                double vB = InterpolateValue(resampledB, t);
                double delta = vB - vA;
                double deltaPct = (vA != 0.0) ? (delta / Math.Abs(vA)) * 100.0 : 0.0;

                comp.AlignedAxisValues.Add(t / 1000.0); // in seconds
                comp.RunAValues.Add(vA);
                comp.RunBValues.Add(vB);
                comp.DeltaValues.Add(delta);
                comp.DeltaPercent.Add(deltaPct);
            }

            result.Channels[chName] = comp;
        }
    }

    private void CompareInRpmDomain(
        CanonicalLogDataSet runA,
        CanonicalLogDataSet runB,
        List<string> channels,
        RunComparisonResult result)
    {
        string rpmKey = channels.FirstOrDefault(c => c.Contains("rpm", StringComparison.OrdinalIgnoreCase) || c.Contains("speed", StringComparison.OrdinalIgnoreCase)) ?? "EngineRpm";
        if (!runA.Channels.ContainsKey(rpmKey) || !runB.Channels.ContainsKey(rpmKey))
        {
            CompareInTimeDomain(runA, runB, channels, result, null, 0.0);
            return;
        }

        var rpmChanA = runA.Channels[rpmKey];
        var rpmChanB = runB.Channels[rpmKey];

        double minRpm = Math.Max(2000.0, Math.Max(rpmChanA.Min, rpmChanB.Min));
        double maxRpm = Math.Min(rpmChanA.Max, rpmChanB.Max);
        if (maxRpm <= minRpm)
        {
            CompareInTimeDomain(runA, runB, channels, result, null, 0.0);
            return;
        }

        double rpmStep = 50.0; // 50 RPM grid resolution

        foreach (var chName in channels)
        {
            if (chName.Equals(rpmKey, StringComparison.OrdinalIgnoreCase)) continue;

            var chanA = runA.Channels[chName];
            var chanB = runB.Channels[chName];
            if (chanA.SampleCount < 2 || chanB.SampleCount < 2) continue;

            var comp = new ChannelComparisonResult
            {
                ChannelName = chName,
                Units = chanA.Units,
                PeakRunA = chanA.Max,
                PeakRunB = chanB.Max
            };

            for (double rpm = minRpm; rpm <= maxRpm; rpm += rpmStep)
            {
                double vA = LookupValueByRpm(chanA, rpmChanA, rpm);
                double vB = LookupValueByRpm(chanB, rpmChanB, rpm);
                double delta = vB - vA;
                double deltaPct = (vA != 0.0) ? (delta / Math.Abs(vA)) * 100.0 : 0.0;

                comp.AlignedAxisValues.Add(rpm);
                comp.RunAValues.Add(vA);
                comp.RunBValues.Add(vB);
                comp.DeltaValues.Add(delta);
                comp.DeltaPercent.Add(deltaPct);
            }

            result.Channels[chName] = comp;
        }
    }

    private static double FindTriggerTime(CanonicalLogDataSet dataset, string? triggerChannel, double threshold)
    {
        if (string.IsNullOrWhiteSpace(triggerChannel) || !dataset.Channels.TryGetValue(triggerChannel, out var chan))
        {
            return dataset.Channels.Values.FirstOrDefault()?.TimestampsMs.FirstOrDefault() ?? 0.0;
        }

        for (int i = 0; i < chan.SampleCount; i++)
        {
            if (chan.Values[i] >= threshold)
            {
                return chan.TimestampsMs[i];
            }
        }

        return chan.TimestampsMs.FirstOrDefault();
    }

    private static TimeSeriesChannel ShiftTime(TimeSeriesChannel src, double offsetMs)
    {
        var shifted = new TimeSeriesChannel(src.Name, src.Units, src.Source);
        for (int i = 0; i < src.SampleCount; i++)
        {
            shifted.AddPoint(src.TimestampsMs[i] - offsetMs, src.Values[i]);
        }
        return shifted;
    }

    private static double InterpolateValue(TimeSeriesChannel ch, double t)
    {
        int idx = ch.TimestampsMs.BinarySearch(t);
        if (idx >= 0) return ch.Values[idx];

        int next = ~idx;
        if (next == 0) return ch.Values[0];
        if (next >= ch.SampleCount) return ch.Values[^1];

        int prev = next - 1;
        double t0 = ch.TimestampsMs[prev];
        double t1 = ch.TimestampsMs[next];
        double v0 = ch.Values[prev];
        double v1 = ch.Values[next];

        if (t1 == t0) return v0;
        return v0 + (t - t0) * (v1 - v0) / (t1 - t0);
    }

    private static double LookupValueByRpm(TimeSeriesChannel dataChan, TimeSeriesChannel rpmChan, double targetRpm)
    {
        // Find closest RPM index in pull
        int bestIdx = 0;
        double minDiff = double.MaxValue;

        for (int i = 0; i < rpmChan.SampleCount; i++)
        {
            double diff = Math.Abs(rpmChan.Values[i] - targetRpm);
            if (diff < minDiff)
            {
                minDiff = diff;
                bestIdx = i;
            }
        }

        // Map timestamp of bestIdx to dataChan
        if (bestIdx < rpmChan.SampleCount)
        {
            double t = rpmChan.TimestampsMs[bestIdx];
            return InterpolateValue(dataChan, t);
        }

        return 0.0;
    }
}
