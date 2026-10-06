using System;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Analysis.TimeSeries;

namespace CanEngineeringPlatform.Core.Analysis.Processing;

public enum InterpolationMode
{
    Linear,
    StepHold,
    Nearest
}

public class ProcessingOptions
{
    public double? ResampleFrequencyHz { get; set; }
    public InterpolationMode Interpolation { get; set; } = InterpolationMode.Linear;
    public int? MovingAverageWindow { get; set; }
    public int? MedianFilterWindow { get; set; }
    public double? EmaAlpha { get; set; }
    public double? LowPassCutoffHz { get; set; }
}

public class SignalProcessingEngine
{
    public TimeSeriesChannel ProcessChannel(TimeSeriesChannel channel, ProcessingOptions options)
    {
        if (channel == null) throw new ArgumentNullException(nameof(channel));
        if (options == null || channel.SampleCount < 2) return channel;

        var result = CloneChannel(channel);

        // 1. Median filter (spike removal first)
        if (options.MedianFilterWindow.HasValue && options.MedianFilterWindow.Value > 1)
        {
            result = ApplyMedianFilter(result, options.MedianFilterWindow.Value);
        }

        // 2. Resampling onto uniform grid
        if (options.ResampleFrequencyHz.HasValue && options.ResampleFrequencyHz.Value > 0)
        {
            result = Resample(result, options.ResampleFrequencyHz.Value, options.Interpolation);
        }

        // 3. Moving Average smoothing
        if (options.MovingAverageWindow.HasValue && options.MovingAverageWindow.Value > 1)
        {
            result = ApplyMovingAverage(result, options.MovingAverageWindow.Value);
        }

        // 4. Exponential Moving Average (EMA)
        if (options.EmaAlpha.HasValue && options.EmaAlpha.Value > 0 && options.EmaAlpha.Value < 1.0)
        {
            result = ApplyEma(result, options.EmaAlpha.Value);
        }

        // 5. Low-Pass Filter
        if (options.LowPassCutoffHz.HasValue && options.LowPassCutoffHz.Value > 0)
        {
            result = ApplyLowPass(result, options.LowPassCutoffHz.Value);
        }

        return result;
    }

    public TimeSeriesChannel Resample(TimeSeriesChannel channel, double targetHz, InterpolationMode mode = InterpolationMode.Linear)
    {
        if (channel.SampleCount < 2 || targetHz <= 0) return CloneChannel(channel);

        double stepMs = 1000.0 / targetHz;
        double startMs = channel.TimestampsMs.First();
        double endMs = channel.TimestampsMs.Last();

        var resampled = new TimeSeriesChannel($"{channel.Name}_Resampled", channel.Units, channel.Source);

        int originalIdx = 0;
        for (double t = startMs; t <= endMs; t += stepMs)
        {
            // Advance originalIdx so t is between originalIdx and originalIdx + 1
            while (originalIdx < channel.SampleCount - 2 && channel.TimestampsMs[originalIdx + 1] < t)
            {
                originalIdx++;
            }

            double t0 = channel.TimestampsMs[originalIdx];
            double t1 = channel.TimestampsMs[originalIdx + 1];
            double v0 = channel.Values[originalIdx];
            double v1 = channel.Values[originalIdx + 1];

            double interpolatedVal;
            if (t <= t0)
            {
                interpolatedVal = v0;
            }
            else if (t >= t1)
            {
                interpolatedVal = v1;
            }
            else
            {
                interpolatedVal = mode switch
                {
                    InterpolationMode.StepHold => v0,
                    InterpolationMode.Nearest => (Math.Abs(t - t0) <= Math.Abs(t - t1)) ? v0 : v1,
                    _ => v0 + (t - t0) * (v1 - v0) / (t1 - t0) // Linear
                };
            }

            resampled.AddPoint(t, interpolatedVal);
        }

        return resampled;
    }

    public TimeSeriesChannel ApplyMovingAverage(TimeSeriesChannel channel, int windowSize)
    {
        if (channel.SampleCount < windowSize || windowSize <= 1) return CloneChannel(channel);

        var smoothed = new TimeSeriesChannel($"{channel.Name}_MA", channel.Units, channel.Source);
        int half = windowSize / 2;

        for (int i = 0; i < channel.SampleCount; i++)
        {
            int start = Math.Max(0, i - half);
            int end = Math.Min(channel.SampleCount - 1, i + half);
            int count = end - start + 1;

            double sum = 0.0;
            for (int k = start; k <= end; k++)
            {
                sum += channel.Values[k];
            }

            smoothed.AddPoint(channel.TimestampsMs[i], sum / count);
        }

        return smoothed;
    }

    public TimeSeriesChannel ApplyMedianFilter(TimeSeriesChannel channel, int windowSize)
    {
        if (channel.SampleCount < windowSize || windowSize <= 1) return CloneChannel(channel);

        var filtered = new TimeSeriesChannel($"{channel.Name}_Median", channel.Units, channel.Source);
        int half = windowSize / 2;
        var buffer = new List<double>(windowSize);

        for (int i = 0; i < channel.SampleCount; i++)
        {
            int start = Math.Max(0, i - half);
            int end = Math.Min(channel.SampleCount - 1, i + half);

            buffer.Clear();
            for (int k = start; k <= end; k++)
            {
                buffer.Add(channel.Values[k]);
            }
            buffer.Sort();

            double median = buffer[buffer.Count / 2];
            filtered.AddPoint(channel.TimestampsMs[i], median);
        }

        return filtered;
    }

    public TimeSeriesChannel ApplyEma(TimeSeriesChannel channel, double alpha)
    {
        if (channel.SampleCount < 2) return CloneChannel(channel);

        var ema = new TimeSeriesChannel($"{channel.Name}_EMA", channel.Units, channel.Source);
        double currentEma = channel.Values[0];
        ema.AddPoint(channel.TimestampsMs[0], currentEma);

        for (int i = 1; i < channel.SampleCount; i++)
        {
            currentEma = (alpha * channel.Values[i]) + ((1.0 - alpha) * currentEma);
            ema.AddPoint(channel.TimestampsMs[i], currentEma);
        }

        return ema;
    }

    public TimeSeriesChannel ApplyLowPass(TimeSeriesChannel channel, double cutoffHz)
    {
        if (channel.SampleCount < 2 || cutoffHz <= 0) return CloneChannel(channel);

        var filtered = new TimeSeriesChannel($"{channel.Name}_LPF", channel.Units, channel.Source);
        double yPrev = channel.Values[0];
        filtered.AddPoint(channel.TimestampsMs[0], yPrev);

        for (int i = 1; i < channel.SampleCount; i++)
        {
            double dt = (channel.TimestampsMs[i] - channel.TimestampsMs[i - 1]) / 1000.0; // in seconds
            if (dt <= 0) dt = 0.01;

            double rc = 1.0 / (2.0 * Math.PI * cutoffHz);
            double alpha = dt / (rc + dt);

            double y = yPrev + alpha * (channel.Values[i] - yPrev);
            filtered.AddPoint(channel.TimestampsMs[i], y);
            yPrev = y;
        }

        return filtered;
    }

    private static TimeSeriesChannel CloneChannel(TimeSeriesChannel src)
    {
        var copy = new TimeSeriesChannel(src.Name, src.Units, src.Source);
        copy.TimestampsMs.AddRange(src.TimestampsMs);
        copy.Values.AddRange(src.Values);
        return copy;
    }
}
