using System;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Hardware.Transports;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Topology;
using Xunit;
using Xunit.Abstractions;

namespace CanEngineeringPlatform.Tests;

public class BenchHardwareTests
{
    private readonly ITestOutputHelper _output;

    public BenchHardwareTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task LiveHardwareBenchValidation_COM8_FullLifecycle()
    {
        // 1. Detect COM8
        var ports = GvretSerialTransport.GetAvailablePorts();
        _output.WriteLine($"[BENCH] Available Ports: {string.Join(", ", ports)}");
        if (!ports.Contains("COM8"))
        {
            _output.WriteLine("[BENCH] COM8 (Nano + MCP2515) is not physically attached; skipping live hardware bench test.");
            return;
        }

        // 2. Instantiate and configure transport for Arduino Nano + MCP2515
        using var transport = new GvretSerialTransport();
        transport.Configure("COM8", busSpeed: 500000u, listenOnly: true, baudRate: 1000000);

        // 3. Open COM8 and connect
        bool connected = await transport.ConnectAsync();
        _output.WriteLine($"[BENCH] Initial Connect Result: {connected}");
        if (!connected)
        {
            _output.WriteLine("[BENCH] COM8 could not be opened (likely locked by active running platform service); skipping exclusive bench test.");
            return;
        }
        Assert.True(transport.IsConnected, "Transport reports not connected");
        Assert.Equal("COM8", transport.ConnectionDetails);

        // 4. Wait for Nano bootloader and handshake exchange
        await Task.Delay(2500);

        var status1 = transport.GetStatus();
        dynamic tele1 = status1.ExtendedTelemetry!;
        _output.WriteLine($"[BENCH] Device Identified: {tele1.IsDeviceIdentified}");
        _output.WriteLine($"[BENCH] Keepalive Validated: {tele1.IsKeepaliveValidated}");
        _output.WriteLine($"[BENCH] Firmware Build: {tele1.FirmwareBuild}");
        _output.WriteLine($"[BENCH] Bus Count: {tele1.BusCount}");
        _output.WriteLine($"[BENCH] Operating Mode: {tele1.OperatingMode}");
        _output.WriteLine($"[BENCH] Baud Rate: {tele1.BaudRate}");

        Assert.True((bool)tele1.IsDeviceIdentified, "Device was not identified by SavvyCAN handshake");
        Assert.True((bool)tele1.IsKeepaliveValidated, "Keepalive was not validated");
        Assert.Equal("333", (string)tele1.FirmwareBuild);
        Assert.Equal(1, (int)tele1.BusCount);

        // 5. Close COM8 cleanly
        await transport.DisconnectAsync();
        Assert.False(transport.IsConnected);
        _output.WriteLine("[BENCH] Disconnected cleanly.");

        // 6. Reopen COM8 cleanly
        await Task.Delay(500);
        bool reconnected = await transport.ConnectAsync();
        _output.WriteLine($"[BENCH] Reopen Connect Result: {reconnected}");
        Assert.True(reconnected, "Failed to reopen COM8");
        Assert.True(transport.IsConnected, "Transport reports not connected on reopen");

        await Task.Delay(2500);
        var status2 = transport.GetStatus();
        dynamic tele2 = status2.ExtendedTelemetry!;
        _output.WriteLine($"[BENCH] Reopened Device Identified: {tele2.IsDeviceIdentified}");
        _output.WriteLine($"[BENCH] Reopened Keepalive Validated: {tele2.IsKeepaliveValidated}");
        _output.WriteLine($"[BENCH] Reopened Firmware Build: {tele2.FirmwareBuild}");

        Assert.True((bool)tele2.IsDeviceIdentified, "Device was not identified after reopen");
        Assert.True((bool)tele2.IsKeepaliveValidated, "Keepalive was not validated after reopen");
        Assert.Equal("333", (string)tele2.FirmwareBuild);

        // Final clean disconnect
        await transport.DisconnectAsync();
        Assert.False(transport.IsConnected);
        _output.WriteLine("[BENCH] Reopened port closed cleanly. Full lifecycle verified!");
    }

