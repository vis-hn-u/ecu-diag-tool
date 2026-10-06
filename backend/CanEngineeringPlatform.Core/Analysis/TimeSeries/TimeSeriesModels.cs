using System;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.Analysis.TimeSeries;

public class TimeSeriesChannel
{
    public string Name { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public string Source { get; set; } = "CAN"; // CAN, OBD, GPS, VCDS, Calculated
    public List<double> TimestampsMs { get; set; } = new();
    public List<double> Values { get; set; } = new();

    public int SampleCount => Values.Count;
    public double Min => Values.Count > 0 ? Values.Min() : 0.0;
    public double Max => Values.Count > 0 ? Values.Max() : 0.0;
    public double Mean => Values.Count > 0 ? Values.Average() : 0.0;

    public double StdDev
    {
        get
        {
            if (Values.Count <= 1) return 0.0;
            double avg = Mean;
            double sumSquares = Values.Sum(v => (v - avg) * (v - avg));
            return Math.Sqrt(sumSquares / (Values.Count - 1));
        }
    }

    public TimeSeriesChannel() { }

    public TimeSeriesChannel(string name, string units = "", string source = "CAN")
    {
        Name = name;
        Units = units;
        Source = source;
    }

    public void AddPoint(double timestampMs, double value)
    {
        TimestampsMs.Add(timestampMs);
        Values.Add(value);
    }
}

public class GpsPoint
{
    public double TimestampMs { get; set; }
    public DateTime? TimestampUtc { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AltitudeMeters { get; set; }
    public double? SpeedKmh { get; set; }
    public double? HeadingDegrees { get; set; }
    public int? Satellites { get; set; }
    public int? FixQuality { get; set; } // 0=Invalid, 1=GPS, 2=DGPS/RTK
}

public enum LogFileFormat
{
    GenericCanCsv,
    TorqueProCsv,
    VcdsCsv,
    LinuxCandump,
    VectorAsc,
    GenericTimeSeriesCsv,
    JsonLog
}

public class CanonicalLogDataSet
{
    public string DatasetId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Imported Log Dataset";
    public LogFileFormat SourceFormat { get; set; } = LogFileFormat.GenericCanCsv;
    public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;
    public DateTime EndTimeUtc { get; set; } = DateTime.UtcNow;
    public double DurationSeconds => Math.Max(0.0, (EndTimeUtc - StartTimeUtc).TotalSeconds);

    public Dictionary<string, TimeSeriesChannel> Channels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<GpsPoint> GpsTrack { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void AddChannelPoint(string channelName, double timestampMs, double value, string units = "", string source = "CAN")
    {
        if (!Channels.TryGetValue(channelName, out var channel))
        {
            channel = new TimeSeriesChannel(channelName, units, source);
            Channels[channelName] = channel;
        }
        channel.AddPoint(timestampMs, value);
    }
}

/// <summary>
/// Precision synchronization engine linking asynchronous GPS UTC timestamps with high-resolution monotonic bus timestamps.
/// </summary>
public static class GpsSyncEngine
{
    public static void SynchronizeGpsWithCan(CanonicalLogDataSet dataset, double canMonotonicOffsetMs = 0.0)
    {
        if (dataset == null || dataset.GpsTrack.Count == 0) return;

        // Find initial GPS UTC anchor
        var firstValidGps = dataset.GpsTrack.FirstOrDefault(g => g.TimestampUtc.HasValue);
        if (firstValidGps == null || !firstValidGps.TimestampUtc.HasValue) return;

        DateTime anchorUtc = firstValidGps.TimestampUtc.Value;
        double anchorMonoMs = firstValidGps.TimestampMs;

        // Align each GPS point's relative time with CAN timeline
        foreach (var pt in dataset.GpsTrack)
        {
            pt.TimestampMs = pt.TimestampMs - canMonotonicOffsetMs;
            if (!pt.TimestampUtc.HasValue)
            {
                double offsetFromAnchor = pt.TimestampMs - anchorMonoMs;
                pt.TimestampUtc = anchorUtc.AddMilliseconds(offsetFromAnchor);
            }
        }

        // Project GPS coordinates into channels if not already present
        if (!dataset.Channels.ContainsKey("GpsLatitude"))
        {
            var latChan = new TimeSeriesChannel("GpsLatitude", "deg", "GPS");
            var lonChan = new TimeSeriesChannel("GpsLongitude", "deg", "GPS");
            var spdChan = new TimeSeriesChannel("GpsSpeedKmh", "km/h", "GPS");

            foreach (var pt in dataset.GpsTrack)
            {
                latChan.AddPoint(pt.TimestampMs, pt.Latitude);
                lonChan.AddPoint(pt.TimestampMs, pt.Longitude);
                if (pt.SpeedKmh.HasValue)
                {
                    spdChan.AddPoint(pt.TimestampMs, pt.SpeedKmh.Value);
                }
            }

            dataset.Channels[latChan.Name] = latChan;
            dataset.Channels[lonChan.Name] = lonChan;
            if (spdChan.SampleCount > 0) dataset.Channels[spdChan.Name] = spdChan;
        }
    }
}
