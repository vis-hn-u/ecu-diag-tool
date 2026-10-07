using System;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware.Transports;
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
        Assert.Contains("COM8", ports);

        // 2. Instantiate and configure transport for Arduino Nano + MCP2515
        using var transport = new GvretSerialTransport();
        transport.Configure("COM8", busSpeed: 500000u, listenOnly: true, baudRate: 1000000);

        // 3. Open COM8 and connect
        bool connected = await transport.ConnectAsync();
        _output.WriteLine($"[BENCH] Initial Connect Result: {connected}");
        Assert.True(connected, "Failed to open COM8");
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
}