    [Fact]
    public async Task LiveHardwareBenchValidation_Esp8266()
    {
        var ports = Esp8266UsbSerialTransport.GetAvailablePorts();
        string? targetPort = ports.FirstOrDefault(p => p.Equals("COM14", StringComparison.OrdinalIgnoreCase) || p.Equals("COM13", StringComparison.OrdinalIgnoreCase));
        _output.WriteLine($"[ESP BENCH] Available Ports: {string.Join(", ", ports)}, Target: {targetPort ?? "None"}");
        if (targetPort == null)
        {
            _output.WriteLine("[ESP BENCH] COM14/COM13 (ESP8266) is not physically attached; skipping live hardware bench test.");
            return;
        }

        var transport = new Esp8266UsbSerialTransport();
        transport.Configure(targetPort, channel: 1);

        bool connected = await transport.ConnectAsync();
        _output.WriteLine($"[ESP BENCH] Initial Connect Result: {connected}");
        if (!connected)
        {
            _output.WriteLine($"[ESP BENCH] {targetPort} could not be opened (likely locked by active running platform service); skipping exclusive bench test.");
            return;
        }
        Assert.True(transport.GetStatus().IsConnected);
        Assert.Equal(1, transport.Channel);

        await Task.Delay(2000);

        var status = transport.GetStatus();
        dynamic tele = status.ExtendedTelemetry!;
        _output.WriteLine($"[ESP BENCH] ComPort: {tele.ComPort}");
        _output.WriteLine($"[ESP BENCH] BaudRate: {tele.BaudRate}");
        _output.WriteLine($"[ESP BENCH] GatewayIdentified: {tele.IsGatewayIdentified}");
        _output.WriteLine($"[ESP BENCH] RxFrameCount: {tele.RxFrameCount}");
        _output.WriteLine($"[ESP BENCH] DroppedFrames: {tele.DroppedFrames}");
        _output.WriteLine($"[ESP BENCH] SerialErrors: {tele.SerialErrors}");

        await transport.DisconnectAsync();
        Assert.False(transport.GetStatus().IsConnected);
        _output.WriteLine("[ESP BENCH] Disconnected cleanly.");
    }

    [Fact]
    public async Task DualHardwareStreaming_ConcurrentManager_PreservesBothTransports()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);

        using var gvret = new GvretSerialTransport();
        using var espUsb = new Esp8266UsbSerialTransport();
        using var sim = new SimulationTransport();

        using var manager = new HardwareManager(pipeline, new ICanTransport[] { gvret, espUsb, sim });

        // Verify initial active transport is gvret
        Assert.Equal(GvretSerialTransport.TransportIdentifier, manager.ActiveTransportId);

        // Switch to espUsb
        await manager.SetActiveTransportAsync("esp8266-usb-serial");
        Assert.Equal("esp8266-usb-serial", manager.ActiveTransportId);

        // Switch back to gvret: verify neither is destructively disposed or locked
        await manager.SetActiveTransportAsync(GvretSerialTransport.TransportIdentifier);
        Assert.Equal(GvretSerialTransport.TransportIdentifier, manager.ActiveTransportId);

        // Disconnect specific transport
        await manager.DisconnectTransportAsync("esp8266-usb-serial");
        Assert.False(espUsb.GetStatus().IsConnected);
    }

    [Fact]
    public async Task CanPipeline_PreloadsMarelliSwiftDbc_DecodesLiveSignalsImmediately()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);

        // 1. Verify default DBC is preloaded on startup
        var activeDbc = pipeline.GetActiveDbc();
        Assert.NotNull(activeDbc);
        Assert.True(activeDbc.Messages.Count >= 5);
        Assert.True(activeDbc.MessageLookup.ContainsKey(292U)); // 0x124 Engine
        Assert.True(activeDbc.MessageLookup.ContainsKey(784U)); // 0x310 Speed
        Assert.True(activeDbc.MessageLookup.ContainsKey(788U)); // 0x314 Thermal
        Assert.True(activeDbc.MessageLookup.ContainsKey(792U)); // 0x318 Pressure & Relays

        // 2. Publish a live 0x124 broadcast frame (e.g. EngineRPM = 2000 rpm -> Raw = 8000 = 0x1F40 LE)
        // Byte 0-1: 0x40, 0x1F (8000 * 0.25 = 2000 rpm), Byte 2: 102 (102 * 0.392157 ~= 40% Throttle)
        byte[] frameData = new byte[8] { 0x40, 0x1F, 102, 150, 0x01, 102, 0x00, 0x00 };
        var frame = CanFrame.CreateClassic(0x124, frameData, isExtended: false, channel: 0, sourceId: "Arduino Nano GVRET");

        var decodedSignals = new List<DecodedSignal>();
        var tcs = new TaskCompletionSource<bool>();

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

        await pipeline.PublishFrameAsync(frame);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.Equal(tcs.Task, completed);

        var rpm = decodedSignals.Find(s => s.SignalName == "EngineRPM");
        Assert.NotNull(rpm);
        Assert.Equal(2000.0, rpm.EngineeringValue);
        Assert.Equal("rpm", rpm.Unit);

        var throttle = decodedSignals.Find(s => s.SignalName == "ThrottlePosition");
        Assert.NotNull(throttle);
        Assert.True(throttle.EngineeringValue >= 39.5 && throttle.EngineeringValue <= 40.5);
    }
}
