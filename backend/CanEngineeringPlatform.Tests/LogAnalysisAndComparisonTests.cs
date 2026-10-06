using System;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Analysis.Comparison;
using CanEngineeringPlatform.Core.Analysis.Import;
using CanEngineeringPlatform.Core.Analysis.Processing;
using CanEngineeringPlatform.Core.Analysis.TimeSeries;
using CanEngineeringPlatform.Core.Analysis.Validation;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class LogAnalysisAndComparisonTests
{
    private readonly LogImportService _importer = new();
    private readonly LogValidationEngine _validator = new();
    private readonly SignalProcessingEngine _processor = new();
    private readonly RunComparisonService _comparison = new();

    // ==========================================
    // 1. Multi-Format Importer Tests
    // ==========================================

    [Fact]
    public void ImportGenericCanCsv_ParsesFramesAccurately()
    {
        string csv = @"TimestampUs,IdHex,DLC,DataHex,Channel
10000,0x123,8,01 02 03 04 05 06 07 08,0
20000,0x280,8,0A 0B 0C 0D 0E 0F 10 11,0
30000,0x300,4,AA BB CC DD,1";

        var result = _importer.ImportLog(csv, "run.csv");

        Assert.True(result.Success);
        Assert.Equal(LogFileFormat.GenericCanCsv, result.DetectedFormat);
        Assert.NotNull(result.CanSession);
        Assert.Equal(3, result.CanSession!.Frames.Count);

        var f0 = result.CanSession.Frames[0];
        Assert.Equal(10000u, f0.TimestampUs);
        Assert.Equal(0x123u, f0.Id);
        Assert.Equal(8, f0.Dlc);
        Assert.Equal(0x01, f0.Data[0]);
        Assert.Equal(0x08, f0.Data[7]);
        Assert.Equal(0, f0.Channel);

        var f2 = result.CanSession.Frames[2];
        Assert.Equal(1, f2.Channel);
        Assert.Equal(0x300u, f2.Id);
    }

    [Fact]
    public void ImportTorqueProCsv_ExtractsGpsAndSensors()
    {
        string torqueCsv = @"GPS Time, Device Time, Latitude, Longitude, Engine RPM(rpm), Speed (OBD)(km/h), Boost(bar)
Wed Jun 10 14:00:00 GMT 2026, 14:00:00.100, 37.7749, -122.4194, 2500, 60, 0.5
Wed Jun 10 14:00:01 GMT 2026, 14:00:01.100, 37.7750, -122.4192, 3200, 75, 1.1
Wed Jun 10 14:00:02 GMT 2026, 14:00:02.100, 37.7751, -122.4190, 4100, 92, 1.4";

        var result = _importer.ImportLog(torqueCsv, "torque_track.csv");

        Assert.True(result.Success);
        Assert.Equal(LogFileFormat.TorqueProCsv, result.DetectedFormat);
        Assert.NotNull(result.DataSet);

        var ds = result.DataSet!;
        Assert.True(ds.Channels.ContainsKey("Engine RPM"));
        Assert.True(ds.Channels.ContainsKey("Speed (OBD)"));
        Assert.True(ds.Channels.ContainsKey("Boost"));
        Assert.Equal(3, ds.Channels["Engine RPM"].SampleCount);
        Assert.Equal(2500.0, ds.Channels["Engine RPM"].Values[0]);
        Assert.Equal(4100.0, ds.Channels["Engine RPM"].Values[2]);

        // Verify GPS points
        Assert.Equal(3, ds.GpsTrack.Count);
        Assert.Equal(37.7749, ds.GpsTrack[0].Latitude, 4);
        Assert.Equal(-122.4194, ds.GpsTrack[0].Longitude, 4);
    }

    [Fact]
    public void ImportVcdsCsv_ExtractsTimestampedGroups()
    {
        string vcdsCsv = @"Group 1, Engine Speed, STAMP, Coolant Temp, STAMP, Throttle Angle
, /min, , C, , %
, 840, 0.15, 88.0, 0.15, 12.5
, 2450, 1.15, 89.0, 1.15, 65.0
, 4800, 2.15, 90.0, 2.15, 100.0";

        var result = _importer.ImportLog(vcdsCsv, "vcds_log.csv");

        Assert.True(result.Success);
        Assert.Equal(LogFileFormat.VcdsCsv, result.DetectedFormat);
        Assert.NotNull(result.DataSet);

        var ds = result.DataSet!;
        Assert.True(ds.Channels.ContainsKey("Engine Speed"));
        Assert.True(ds.Channels.ContainsKey("Coolant Temp"));
        Assert.True(ds.Channels.ContainsKey("Throttle Angle"));
        Assert.Equal(3, ds.Channels["Engine Speed"].SampleCount);
        Assert.Equal(4800.0, ds.Channels["Engine Speed"].Values[2]);
    }

    [Fact]
    public void ImportCandump_ParsesLinuxSocketCanTraces()
    {
        string candumpText = @"(1600000000.001000) can0 123#0102030405060708
(1600000000.002000) can0 280#AABBCCDDEEFF0011
(1600000000.003500) can1 300#CAFE";

        var result = _importer.ImportLog(candumpText, "candump.log");

        Assert.True(result.Success);
        Assert.Equal(LogFileFormat.LinuxCandump, result.DetectedFormat);
        Assert.NotNull(result.CanSession);
        Assert.Equal(3, result.CanSession!.Frames.Count);

        var f0 = result.CanSession.Frames[0];
        Assert.Equal(0x123u, f0.Id);
        Assert.Equal(8, f0.ActualLength);
        Assert.Equal(0, f0.Channel);

        var f2 = result.CanSession.Frames[2];
        Assert.Equal(0x300u, f2.Id);
        Assert.Equal(2, f2.ActualLength);
        Assert.Equal(1, f2.Channel);
    }

    [Fact]
    public void ImportVectorAsc_ParsesStandardVectorAscLogs()
    {
        string vectorAsc = @"date Wed Jun 10 02:00:00 pm 2026
base hex  timestamps absolute
internal events logged
// version 7.0.0
   0.0012 1  123             Rx   d 8 01 02 03 04 05 06 07 08
   0.0025 1  18DAF110x       Tx   d 8 02 01 00 00 00 00 00 00
   0.0040 2  280             Rx   d 8 AA BB CC DD EE FF 11 22";

        var result = _importer.ImportLog(vectorAsc, "vector_trace.asc");

        Assert.True(result.Success);
        Assert.Equal(LogFileFormat.VectorAsc, result.DetectedFormat);
        Assert.NotNull(result.CanSession);
        Assert.Equal(3, result.CanSession!.Frames.Count);

        var f0 = result.CanSession.Frames[0];
        Assert.Equal(0x123u, f0.Id);
        Assert.False(f0.IsExtended);
        Assert.Equal(0, f0.Channel);

        var f1 = result.CanSession.Frames[1];
        Assert.Equal(0x18DAF110u, f1.Id);
        Assert.True(f1.IsExtended);

        var f2 = result.CanSession.Frames[2];
        Assert.Equal(1, f2.Channel);
    }

    // ==========================================
    // 2. Automated Log Validation Tests
    // ==========================================

    [Fact]
    public void LogValidator_DetectsMissingSampleGaps()
    {
        var ds = new CanonicalLogDataSet();
        var ch = new TimeSeriesChannel("EngineSpeed", "RPM", "CAN");

        // Normal 10ms sampling, followed by an unexpected 500ms drop
        for (int i = 0; i < 20; i++) ch.AddPoint(i * 10, 2000 + i * 10);
        ch.AddPoint(700, 2300); // 500ms gap
        for (int i = 0; i < 10; i++) ch.AddPoint(710 + i * 10, 2310 + i * 10);

        ds.Channels[ch.Name] = ch;

        var report = _validator.Validate(ds);

        Assert.True(report.MissingGapsCount > 0);
        Assert.Contains(report.Issues, i => i.Type == ValidationIssueType.MissingSamples && i.ChannelName == "EngineSpeed");
    }

    [Fact]
    public void LogValidator_DetectsTimestampJumpsAndDuplicates()
    {
        var ds = new CanonicalLogDataSet();
        var ch = new TimeSeriesChannel("VehicleSpeed", "km/h", "CAN");

        ch.AddPoint(100, 50.0);
        ch.AddPoint(110, 50.5);
        ch.AddPoint(110, 50.6); // duplicate timestamp
        ch.AddPoint(90, 49.0);  // backwards jump

        ds.Channels[ch.Name] = ch;

        var report = _validator.Validate(ds);

        Assert.True(report.DuplicatesCount >= 1);
        Assert.True(report.TimestampJumpsCount >= 1);
        Assert.False(report.IsValid); // backwards jump is an Error
    }

    [Fact]
    public void LogValidator_DetectsImpossibleAutomotiveValues()
    {
        var ds = new CanonicalLogDataSet();
        var rpm = new TimeSeriesChannel("RPM", "rpm", "CAN");
        var coolant = new TimeSeriesChannel("Coolant", "C", "CAN");

        rpm.AddPoint(0, 800);
        rpm.AddPoint(10, 18500); // Impossible RPM (> 12,000)

        coolant.AddPoint(0, 90);
        coolant.AddPoint(10, 250); // Impossible Coolant Temp (> 150 C)

        ds.Channels[rpm.Name] = rpm;
        ds.Channels[coolant.Name] = coolant;

        var report = _validator.Validate(ds);

        Assert.True(report.ImpossibleValuesCount >= 2);
        Assert.Contains(report.Issues, i => i.Type == ValidationIssueType.ImpossibleValues && i.ChannelName == "RPM");
        Assert.Contains(report.Issues, i => i.Type == ValidationIssueType.ImpossibleValues && i.ChannelName == "Coolant");
    }

    // ==========================================
    // 3. Signal Processing & Filtering Tests
    // ==========================================

    [Fact]
    public void SignalProcessor_ResamplesIrregularSignalsToUniformGrid()
    {
        var ch = new TimeSeriesChannel("Throttle", "%", "OBD");
        ch.AddPoint(0, 0.0);
        ch.AddPoint(53, 50.0);
        ch.AddPoint(105, 100.0);
        ch.AddPoint(198, 100.0);

        var options = new ProcessingOptions
        {
            ResampleFrequencyHz = 50.0, // 20ms intervals
            Interpolation = InterpolationMode.Linear
        };

        var processed = _processor.ProcessChannel(ch, options);

        // Grid should be 0, 20, 40, 60, 80, 100, 120, 140, 160, 180ms
        Assert.True(processed.SampleCount > 5);
        Assert.Equal(0.0, processed.TimestampsMs[0]);
        Assert.Equal(20.0, processed.TimestampsMs[1]);
        Assert.Equal(40.0, processed.TimestampsMs[2]);
        Assert.InRange(processed.Values[1], 15.0, 25.0); // interpolated around 20ms
    }

    [Fact]
    public void SignalProcessor_MedianFilterRemovesSpikes()
    {
        var ch = new TimeSeriesChannel("Boost", "bar", "CAN");
        ch.AddPoint(0, 1.0);
        ch.AddPoint(10, 1.02);
        ch.AddPoint(20, 9.85); // Huge corrupted spike
        ch.AddPoint(30, 1.04);
        ch.AddPoint(40, 1.05);

        var options = new ProcessingOptions
        {
            MedianFilterWindow = 3
        };

        var filtered = _processor.ProcessChannel(ch, options);

        Assert.Equal(ch.SampleCount, filtered.SampleCount);
        // The spike at index 2 (9.85) should be suppressed by the median of [1.02, 9.85, 1.04] -> 1.04
        Assert.InRange(filtered.Values[2], 1.0, 1.1);
    }

    [Fact]
    public void SignalProcessor_MovingAverageSmoothsNoise()
    {
        var ch = new TimeSeriesChannel("Lambda", "ratio", "CAN");
        ch.AddPoint(0, 1.00);
        ch.AddPoint(10, 1.10);
        ch.AddPoint(20, 0.90);
        ch.AddPoint(30, 1.10);
        ch.AddPoint(40, 0.90);

        var options = new ProcessingOptions
        {
            MovingAverageWindow = 3
        };

        var filtered = _processor.ProcessChannel(ch, options);

        Assert.Equal(ch.SampleCount, filtered.SampleCount);
        // Index 2 is average of [1.10, 0.90, 1.10] = 3.10 / 3 = 1.033
        Assert.InRange(filtered.Values[2], 1.0, 1.05);
    }

    // ==========================================
    // 4. Session & Run Comparison Tests
    // ==========================================

    [Fact]
    public void RunComparison_TimeDomainWithTrigger_CalculatesAccurateDelta()
    {
        // Run A (Stock): boost peaks at 1.0 bar
        var runA = new CanonicalLogDataSet { DatasetId = "run_a", Name = "Stock Calibration" };
        var boostA = new TimeSeriesChannel("Boost", "bar", "CAN");
        var tpsA = new TimeSeriesChannel("TPS", "%", "CAN");
        for (int i = 0; i <= 20; i++)
        {
            double t = i * 50.0;
            tpsA.AddPoint(t, i >= 2 ? 100.0 : 20.0);
            boostA.AddPoint(t, i >= 2 ? Math.Min(1.0, (i - 2) * 0.1) : 0.0);
        }
        runA.Channels[boostA.Name] = boostA;
        runA.Channels[tpsA.Name] = tpsA;

        // Run B (Tuned): boost peaks at 1.5 bar
        var runB = new CanonicalLogDataSet { DatasetId = "run_b", Name = "Stage 1 Calibration" };
        var boostB = new TimeSeriesChannel("Boost", "bar", "CAN");
        var tpsB = new TimeSeriesChannel("TPS", "%", "CAN");
        for (int i = 0; i <= 20; i++)
        {
            double t = i * 50.0;
            tpsB.AddPoint(t, i >= 4 ? 100.0 : 20.0); // triggered slightly later
            boostB.AddPoint(t, i >= 4 ? Math.Min(1.5, (i - 4) * 0.15) : 0.0);
        }
        runB.Channels[boostB.Name] = boostB;
        runB.Channels[tpsB.Name] = tpsB;

        var result = _comparison.CompareDataSets(
            runA,
            runB,
            ComparisonAlignmentMode.TimeTriggered,
            triggerChannel: "TPS",
            triggerThreshold: 90.0);

        Assert.NotNull(result);
        Assert.True(result.Channels.ContainsKey("Boost"));

        var boostComp = result.Channels["Boost"];
        Assert.Equal(1.0, boostComp.PeakRunA, 2);
        Assert.Equal(1.5, boostComp.PeakRunB, 2);
        Assert.Equal(0.5, boostComp.PeakDelta, 2); // +0.5 bar gain
        Assert.Equal(50.0, boostComp.PeakDeltaPercent, 1); // +50%
    }

    [Fact]
    public void RunComparison_RpmDomain_ResamplesAccuratelyAcrossRpmGrid()
    {
        // Run A: Power pull from 2000 to 6500 RPM
        var runA = new CanonicalLogDataSet { DatasetId = "dyno_stock", Name = "Dyno Stock" };
        var rpmA = new TimeSeriesChannel("RPM", "rpm", "CAN");
        var torqueA = new TimeSeriesChannel("Torque", "Nm", "CAN");
        for (int i = 0; i <= 10; i++)
        {
            double rpm = 2000 + i * 450;
            rpmA.AddPoint(i * 100, rpm);
            torqueA.AddPoint(i * 100, 300.0); // flat 300 Nm
        }
        runA.Channels[rpmA.Name] = rpmA;
        runA.Channels[torqueA.Name] = torqueA;

        // Run B: Tuned pull from 2000 to 6500 RPM
        var runB = new CanonicalLogDataSet { DatasetId = "dyno_tune", Name = "Dyno Tune" };
        var rpmB = new TimeSeriesChannel("RPM", "rpm", "CAN");
        var torqueB = new TimeSeriesChannel("Torque", "Nm", "CAN");
        for (int i = 0; i <= 10; i++)
        {
            double rpm = 2000 + i * 450;
            rpmB.AddPoint(i * 100, rpm);
            torqueB.AddPoint(i * 100, 360.0); // flat 360 Nm (+60 Nm)
        }
        runB.Channels[rpmB.Name] = rpmB;
        runB.Channels[torqueB.Name] = torqueB;

        var result = _comparison.CompareDataSets(
            runA,
            runB,
            ComparisonAlignmentMode.RpmDomain);

        Assert.NotNull(result);
        Assert.True(result.Channels.ContainsKey("Torque"));

        var torqueComp = result.Channels["Torque"];
        Assert.InRange(torqueComp.PeakRunA, 299.0, 301.0);
        Assert.InRange(torqueComp.PeakRunB, 359.0, 361.0);
        Assert.InRange(torqueComp.PeakDelta, 59.0, 61.0);
        Assert.InRange(torqueComp.MeanDelta, 59.0, 61.0);
    }

    // ==========================================
    // 5. GPS Synchronization Tests
    // ==========================================

    [Fact]
    public void GpsSyncEngine_ProjectsCoordinatesAndSpeedToChannels()
    {
        var ds = new CanonicalLogDataSet();
        DateTime baseUtc = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

        ds.GpsTrack.Add(new GpsPoint
        {
            TimestampMs = 1000,
            TimestampUtc = baseUtc,
            Latitude = 52.5200,
            Longitude = 13.4050,
            SpeedKmh = 50.0
        });

        ds.GpsTrack.Add(new GpsPoint
        {
            TimestampMs = 2000,
            TimestampUtc = baseUtc.AddSeconds(1),
            Latitude = 52.5205,
            Longitude = 13.4060,
            SpeedKmh = 55.0
        });

        GpsSyncEngine.SynchronizeGpsWithCan(ds, canMonotonicOffsetMs: 500);

        Assert.True(ds.Channels.ContainsKey("GpsLatitude"));
        Assert.True(ds.Channels.ContainsKey("GpsLongitude"));
        Assert.True(ds.Channels.ContainsKey("GpsSpeedKmh"));

        var lat = ds.Channels["GpsLatitude"];
        Assert.Equal(2, lat.SampleCount);
        Assert.Equal(500.0, lat.TimestampsMs[0]); // 1000 - 500 offset
        Assert.Equal(52.5200, lat.Values[0], 4);
    }
}
