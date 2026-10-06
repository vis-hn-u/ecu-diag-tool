using System;
using System.Linq;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Hardware.Transports;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Topology;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class HardwareTransportTests
{
    [Fact]
    public void CanFrame_CanFdProperties_CorrectlyCalculated()
    {
        // 64-byte CAN FD Frame with BRS and ESI
        byte[] payload64 = new byte[64];
        for (int i = 0; i < 64; i++) payload64[i] = (byte)i;

        byte flags = CanFrame.FlagCanFd | CanFrame.FlagBrs | CanFrame.FlagEsi | CanFrame.FlagTx;
        var frame = new CanFrame(
            timestampUs: 123456,
            id: 0x123,
            dlc: 15,
            flags: flags,
            data: payload64,
            channel: 1,
            sourceId: "Waveshare-CAN2"
        );

        Assert.True(frame.IsValid());
        Assert.True(frame.IsCanFd);
        Assert.True(frame.BitRateSwitch);
        Assert.True(frame.ErrorStateIndicator);
        Assert.Equal(CanDirection.Tx, frame.Direction);
        Assert.Equal(1, frame.Channel);
        Assert.Equal("Waveshare-CAN2", frame.SourceId);
        Assert.Equal(64, frame.ActualLength);
        Assert.Equal(15, frame.Dlc);
        Assert.False(frame.IsExtended);
        Assert.False(frame.IsRtr);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(8, 8)]
    [InlineData(9, 12)]
    [InlineData(10, 16)]
    [InlineData(11, 20)]
    [InlineData(12, 24)]
    [InlineData(13, 32)]
    [InlineData(14, 48)]
    [InlineData(15, 64)]
    public void CanFrame_DlcToLength_MatchesCanFdStandard(byte dlc, int expectedLength)
    {
        Assert.Equal(expectedLength, CanFrame.DlcToLength(dlc));
        Assert.Equal(dlc, CanFrame.LengthToDlc(expectedLength));
    }

    [Fact]
    public void CanFrame_ClassicCan_RejectsDlcGreaterThan8()
    {
        byte[] payload = new byte[8];
        var classicFrame = new CanFrame(0, 0x100, 9, 0, payload);
        Assert.False(classicFrame.IsValid());
    }

    [Fact]
    public void CanFrame_CanFd_AcceptsUpTo64Bytes()
    {
        byte[] payload64 = new byte[64];
        var fdFrame = new CanFrame(0, 0x100, 15, CanFrame.FlagCanFd, payload64);
        Assert.True(fdFrame.IsValid());

        var invalidFdFrame = new CanFrame(0, 0x100, 16, CanFrame.FlagCanFd, payload64);
        Assert.False(invalidFdFrame.IsValid());
    }

    [Fact]
    public async Task HardwareManager_RegisterAndSwitchActiveTransport()
    {
        using var manager = new HardwareManager();
        var sim = new SimulationTransport();
        manager.RegisterTransport(sim);

        var transports = manager.GetAllTransports();
        Assert.Single(transports);
        Assert.Equal(sim.TransportId, transports[0].TransportId);

        // Select active transport
        bool selected = await manager.SetActiveTransportAsync(sim.TransportId);
        Assert.True(selected);
        Assert.Equal(sim.TransportId, manager.ActiveTransportId);

        var status = manager.GetStatus();
        Assert.NotNull(status);
        Assert.True(status.IsConnected);
        Assert.Equal(TransportType.Simulation, status.Type);

        // Verify sending frame routes through simulation
        CanFrame? observedTx = null;
        manager.OnFrameReceived += f => observedTx = f;

        var testFrame = new CanFrame(0, 0x7E0, 8, 0, new byte[] { 0x02, 0x10, 0x01, 0, 0, 0, 0, 0 });
        bool sent = await manager.SendFrameAsync(testFrame);
        Assert.True(sent);
        Assert.NotNull(observedTx);
        Assert.Equal(0x7E0u, observedTx.Value.Id);
        Assert.Equal(CanDirection.Tx, observedTx.Value.Direction);
    }

    [Fact]
    public async Task Esp8266Transport_EnforcesListenOnly()
    {
        using var esp = new Esp8266Transport();
        Assert.False(esp.Capabilities.SupportsTransmit);
        Assert.Equal(1, esp.Capabilities.ChannelCount);
        Assert.Equal(8, esp.Capabilities.MaxPayloadBytes);

        var testFrame = new CanFrame(0, 0x100, 8, 0, new byte[8]);
        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await esp.SendFrameAsync(testFrame);
        });
    }

    [Fact]
    public void WaveshareCanFdTransport_ExposesDualChannelFdCapabilities()
    {
        using var ws = new WaveshareCanFdTransport();
        Assert.True(ws.Capabilities.SupportsCanFd);
        Assert.True(ws.Capabilities.SupportsBitRateSwitch);
        Assert.True(ws.Capabilities.SupportsTransmit);
        Assert.Equal(2, ws.Capabilities.ChannelCount);
        Assert.Equal(64, ws.Capabilities.MaxPayloadBytes);
        Assert.Equal(TransportType.WaveshareCanFd, ws.Type);

        var status = ws.GetStatus();
        Assert.False(status.IsConnected);
    }

    [Fact]
    public async Task DiagnosticSessionService_RoutesThroughHardwareManager()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var isoTp = new IsoTpRouter();
        var sim = new EcuSimulator(pipeline, isoTp);

        using var hwManager = new HardwareManager();
        var simTransport = new SimulationTransport();
        hwManager.RegisterTransport(simTransport);
        await hwManager.SetActiveTransportAsync(simTransport.TransportId);

        using var diagService = new DiagnosticSessionService(pipeline, isoTp, sim, hwManager);

        CanFrame? capturedTxFrame = null;
        simTransport.OnFrameReceived += f =>
        {
            if (f.Direction == CanDirection.Tx)
            {
                capturedTxFrame = f;
            }
        };

        // Send a non-simulation request through DiagnosticSessionService
        byte[] req = new byte[] { 0x10, 0x01 }; // DiagnosticSessionControl Default
        var exchange = await diagService.SendRequestAsync(0x7E0, req, "ECM", isSimulation: false);

        Assert.NotNull(exchange);
        Assert.NotNull(capturedTxFrame);
        Assert.Equal(0x7E0u, capturedTxFrame.Value.Id);
        Assert.Equal(0x10, capturedTxFrame.Value.Data[1]); // ISO-TP SF data byte 1
    }

    [Fact]
    public void CanFrame_CanFd_WithRtr_IsInvalid()
    {
        // ISO 11898-1: CAN FD does not allow RTR
        byte flags = CanFrame.FlagCanFd | CanFrame.FlagRtr;
        var frame = new CanFrame(0, 0x123, 8, flags, new byte[8]);
        Assert.False(frame.IsValid());
    }

    [Fact]
    public void CanFrame_Extended29Bit_CanFd_IsValid()
    {
        byte[] payload = new byte[64];
        var frame = CanFrame.CreateCanFd(
            id: 0x18DAF110,
            data: payload,
            isExtended: true,
            bitRateSwitch: true,
            channel: 1,
            sourceId: "Waveshare-CAN2",
            direction: CanDirection.Tx
        );

        Assert.True(frame.IsValid());
        Assert.True(frame.IsExtended);
        Assert.True(frame.IsCanFd);
        Assert.True(frame.BitRateSwitch);
        Assert.Equal(0x18DAF110u, frame.Id);
        Assert.Equal(15, frame.Dlc);
        Assert.Equal(64, frame.ActualLength);
        Assert.Equal(CanDirection.Tx, frame.Direction);
        Assert.Equal(1, frame.Channel);
        Assert.Equal("Waveshare-CAN2", frame.SourceId);
    }

    [Fact]
    public async Task HardwareManager_UnknownTransport_ReturnsFalse()
    {
        using var manager = new HardwareManager();
        bool result = await manager.SetActiveTransportAsync("non-existent-id");
        Assert.False(result);
        Assert.Null(manager.ActiveTransportId);
    }

    [Fact]
    public async Task HardwareManager_SendFrame_WithListenOnlyTransport_ReturnsFalse()
    {
        using var manager = new HardwareManager();
        var esp = new Esp8266Transport();
        manager.RegisterTransport(esp);
        await manager.SetActiveTransportAsync(esp.TransportId);

        var frame = CanFrame.CreateClassic(0x7DF, new byte[] { 0x01, 0x00 });
        bool sent = await manager.SendFrameAsync(frame);
        Assert.False(sent);
    }

    [Fact]
    public void WaveshareCanFdTransport_Configure_UpdatesBitratesAndChannel()
    {
        using var ws = new WaveshareCanFdTransport();
        Assert.Equal(500000u, ws.NominalBaudRate);
        Assert.Equal(2000000u, ws.DataBaudRate);
        Assert.Equal(0, ws.ActiveChannel);

        ws.Configure(1000000u, 4000000u, 1);
        Assert.Equal(1000000u, ws.NominalBaudRate);
        Assert.Equal(4000000u, ws.DataBaudRate);
        Assert.Equal(1, ws.ActiveChannel);

        var status = ws.GetStatus();
        Assert.Contains("CAN2", status.ConnectionDetails);
    }

    [Fact]
    public void Esp8266Transport_ConfigureAddress_UpdatesGatewayAddress()
    {
        using var esp = new Esp8266Transport();
        Assert.Equal("192.168.4.1:8088", esp.GatewayAddress);

        esp.ConfigureAddress("192.168.1.150:8088");
        Assert.Equal("192.168.1.150:8088", esp.GatewayAddress);

        var status = esp.GetStatus();
        Assert.Equal("ws://192.168.1.150:8088", status.ConnectionDetails);
    }

    [Fact]
    public async Task WaveshareCanFdTransport_PhysicalHardware_ReportsNotConnected_WhenDeviceAbsent()
    {
        using var ws = new WaveshareCanFdTransport { ForceBenchLoopback = false };
        bool connected = await ws.ConnectAsync();

        // On this host machine without physical USB-CAN-FD-B hardware attached:
        Assert.False(connected);
        Assert.False(ws.IsHardwareDevicePresent);
        Assert.False(ws.GetStatus().IsConnected);
        Assert.Contains("not detected", ws.GetStatus().LastError);
    }

    [Fact]
    public async Task WaveshareCanFdTransport_Can1ToCan2_BenchLoopback_ClassicCan_11Bit_8Bytes()
    {
        using var ws = new WaveshareCanFdTransport { ForceBenchLoopback = true };
        await ws.ConnectAsync();

        CanFrame? receivedFrame = null;
        ws.OnFrameReceived += f => receivedFrame = f;

        byte[] payload8 = new byte[] { 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 };
        var txFrame = CanFrame.CreateClassic(
            id: 0x321,
            data: payload8,
            isExtended: false,
            channel: 0,
            sourceId: "Waveshare-CAN1"
        );

        bool sent = await ws.SendFrameAsync(txFrame);
        Assert.True(sent);

        // Wait for async loopback delivery
        for (int i = 0; i < 20 && receivedFrame == null; i++) await Task.Delay(25);

        Assert.NotNull(receivedFrame);
        Assert.Equal(0x321u, receivedFrame.Value.Id);
        Assert.False(receivedFrame.Value.IsExtended);
        Assert.False(receivedFrame.Value.IsCanFd);
        Assert.False(receivedFrame.Value.BitRateSwitch);
        Assert.Equal(8, receivedFrame.Value.Dlc);
        Assert.Equal(8, receivedFrame.Value.ActualLength);
        Assert.Equal(1, receivedFrame.Value.Channel); // Received on CAN2
        Assert.Equal("Waveshare-CAN2", receivedFrame.Value.SourceId);
        Assert.True(receivedFrame.Value.TimestampUs > 0);
        Assert.Equal(payload8, receivedFrame.Value.Data);
    }

    [Fact]
    public async Task WaveshareCanFdTransport_Can2ToCan1_BenchLoopback_ClassicCan_29Bit_8Bytes()
    {
        using var ws = new WaveshareCanFdTransport { ForceBenchLoopback = true };
        await ws.ConnectAsync();

        CanFrame? receivedFrame = null;
        ws.OnFrameReceived += f => receivedFrame = f;

        byte[] payload8 = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00, 0x11 };
        var txFrame = CanFrame.CreateClassic(
            id: 0x18DAF110,
            data: payload8,
            isExtended: true,
            channel: 1,
            sourceId: "Waveshare-CAN2"
        );

        bool sent = await ws.SendFrameAsync(txFrame);
        Assert.True(sent);

        for (int i = 0; i < 20 && receivedFrame == null; i++) await Task.Delay(25);

        Assert.NotNull(receivedFrame);
        Assert.Equal(0x18DAF110u, receivedFrame.Value.Id);
        Assert.True(receivedFrame.Value.IsExtended);
        Assert.False(receivedFrame.Value.IsCanFd);
        Assert.Equal(8, receivedFrame.Value.Dlc);
        Assert.Equal(8, receivedFrame.Value.ActualLength);
        Assert.Equal(0, receivedFrame.Value.Channel); // Received on CAN1
        Assert.Equal("Waveshare-CAN1", receivedFrame.Value.SourceId);
        Assert.Equal(payload8, receivedFrame.Value.Data);
    }

    [Fact]
    public async Task WaveshareCanFdTransport_Can1ToCan2_BenchLoopback_CanFd_11Bit_64Bytes_BRS()
    {
        using var ws = new WaveshareCanFdTransport { ForceBenchLoopback = true };
        await ws.ConnectAsync();

        CanFrame? receivedFrame = null;
        ws.OnFrameReceived += f => receivedFrame = f;

        byte[] payload64 = new byte[64];
        for (int i = 0; i < 64; i++) payload64[i] = (byte)(i * 2 + 1);

        var txFrame = CanFrame.CreateCanFd(
            id: 0x456,
            data: payload64,
            isExtended: false,
            bitRateSwitch: true,
            channel: 0,
            sourceId: "Waveshare-CAN1"
        );

        bool sent = await ws.SendFrameAsync(txFrame);
        Assert.True(sent);

        for (int i = 0; i < 20 && receivedFrame == null; i++) await Task.Delay(25);

        Assert.NotNull(receivedFrame);
        Assert.Equal(0x456u, receivedFrame.Value.Id);
        Assert.False(receivedFrame.Value.IsExtended);
        Assert.True(receivedFrame.Value.IsCanFd);
        Assert.True(receivedFrame.Value.BitRateSwitch);
        Assert.Equal(15, receivedFrame.Value.Dlc);
        Assert.Equal(64, receivedFrame.Value.ActualLength);
        Assert.Equal(1, receivedFrame.Value.Channel); // Received on CAN2
        Assert.Equal("Waveshare-CAN2", receivedFrame.Value.SourceId);
        Assert.Equal(payload64, receivedFrame.Value.Data);
    }

    [Fact]
    public async Task WaveshareCanFdTransport_Can1ToCan2_BenchLoopback_CanFd_29Bit_64Bytes_BRS()
    {
        using var ws = new WaveshareCanFdTransport { ForceBenchLoopback = true };
        await ws.ConnectAsync();

        CanFrame? receivedFrame = null;
        ws.OnFrameReceived += f => receivedFrame = f;

        byte[] payload64 = new byte[64];
        for (int i = 0; i < 64; i++) payload64[i] = (byte)(0xFF - i);

        var txFrame = CanFrame.CreateCanFd(
            id: 0x18FEE000,
            data: payload64,
            isExtended: true,
            bitRateSwitch: true,
            channel: 0,
            sourceId: "Waveshare-CAN1"
        );

        bool sent = await ws.SendFrameAsync(txFrame);
        Assert.True(sent);

        for (int i = 0; i < 20 && receivedFrame == null; i++) await Task.Delay(25);

        Assert.NotNull(receivedFrame);
        Assert.Equal(0x18FEE000u, receivedFrame.Value.Id);
        Assert.True(receivedFrame.Value.IsExtended);
        Assert.True(receivedFrame.Value.IsCanFd);
        Assert.True(receivedFrame.Value.BitRateSwitch);
        Assert.Equal(15, receivedFrame.Value.Dlc);
        Assert.Equal(64, receivedFrame.Value.ActualLength);
        Assert.Equal(1, receivedFrame.Value.Channel);
        Assert.Equal("Waveshare-CAN2", receivedFrame.Value.SourceId);
        Assert.Equal(payload64, receivedFrame.Value.Data);
    }

    [Fact]
    public async Task WaveshareCanFdTransport_Disconnect_Reconnect_Handling()
    {
        using var ws = new WaveshareCanFdTransport { ForceBenchLoopback = true };

        // 1. Initial Connect
        bool connected = await ws.ConnectAsync();
        Assert.True(connected);
        Assert.True(ws.GetStatus().IsConnected);

        // 2. Disconnect
        await ws.DisconnectAsync();
        Assert.False(ws.GetStatus().IsConnected);

        // Sending when disconnected returns false
        var testFrame = CanFrame.CreateClassic(0x100, new byte[] { 1, 2, 3 });
        bool sentWhenDisconnected = await ws.SendFrameAsync(testFrame);
        Assert.False(sentWhenDisconnected);

        // 3. Reconnect
        bool reconnected = await ws.ConnectAsync();
        Assert.True(reconnected);
        Assert.True(ws.GetStatus().IsConnected);

        // Sending after reconnect succeeds
        bool sentAfterReconnect = await ws.SendFrameAsync(testFrame);
        Assert.True(sentAfterReconnect);
    }

    [Fact]
    public async Task WaveshareCanFdTransport_EndToEnd_PipelineAndAnalyzer_Delivery()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);

        using var manager = new HardwareManager(pipeline);
        using var ws = new WaveshareCanFdTransport { ForceBenchLoopback = true };
        manager.RegisterTransport(ws);

        await manager.SetActiveTransportAsync(ws.TransportId);

        var analyzerObservedFrames = new List<CanFrame>();
        pipeline.OnFrameReceived += f => analyzerObservedFrames.Add(f);

        // Send 64-byte CAN FD frame on CAN1 (channel 0)
        byte[] payload64 = new byte[64];
        for (int i = 0; i < 64; i++) payload64[i] = (byte)i;

        var txFrame = CanFrame.CreateCanFd(
            id: 0x7E8,
            data: payload64,
            isExtended: false,
            bitRateSwitch: true,
            channel: 0,
            sourceId: "Waveshare-CAN1"
        );

        bool sent = await manager.SendFrameAsync(txFrame);
        Assert.True(sent);

        for (int i = 0; i < 20 && analyzerObservedFrames.Count == 0; i++)
        {
            await Task.Delay(25);
        }

        // Frame delivered cleanly to pipeline without special Waveshare code in downstream services
        Assert.Single(analyzerObservedFrames);
        var rx = analyzerObservedFrames[0];
        Assert.Equal(0x7E8u, rx.Id);
        Assert.True(rx.IsCanFd);
        Assert.True(rx.BitRateSwitch);
        Assert.Equal(64, rx.ActualLength);
        Assert.Equal(1, rx.Channel); // Echoed to CAN2
        Assert.Equal("Waveshare-CAN2", rx.SourceId);
        Assert.Equal(payload64, rx.Data);
    }
}

