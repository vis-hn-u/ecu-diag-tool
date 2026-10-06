using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Dbc;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Diagnostics.Obd;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.Diagnostics.Uds;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Hardware.Transports;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Replay;
using CanEngineeringPlatform.Core.Topology;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class RegressionAndIntegrationAuditTests
{
    // =========================================================================
    // 1. ESP8266 Classic CAN Frames
    // =========================================================================
    [Fact]
    public async Task Audit_01_Esp8266_ClassicCan_PacketDecoding_And_PipelineDelivery()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        using var espTransport = new Esp8266Transport();

        Assert.Equal("esp8266-gateway", espTransport.TransportId);
        Assert.Equal(TransportType.Esp8266, espTransport.Type);
        Assert.False(espTransport.Capabilities.SupportsTransmit);
        Assert.False(espTransport.Capabilities.SupportsCanFd);
        Assert.Equal(1, espTransport.Capabilities.ChannelCount);
        Assert.Equal(8, espTransport.Capabilities.MaxPayloadBytes);

        var receivedFrames = new List<CanFrame>();
        pipeline.OnFrameReceived += f => receivedFrames.Add(f);

        // Simulate ESP8266 received classic CAN frame
        byte[] payload = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 };
        var frame = CanFrame.CreateClassic(
            id: 0x301,
            data: payload,
            isExtended: false,
            channel: 0,
            sourceId: "ESP8266"
        );

        Assert.True(frame.IsValid());
        Assert.False(frame.IsCanFd);
        Assert.Equal("ESP8266", frame.SourceId);
        Assert.Equal(8, frame.ActualLength);

        await pipeline.PublishFrameAsync(frame);
        await Task.Delay(50); // Allow pipeline channel batch to process

        Assert.Single(receivedFrames);
        var rx = receivedFrames[0];
        Assert.Equal(0x301u, rx.Id);
        Assert.Equal("ESP8266", rx.SourceId);
        Assert.Equal(8, rx.Dlc);
        Assert.False(rx.IsCanFd);
    }

    // =========================================================================
    // 2. Waveshare Classic CAN Frames
    // =========================================================================
    [Fact]
    public void Audit_02_Waveshare_ClassicCan_Frames_Ch0_And_Ch1()
    {
        using var wsTransport = new WaveshareCanFdTransport();
        Assert.Equal(2, wsTransport.Capabilities.ChannelCount);
        Assert.True(wsTransport.Capabilities.SupportsCanFd);
        Assert.True(wsTransport.Capabilities.SupportsTransmit);

        // Classic CAN frame on Waveshare Channel 0 (CAN1)
        var ch0Frame = CanFrame.CreateClassic(
            id: 0x100,
            data: new byte[] { 0x01, 0x02, 0x03, 0x04 },
            channel: 0,
            sourceId: "Waveshare-CAN1"
        );

        // Classic CAN frame on Waveshare Channel 1 (CAN2)
        var ch1Frame = CanFrame.CreateClassic(
            id: 0x200,
            data: new byte[] { 0x0A, 0x0B, 0x0C, 0x0D, 0x0E },
            channel: 1,
            sourceId: "Waveshare-CAN2"
        );

        Assert.True(ch0Frame.IsValid());
        Assert.False(ch0Frame.IsCanFd);
        Assert.Equal(0, ch0Frame.Channel);
        Assert.Equal("Waveshare-CAN1", ch0Frame.SourceId);
        Assert.Equal(4, ch0Frame.ActualLength);

        Assert.True(ch1Frame.IsValid());
        Assert.False(ch1Frame.IsCanFd);
        Assert.Equal(1, ch1Frame.Channel);
        Assert.Equal("Waveshare-CAN2", ch1Frame.SourceId);
        Assert.Equal(5, ch1Frame.ActualLength);
    }

    // =========================================================================
    // 3. Waveshare CAN FD Frames (up to 64 bytes)
    // =========================================================================
    [Fact]
    public void Audit_03_Waveshare_CanFd_64Byte_Payload_BRS_ESI()
    {
        byte[] payload64 = new byte[64];
        for (int i = 0; i < 64; i++) payload64[i] = (byte)(0xA0 + i);

        var fdFrame = CanFrame.CreateCanFd(
            id: 0x456,
            data: payload64,
            isExtended: false,
            bitRateSwitch: true,
            errorStateIndicator: false,
            timestampUs: 999999,
            channel: 1,
            sourceId: "Waveshare-CAN2"
        );

        Assert.True(fdFrame.IsValid());
        Assert.True(fdFrame.IsCanFd);
        Assert.True(fdFrame.BitRateSwitch);
        Assert.False(fdFrame.ErrorStateIndicator);
        Assert.Equal(15, fdFrame.Dlc);
        Assert.Equal(64, fdFrame.ActualLength);
        Assert.Equal(1, fdFrame.Channel);
        Assert.Equal("Waveshare-CAN2", fdFrame.SourceId);
        Assert.Equal((byte)0xA0, fdFrame.Data[0]);
        Assert.Equal((byte)(0xA0 + 63), fdFrame.Data[63]);
    }

    // =========================================================================
    // 4. Standard (11-bit) and Extended (29-bit) IDs
    // =========================================================================
    [Fact]
    public void Audit_04_Standard_And_Extended_Id_Boundaries()
    {
        // Standard ID max: 0x7FF
        var stdValid = CanFrame.CreateClassic(0x7FF, new byte[] { 1 });
        Assert.True(stdValid.IsValid());
        Assert.False(stdValid.IsExtended);

        // Standard ID exceeding 0x7FF without FlagExtended: invalid
        var stdInvalid = new CanFrame(0, 0x800, 1, 0, new byte[] { 1 });
        Assert.False(stdInvalid.IsValid());

        // Extended ID max: 0x1FFFFFFF
        var extValid = CanFrame.CreateClassic(0x1FFFFFFF, new byte[] { 1 }, isExtended: true);
        Assert.True(extValid.IsValid());
        Assert.True(extValid.IsExtended);

        // Extended ID exceeding 0x1FFFFFFF: invalid
        var extInvalid = new CanFrame(0, 0x20000000, 1, CanFrame.FlagExtended, new byte[] { 1 });
        Assert.False(extInvalid.IsValid());
    }

    // =========================================================================
    // 5. Payload Size Transitions (8-byte vs 64-byte) & Replay / Logger
    // =========================================================================
    [Fact]
    public void Audit_05_PayloadSizeTransitions_Logger_And_Replay_Preserves64Bytes()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var session = logger.StartLogging("AuditSession_FD");

        byte[] payload8 = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var classicFrame = CanFrame.CreateClassic(0x123, payload8, channel: 0, sourceId: "ESP8266");

        byte[] payload64 = new byte[64];
        for (int i = 0; i < 64; i++) payload64[i] = (byte)(i + 1);
        var fdFrame = CanFrame.CreateCanFd(0x18DAF110, payload64, isExtended: true, bitRateSwitch: true, channel: 1, sourceId: "Waveshare-CAN2");

        logger.ProcessFrame(classicFrame);
        logger.ProcessFrame(fdFrame);

        string csv = logger.ExportCsv(session.Id);
        Assert.Contains("TimestampUs,IdHex,IdDec,Type,RTR,DLC,DataHex,Channel,Protocol,BRS,ESI,SourceId", csv);
        Assert.Contains("Classic CAN", csv);
        Assert.Contains("CAN FD", csv);
        Assert.Contains("Waveshare-CAN2", csv);

        // Replay service load
        using var replay = new CanReplayService(pipeline);
        bool loaded = replay.LoadFromCsv(csv, "ReplayFD");
        Assert.True(loaded);

        var status = replay.GetStatus();
        Assert.True(status.IsLoaded);
        Assert.Equal(2, status.TotalFrames);
    }

    // =========================================================================
    // 6. Two Waveshare Channels Isolation
    // =========================================================================
    [Fact]
    public async Task Audit_06_Two_Channels_Isolated_Dispatch()
    {
        using var sim = new SimulationTransport();
        await sim.ConnectAsync();

        var ch0Frames = new List<CanFrame>();
        var ch1Frames = new List<CanFrame>();

        sim.OnFrameReceived += f =>
        {
            if (f.Channel == 0) ch0Frames.Add(f);
            else if (f.Channel == 1) ch1Frames.Add(f);
        };

        var f0 = CanFrame.CreateClassic(0x100, new byte[] { 0x01 }, channel: 0);
        var f1 = CanFrame.CreateCanFd(0x200, new byte[] { 0x02, 0x03 }, channel: 1);

        await sim.SendFrameAsync(f0);
        await sim.SendFrameAsync(f1);

        Assert.Single(ch0Frames);
        Assert.Equal(0x100u, ch0Frames[0].Id);
        Assert.Equal(0, ch0Frames[0].Channel);

        Assert.Single(ch1Frames);
        Assert.Equal(0x200u, ch1Frames[0].Id);
        Assert.Equal(1, ch1Frames[0].Channel);
    }

    // =========================================================================
    // 7. BRS, ESI, and RTR Flags
    // =========================================================================
    [Fact]
    public void Audit_07_Brs_Esi_Rtr_Flag_Validations()
    {
        // 1. Classic CAN with RTR is valid
        var classicRtr = CanFrame.CreateClassic(0x100, Array.Empty<byte>(), isRtr: true);
        Assert.True(classicRtr.IsValid());
        Assert.True(classicRtr.IsRtr);
        Assert.False(classicRtr.IsCanFd);

        // 2. CAN FD with RTR is ILLEGAL per ISO 11898-1:2015
        var fdWithRtr = new CanFrame(0, 0x100, 8, (byte)(CanFrame.FlagCanFd | CanFrame.FlagRtr), new byte[8]);
        Assert.False(fdWithRtr.IsValid());

        // 3. CAN FD with BRS and ESI
        var fdBrsEsi = CanFrame.CreateCanFd(0x200, new byte[12], bitRateSwitch: true, errorStateIndicator: true);
        Assert.True(fdBrsEsi.IsValid());
        Assert.True(fdBrsEsi.BitRateSwitch);
        Assert.True(fdBrsEsi.ErrorStateIndicator);
        Assert.False(fdBrsEsi.IsRtr);
    }

    // =========================================================================
    // 8. Hardware Disconnect
    // =========================================================================
    [Fact]
    public async Task Audit_08_Hardware_Disconnect_Handling()
    {
        using var sim = new SimulationTransport();
        await sim.ConnectAsync();
        Assert.True(sim.GetStatus().IsConnected);

        // Disconnect
        await sim.DisconnectAsync();
        Assert.False(sim.GetStatus().IsConnected);

        // Sending when disconnected returns false
        var testFrame = CanFrame.CreateClassic(0x123, new byte[] { 1 });
        bool sent = await sim.SendFrameAsync(testFrame);
        Assert.False(sent);
    }

    // =========================================================================
    // 9. Malformed Frames Rejected Gracefully by Pipeline
    // =========================================================================
    [Fact]
    public async Task Audit_09_Malformed_Frames_Rejected()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);

        var received = new List<CanFrame>();
        var frameReceivedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        pipeline.OnFrameReceived += f =>
        {
            lock (received)
            {
                received.Add(f);
                frameReceivedTcs.TrySetResult(true);
            }
        };

        // Malformed frame 1: Classic CAN with DLC 12
        var malformed1 = new CanFrame(0, 0x100, 12, 0, new byte[12]);
        Assert.False(malformed1.IsValid());
        await pipeline.PublishFrameAsync(malformed1);

        // Malformed frame 2: Reserved flag bits set
        var malformed2 = CanFrame.CreateClassic(0x100, new byte[8]);
        malformed2.Flags = 0x80;
        Assert.False(malformed2.IsValid());
        await pipeline.PublishFrameAsync(malformed2);

        // Malformed frame 3: CAN FD with RTR
        var malformed3 = new CanFrame(0, 0x100, 8, (byte)(CanFrame.FlagCanFd | CanFrame.FlagRtr), new byte[8]);
        Assert.False(malformed3.IsValid());
        await pipeline.PublishFrameAsync(malformed3);

        // Valid frame
        var validFrame = CanFrame.CreateClassic(0x100, new byte[] { 0x55 });
        Assert.True(validFrame.IsValid());
        await pipeline.PublishFrameAsync(validFrame);

        await Task.WhenAny(frameReceivedTcs.Task, Task.Delay(2000));
        await Task.Delay(50); // Settle window

        // Only the single valid frame should be processed
        lock (received)
        {
            Assert.Single(received);
            Assert.Equal(0x100u, received[0].Id);
            Assert.Equal((byte)0x55, received[0].Data[0]);
        }
    }

    // =========================================================================
    // 10. Unsupported Configuration
    // =========================================================================
    [Fact]
    public async Task Audit_10_Unsupported_Configuration_Rejection()
    {
        using var manager = new HardwareManager();
        var esp = new Esp8266Transport();
        var ws = new WaveshareCanFdTransport();

        manager.RegisterTransport(esp);
        manager.RegisterTransport(ws);

        // 1. ESP8266 does not support transmit
        await manager.SetActiveTransportAsync(esp.TransportId);
        var frame = CanFrame.CreateClassic(0x7DF, new byte[] { 0x02, 0x01, 0x00 });
        bool sentOnEsp = await manager.SendFrameAsync(frame);
        Assert.False(sentOnEsp); // Blocked safely by manager

        // 2. Direct send on ESP throws NotSupportedException
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await esp.SendFrameAsync(frame);
        });

        // 3. Configure Waveshare baud rate validation
        ws.Configure(nominalBaudRate: 500000, dataBaudRate: 2000000, channel: 1);
        Assert.Equal(500000u, ws.NominalBaudRate);
        Assert.Equal(2000000u, ws.DataBaudRate);
        Assert.Equal(1, ws.ActiveChannel);

        // Non-existent transport selection returns false
        bool invalidSet = await manager.SetActiveTransportAsync("non-existent-device");
        Assert.False(invalidSet);
    }

    // =========================================================================
    // 11. Dynamic Transport Switching
    // =========================================================================
    [Fact]
    public async Task Audit_11_Dynamic_Transport_Switching()
    {
        using var manager = new HardwareManager();
        var sim = new SimulationTransport();
        var ws = new WaveshareCanFdTransport();

        manager.RegisterTransport(sim);
        manager.RegisterTransport(ws);

        ICanTransport? activeChanged = null;
        HardwareStatus? statusChanged = null;
        manager.OnActiveTransportChanged += t => activeChanged = t;
        manager.OnHardwareStatusChanged += s => statusChanged = s;

        // Currently sim is active default. Switch to Waveshare:
        bool switchedToWs = await manager.SetActiveTransportAsync(ws.TransportId);
        Assert.True(switchedToWs);
        Assert.Equal(ws.TransportId, manager.ActiveTransportId);
        Assert.NotNull(activeChanged);
        Assert.Equal(ws.TransportId, activeChanged.TransportId);
        Assert.NotNull(statusChanged);

        // Switch back to Simulation:
        bool switchedBackToSim = await manager.SetActiveTransportAsync(sim.TransportId);
        Assert.True(switchedBackToSim);
        Assert.Equal(sim.TransportId, manager.ActiveTransportId);
        Assert.Equal(sim.TransportId, activeChanged.TransportId);
        Assert.True(sim.GetStatus().IsConnected);
    }

    // =========================================================================
    // 12. Existing DBC, ISO-TP, UDS, and OBD Functionality
    // =========================================================================
    [Fact]
    public async Task Audit_12_Existing_Dbc_Uds_Obd_IsoTp_Functional_Integrity()
    {
        // 1. DBC Signal Decoding (Classic + CAN FD with actual length)
        string dbcContent = @"
BO_ 256 EngineData: 8 EngineECU
 SG_ EngineSpeed : 0|16@1+ (0.25,0) [0|8000] ""rpm"" Dashboard
 SG_ CoolantTemp : 16|8@1+ (1,-40) [-40|215] ""degC"" Dashboard

BO_ 1110 CanFdTelemetry: 64 TelemetryECU
 SG_ DeepPayloadSignal : 480|16@1+ (0.1,0) [0|6500] ""bar"" Logger
";
        var dbc = DbcParser.Parse(dbcContent);
        Assert.Equal(2, dbc.Messages.Count);

        // Decode Classic CAN
        byte[] classicBytes = new byte[8];
        ushort rawRpm = (ushort)(3000 / 0.25); // 12000 = 0x2EE0
        classicBytes[0] = (byte)(rawRpm & 0xFF);
        classicBytes[1] = (byte)((rawRpm >> 8) & 0xFF);
        classicBytes[2] = 120; // 120 - 40 = 80 degC
        var classicFrame = CanFrame.CreateClassic(256, classicBytes);
        var decodedClassic = SignalDecoder.DecodeFrame(classicFrame, dbc.Messages[0]);
        var rpmSig = decodedClassic.First(s => s.SignalName == "EngineSpeed");
        Assert.Equal(3000.0, rpmSig.EngineeringValue);

        // Decode CAN FD at byte offset 60 (bit 480)
        byte[] fdBytes = new byte[64];
        ushort rawBar = 350; // 35.0 bar
        fdBytes[60] = (byte)(rawBar & 0xFF);
        fdBytes[61] = (byte)((rawBar >> 8) & 0xFF);
        var fdFrame = CanFrame.CreateCanFd(1110, fdBytes);
        var decodedFd = SignalDecoder.DecodeFrame(fdFrame, dbc.Messages[1]);
        var deepSig = decodedFd.First(s => s.SignalName == "DeepPayloadSignal");
        Assert.Equal(35.0, deepSig.EngineeringValue);

        // 2. ISO-TP Multi-Frame Segmentation & Reassembly
        var isoTpSession = new IsoTpSession(0x7E8, 0x7E0);
        bool fcRequired = false;
        isoTpSession.OnFlowControlRequired += fc => fcRequired = true;

        // Multi-frame payload of 20 bytes
        byte[] ffData = new byte[8] { 0x10, 0x14, 1, 2, 3, 4, 5, 6 };
        var ff = new CanFrame(1000, 0x7E8, 8, 0, ffData);
        bool ffCompleted = isoTpSession.ProcessIncomingCanFrame(ff, out var msg1);
        Assert.False(ffCompleted);
        Assert.Null(msg1);
        Assert.True(fcRequired);

        byte[] cf1Data = new byte[8] { 0x21, 7, 8, 9, 10, 11, 12, 13 };
        var cf1 = new CanFrame(2000, 0x7E8, 8, 0, cf1Data);
        isoTpSession.ProcessIncomingCanFrame(cf1, out var msg2);
        Assert.Null(msg2);

        byte[] cf2Data = new byte[8] { 0x22, 14, 15, 16, 17, 18, 19, 20 };
        var cf2 = new CanFrame(3000, 0x7E8, 8, 0, cf2Data);
        bool cf2Completed = isoTpSession.ProcessIncomingCanFrame(cf2, out var finalMsg);

        Assert.True(cf2Completed);
        Assert.NotNull(finalMsg);
        Assert.Equal(20, finalMsg.Length);
        Assert.Equal(20, finalMsg.Payload.Length);
        Assert.Equal(1, finalMsg.Payload[0]);
        Assert.Equal(20, finalMsg.Payload[19]);

        // 3. UDS and OBD Diagnostics Simulation
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var isoTpRouter = new IsoTpRouter();
        var ecuSim = new EcuSimulator(pipeline, isoTpRouter);
        using var diagService = new DiagnosticSessionService(pipeline, isoTpRouter, ecuSim);

        // Send UDS 0x22 DID 0xF190 (VIN) to ECM (0x7E0)
        byte[] udsReq = new byte[] { 0x22, 0xF1, 0x90 };
        var exchange = await diagService.SendRequestAsync(0x7E0, udsReq, "ECM", isSimulation: true);

        for (int i = 0; i < 15 && diagService.GetEcuInfo("ECM")?.Vin == null; i++)
        {
            await Task.Delay(50);
        }

        var exchanges = diagService.GetRecentExchanges();
        Assert.NotEmpty(exchanges);
        var completed = exchanges.FirstOrDefault(e => e.Id == exchange.Id);
        Assert.NotNull(completed);
        Assert.True(completed.IsComplete);
        Assert.True(completed.IsSuccess);
        Assert.Equal("UDS", completed.Protocol);

        // Verify VIN was automatically decoded
        var ecm = diagService.GetEcuInfo("ECM");
        Assert.NotNull(ecm);
        Assert.Equal("1FA6P8CF5H5123456", ecm.Vin);

        // Clear DTCs (Service 0x14)
        await diagService.ClearDtcsAsync(0x7E0, "ECM", isSimulation: true);
        var recentAfterClear = diagService.GetRecentExchanges();
        Assert.True(recentAfterClear.Count >= 2);
    }
}
