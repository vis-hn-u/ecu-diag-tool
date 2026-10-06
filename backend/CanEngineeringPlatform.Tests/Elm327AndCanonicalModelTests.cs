using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Analysis.TimeSeries;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Dyno;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Hardware.Transports;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Signals.Calculated;
using CanEngineeringPlatform.Core.Vehicles;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class Elm327AndCanonicalModelTests
{
    // ==========================================
    // 1. ELM327 Transport Tests
    // ==========================================

    [Fact]
    public void Elm327Transport_Capabilities_MatchesStandardClassicCan()
    {
        using var elm = new Elm327Transport();
        var caps = elm.Capabilities;

        Assert.True(caps.SupportsClassicCan);
        Assert.False(caps.SupportsCanFd);
        Assert.False(caps.SupportsBitRateSwitch);
        Assert.Equal(1, caps.ChannelCount);
        Assert.Equal(8, caps.MaxPayloadBytes);
        Assert.True(caps.SupportsTransmit);
        Assert.Equal(TransportType.Elm327, elm.Type);
    }

    [Fact]
    public async Task Elm327Transport_ConnectAndDisconnect_UpdatesStatus()
    {
        using var elm = new Elm327Transport("COM5", 115200);

        Assert.False(elm.GetStatus().IsConnected);

        bool connected = await elm.ConnectAsync();
        Assert.True(connected);
        Assert.True(elm.GetStatus().IsConnected);
        Assert.Contains("COM5", elm.GetStatus().ConnectionDetails);

        await elm.DisconnectAsync();
        Assert.False(elm.GetStatus().IsConnected);
    }

    [Fact]
    public async Task Elm327Transport_SendClassicFrame_SucceedsAndGeneratesLoopbackResponse()
    {
        using var elm = new Elm327Transport();
        await elm.ConnectAsync();

        var receivedFrames = new List<CanFrame>();
        elm.OnFrameReceived += f => receivedFrames.Add(f);

        // OBD Mode 01 PID 0x0C (Engine RPM)
        byte[] data = new byte[] { 0x02, 0x01, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var reqFrame = CanFrame.CreateClassic(0x7DF, data, isExtended: false, channel: 1);

        bool sent = await elm.SendFrameAsync(reqFrame);
        Assert.True(sent);

        // Should receive transmitted echo and simulated response (0x7E8)
        Assert.NotEmpty(receivedFrames);
        Assert.Contains(receivedFrames, f => f.Id == 0x7E8);
        var rxResp = receivedFrames.First(f => f.Id == 0x7E8);
        Assert.Equal(0x41, rxResp.Data[1]); // Mode 01 response
        Assert.Equal(0x0C, rxResp.Data[2]); // PID 0x0C
    }

    [Fact]
    public async Task Elm327Transport_SendCanFdFrame_RejectedDueToHardwareLimits()
    {
        using var elm = new Elm327Transport();
        await elm.ConnectAsync();

        // 64-byte CAN FD frame
        byte[] fdData = new byte[64];
        var fdFrame = CanFrame.CreateCanFd(0x123, fdData, channel: 1);

        bool sent = await elm.SendFrameAsync(fdFrame);
        Assert.False(sent, "ELM327 should reject CAN FD frames.");
    }

    [Fact]
    public void Elm327Transport_ProcessIncomingElmLine_Standard11Bit_ParsesCorrectly()
    {
        using var elm = new Elm327Transport();
        CanFrame? parsed = null;
        elm.OnFrameReceived += f => parsed = f;

        // Line with spaces: 7E8 06 41 00 BE 3F B8 11
        bool success = elm.ProcessIncomingElmLine("7E8 06 41 00 BE 3F B8 11");
        Assert.True(success);
        Assert.NotNull(parsed);
        Assert.Equal(0x7E8U, parsed.Value.Id);
        Assert.False(parsed.Value.IsExtended);
        Assert.Equal(7, parsed.Value.ActualLength);
        Assert.Equal(0x06, parsed.Value.Data[0]);
        Assert.Equal(0x41, parsed.Value.Data[1]);
        Assert.Equal(0x00, parsed.Value.Data[2]);
    }

    [Fact]
    public void Elm327Transport_ProcessIncomingElmLine_Extended29Bit_ParsesCorrectly()
    {
        using var elm = new Elm327Transport();
        CanFrame? parsed = null;
        elm.OnFrameReceived += f => parsed = f;

        // 29-bit CAN ID: 18DAF110
        bool success = elm.ProcessIncomingElmLine("18DAF110 08 49 02 01 00 00 00 00");
        Assert.True(success);
        Assert.NotNull(parsed);
        Assert.Equal(0x18DAF110U, parsed.Value.Id);
        Assert.True(parsed.Value.IsExtended);
        Assert.Equal(8, parsed.Value.ActualLength);
        Assert.Equal(0x49, parsed.Value.Data[1]);
    }

    [Fact]
    public async Task HardwareManager_RegistersAndSwitchesToElm327()
    {
        using var sim = new SimulationTransport();
        using var elm = new Elm327Transport();
        using var mgr = new HardwareManager(null, new ICanTransport[] { sim, elm });

        Assert.Equal(2, mgr.GetAllTransports().Count);

        // Switch to ELM327
        bool switched = await mgr.SetActiveTransportAsync("elm327-adapter");
        Assert.True(switched);
        Assert.Equal("elm327-adapter", mgr.ActiveTransportId);
        Assert.True(mgr.GetStatus()?.IsConnected);
        Assert.Equal(TransportType.Elm327, mgr.GetStatus()?.Type);
    }

    // ==========================================
    // 2. Canonical Model Interoperability Tests
    // ==========================================

    [Fact]
    public void CanonicalLogDataSet_ExtractRoadDynoSamples_ProducesValidAlignedSamples()
    {
        var dataset = new CanonicalLogDataSet();
        dataset.AddChannelPoint("Engine RPM", 0, 2000, "RPM");
        dataset.AddChannelPoint("Engine RPM", 1000, 3000, "RPM");
        dataset.AddChannelPoint("Engine RPM", 2000, 4000, "RPM");
        dataset.AddChannelPoint("Engine RPM", 3000, 5000, "RPM");

        dataset.AddChannelPoint("Vehicle Speed", 0, 40.0, "km/h");
        dataset.AddChannelPoint("Vehicle Speed", 1000, 60.0, "km/h");
        dataset.AddChannelPoint("Vehicle Speed", 2000, 85.0, "km/h");
        dataset.AddChannelPoint("Vehicle Speed", 3000, 110.0, "km/h");

        var samples = RoadDynoEngine.ExtractSamples(dataset);

        Assert.Equal(4, samples.Count);
        Assert.Equal(2000.0, samples[0].EngineRpm);
        Assert.Equal(40.0, samples[0].GpsSpeedKmh);
        Assert.Equal(5000.0, samples[3].EngineRpm);
        Assert.Equal(110.0, samples[3].GpsSpeedKmh);
    }

    [Fact]
    public void RoadDynoRun_ToDynoPullSummary_BridgesSeamlessly()
    {
        var roadRun = new RoadDynoRun
        {
            RunId = "road-123",
            Name = "Stage 1 Road Pull",
            PeakCorrectedPowerHp = 310.5,
            PeakCorrectedTorqueNm = 420.0,
            PeakEnginePowerRpm = 5800,
            PeakEngineTorqueRpm = 3500,
            StartRpm = 2200,
            EndRpm = 6600,
            DurationSeconds = 4.5,
            Performance = new AccelerationPerformanceMetrics
            {
                ZeroToSixtyMphSec = 4.25,
                ZeroToHundredKmhSec = 4.50,
                QuarterMileSec = 12.80,
                QuarterMileTrapSpeedKmh = 178.5
            }
        };

        roadRun.Points.Add(new RoadDynoDataPoint
        {
            TimestampMs = 100,
            Rpm = 2500,
            SpeedKmh = 50.0,
            CorrectedEnginePowerHp = 150.0,
            CorrectedEngineTorqueNm = 380.0
        });

        // 1. Bridge to DynoPullSummary
        var summary = roadRun.ToDynoPullSummary();
        Assert.Equal("road-123", summary.PullId);
        Assert.Equal("ROAD_MEASURED", summary.Type);
        Assert.Equal(310.5, summary.PeakPowerHp);
        Assert.Equal(420.0, summary.PeakTorqueNm);
        Assert.Single(summary.Points);

        // 2. Bridge to VehiclePerformanceSummary
        var perf = roadRun.ToVehiclePerformanceSummary();
        Assert.Equal(4.25, perf.ZeroToSixtyMphSeconds);
        Assert.Equal(4.50, perf.ZeroToHundredKmhSeconds);
        Assert.Equal(12.80, perf.QuarterMileSeconds);
        Assert.Equal("ROAD_MEASURED", perf.SourceType);
    }

    [Fact]
    public void CalculatedSignalEngine_EvaluateDataSet_AppendsCalculatedChannel()
    {
        var engine = new CalculatedSignalEngine();
        engine.RegisterSignal(new CalculatedSignalDefinition("kw_calc", "PowerKw", "TorqueNm * EngineRpm / 9548.8", "kW"));

        var dataset = new CanonicalLogDataSet();
        dataset.AddChannelPoint("EngineRpm", 0, 3000);
        dataset.AddChannelPoint("EngineRpm", 1000, 4000);
        dataset.AddChannelPoint("TorqueNm", 0, 350);
        dataset.AddChannelPoint("TorqueNm", 1000, 380);

        engine.EvaluateDataSet(dataset);

        Assert.True(dataset.Channels.ContainsKey("PowerKw"));
        var kwChannel = dataset.Channels["PowerKw"];
        Assert.Equal(2, kwChannel.SampleCount);
        // At t=0: 350 * 3000 / 9548.8 ~ 109.96 kW
        Assert.InRange(kwChannel.Values[0], 109.0, 111.0);
    }

    [Fact]
    public void DiagnosticExchange_ContainsVehicleAndSessionMetadata()
    {
        var exchange = new DiagnosticExchange
        {
            VehicleProfileId = "v-golf-r-2021",
            SessionId = "sess-track-001",
            EcuName = "ECM",
            RequestCanId = 0x7E0,
            ResponseCanId = 0x7E8
        };

        Assert.Equal("v-golf-r-2021", exchange.VehicleProfileId);
        Assert.Equal("sess-track-001", exchange.SessionId);
    }
}
