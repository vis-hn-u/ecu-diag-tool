using System;
using System.Linq;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Replay;
using CanEngineeringPlatform.Core.Topology;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class CanLoggerAndReplayTests
{
    [Fact]
    public void Logger_StartCaptureExportCsv_Succeeds()
    {
        var logger = new CanLoggerService();
        var session = logger.StartLogging("TestSession");

        Assert.True(session.IsActive);
        Assert.False(session.IsPaused);

        for (int i = 0; i < 10; i++)
        {
            var frame = new CanFrame((uint)(i * 1000), 0x7DF, 8, 0, new byte[] { 2, 1, (byte)i, 0, 0, 0, 0, 0 });
            logger.ProcessFrame(frame);
        }

        var stopped = logger.StopLogging();
        Assert.NotNull(stopped);
        Assert.Equal(10, stopped.FrameCount);

        string csv = logger.ExportCsv(stopped.Id);
        Assert.Contains("TimestampUs,IdHex,IdDec,Type,RTR,DLC,DataHex", csv);
        Assert.Contains("0x7DF", csv);
        Assert.Contains("02 01 00 00 00 00 00 00", csv);
    }

    [Fact]
    public void Replay_LoadFromCsv_ParsesFramesCorrectly()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        using var replay = new CanReplayService(pipeline);

        string csv = @"TimestampUs,IdHex,IdDec,Type,RTR,DLC,DataHex
1000,0x100,256,Standard,0,4,11 22 33 44
2000,0x200,512,Standard,0,2,AA BB
";

        bool loaded = replay.LoadFromCsv(csv, "TestReplay");
        Assert.True(loaded);

        var status = replay.GetStatus();
        Assert.True(status.IsLoaded);
        Assert.Equal(2, status.TotalFrames);
        Assert.Equal("TestReplay", status.Name);
    }
}
