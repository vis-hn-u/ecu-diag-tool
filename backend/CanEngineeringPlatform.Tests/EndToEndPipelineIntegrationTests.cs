using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Decoders;
using CanEngineeringPlatform.Core.Dbc;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Replay;
using CanEngineeringPlatform.Core.Topology;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class EndToEndPipelineIntegrationTests
{
    private const string SampleDbc = @"VERSION ""1.0""
BU_: ECM ABS BCM CLUSTER

BO_ 280 EngineData: 8 ECM
 SG_ EngineSpeed : 0|16@1+ (0.125,0) [0|8000] ""rpm"" CLUSTER,BCM
 SG_ EngineTorque : 16|8@1+ (1,-100) [-100|155] ""Nm"" ABS

BO_ 390 VehicleDynamics: 8 ABS
 SG_ VehicleSpeed : 7|16@0+ (0.01,0) [0|250] ""km/h"" ECM,CLUSTER
";

    [Fact]
    public void Test1_Valid20BytePacket_DecodesAccurately()
    {
        // 20-byte packet: AA 55 | TS=5000us | ID=0x123 (291) | DLC=8 | Flags=0x00 | Data=[1..8]
        byte[] raw = new byte[20]
        {
            0xAA, 0x55,
            0x88, 0x13, 0x00, 0x00, // 5000 us LE
            0x23, 0x01, 0x00, 0x00, // ID 291 LE
            0x08,                   // DLC 8
            0x00,                   // Standard Data
            0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80
        };

        bool success = BinaryPacketDecoder.TryDecode(raw, out var frame);

        Assert.True(success);
        Assert.Equal(5000U, frame.TimestampUs);
        Assert.Equal(0x123U, frame.Id);
        Assert.Equal(8, frame.Dlc);
        Assert.False(frame.IsExtended);
        Assert.False(frame.IsRtr);
        Assert.Equal(0x10, frame.Data[0]);
        Assert.Equal(0x80, frame.Data[7]);
    }

    [Fact]
    public void Test2_MalformedPackets_ProperlyRejected()
    {
        // Bad Length
        Assert.False(BinaryPacketDecoder.TryDecode(new byte[19], out _));

        // Bad Sync Bytes
        byte[] badSync = new byte[20] { 0xBB, 0x55, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        Assert.False(BinaryPacketDecoder.TryDecode(badSync, out _));

        // Bad DLC (> 8)
        byte[] badDlc = new byte[20] { 0xAA, 0x55, 0, 0, 0, 0, 0x20, 0, 0, 0, 9, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        Assert.False(BinaryPacketDecoder.TryDecode(badDlc, out _));

        // Invalid Standard CAN ID (> 0x7FF when flag is not extended)
        byte[] badId = new byte[20] { 0xAA, 0x55, 0, 0, 0, 0, 0x00, 0x08, 0x00, 0x00, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; // 0x800
        Assert.False(BinaryPacketDecoder.TryDecode(badId, out _));
    }

    [Fact]
    public void Test3_ExtendedAndRtrFlags_ParsedAccurately()
    {
        // Extended (0x01) + RTR (0x02) = Flags 0x03, Extended ID 0x18DAF110
        byte[] raw = new byte[20]
        {
            0xAA, 0x55,
            0x00, 0x00, 0x00, 0x00,
            0x10, 0xF1, 0xDA, 0x18, // 0x18DAF110 LE
            0x00,                   // DLC 0
            0x03,                   // Extended + RTR
            0, 0, 0, 0, 0, 0, 0, 0
        };

        bool success = BinaryPacketDecoder.TryDecode(raw, out var frame);

        Assert.True(success);
        Assert.Equal(0x18DAF110U, frame.Id);
        Assert.True(frame.IsExtended);
        Assert.True(frame.IsRtr);
        Assert.Equal(0, frame.Dlc);
    }

    [Fact]
    public async Task Test4_SingleAuthoritativePipeline_DeliversToAllDownstream()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);

        var dbc = DbcParser.Parse(SampleDbc);
        pipeline.SetActiveDbc(dbc);

        var receivedFrames = new List<CanFrame>();
        var decodedSignals = new List<DecodedSignal>();
        var tcs = new TaskCompletionSource<bool>();

        pipeline.OnFrameReceived += frame =>
        {
            lock (receivedFrames)
            {
                receivedFrames.Add(frame);
            }
        };

        pipeline.OnSignalsDecoded += sigs =>
        {
            lock (decodedSignals)
            {
                decodedSignals.AddRange(sigs);
                if (decodedSignals.Count >= 2)
                {
                    tcs.TrySetResult(true);
                }
            }
        };

        // EngineData (ID 280): EngineSpeed = 16000 (2000 rpm) -> 0x80, 0x3E, EngineTorque = 150 (50 Nm) -> 0x96
        byte[] data = new byte[8] { 0x80, 0x3E, 0x96, 0, 0, 0, 0, 0 };
        var frame = new CanFrame(1000, 280, 8, 0, data);

        await pipeline.PublishFrameAsync(frame);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.Equal(tcs.Task, completed);

        Assert.Single(receivedFrames);
        Assert.Equal(280U, receivedFrames[0].Id);

        var speed = decodedSignals.Find(s => s.SignalName == "EngineSpeed");
        Assert.NotNull(speed);
        Assert.Equal(2000.0, speed.EngineeringValue);
        Assert.Equal("rpm", speed.Unit);
    }

    [Fact]
    public void Test5_DbcMotorolaDecoding_CalculatesPhysicalValue()
    {
        var dbc = DbcParser.Parse(SampleDbc);
        var msg390 = dbc.Messages.Find(m => m.Id == 390)!;

        // VehicleSpeed: StartBit 7, Length 16 @0+ (0.01, 0) -> Raw 12000 = 0x2EE0 -> 120.0 km/h
        byte[] data = new byte[8] { 0x2E, 0xE0, 0, 0, 0, 0, 0, 0 };
        var frame = new CanFrame(2000, 390, 8, 0, data);

        var decoded = SignalDecoder.DecodeFrame(frame, msg390);

        Assert.Single(decoded);
        Assert.Equal("VehicleSpeed", decoded[0].SignalName);
        Assert.Equal(120.0, decoded[0].EngineeringValue);
        Assert.Equal("km/h", decoded[0].Unit);
    }

    [Fact]
    public void Test6_TopologyEngine_UpdatesDynamicNodeAndSignalPaths()
    {
        var engine = new TopologyEngine();
        var dbc = DbcParser.Parse(SampleDbc);
        engine.LoadDbc(dbc);

        var msg280 = dbc.Messages[0];
        var frame = new CanFrame(1000, 280, 8, 0, new byte[] { 0x80, 0x3E, 0x96, 0, 0, 0, 0, 0 });
        var signals = SignalDecoder.DecodeFrame(frame, msg280);

        engine.ProcessFrame(frame, msg280, signals);

        var snapshot = engine.GetSnapshot();
        Assert.Equal(1, snapshot.TotalFramesObserved);

        var ecm = snapshot.Nodes.Find(n => n.Name == "ECM");
        Assert.NotNull(ecm);
        Assert.Equal("Active", ecm.Status);
        Assert.Equal(1, ecm.MessageCount);

        var edge = snapshot.Edges.Find(e => e.SignalName == "EngineSpeed" && e.Target == "CLUSTER");
        Assert.NotNull(edge);
        Assert.Equal("Active", edge.State);
    }

    [Fact]
    public void Test7_UnknownCanId_ObservedWithoutDiscarding()
    {
        var engine = new TopologyEngine();

        // CAN ID 0x666 not in DBC
        var frame = new CanFrame(1000, 0x666, 8, 0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        engine.ProcessFrame(frame, null, null);

        var snapshot = engine.GetSnapshot();
        Assert.Equal(1, snapshot.TotalFramesObserved);

        var unknownMsg = snapshot.Messages.Find(m => m.Id == 0x666);
        Assert.NotNull(unknownMsg);
        Assert.False(unknownMsg.IsKnown);
        Assert.Equal("UNKNOWN (0x666)", unknownMsg.Name);
    }

    [Fact]
    public void Test8_LoggerService_CapturesFromSamePipelineAndExportsCsv()
    {
        var logger = new CanLoggerService();
        var session = logger.StartLogging("IntegrationSession");

        var frame = new CanFrame(5000, 0x100, 4, 0, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0, 0, 0, 0 });
        logger.ProcessFrame(frame);

        var stopped = logger.StopLogging();
        Assert.NotNull(stopped);
        Assert.Equal(1, stopped.FrameCount);

        string csv = logger.ExportCsv(stopped.Id);
        Assert.Contains("0x100", csv);
        Assert.Contains("DE AD BE EF", csv);
    }

    [Fact]
    public void Test9_SimulationReplay_IngestsViaAuthoritativePipelineOnly()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        using var replay = new CanReplayService(pipeline);

        string csv = @"TimestampUs,IdHex,IdDec,Type,RTR,DLC,DataHex
1000,0x280,280,Standard,0,8,80 3E 96 00 00 00 00 00
";
        bool loaded = replay.LoadFromCsv(csv, "SimReplay");
        Assert.True(loaded);

        var status = replay.GetStatus();
        Assert.True(status.IsLoaded);
        Assert.Equal(1, status.TotalFrames);
    }
}
