using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Hardware.Transports;

namespace CanEngineeringPlatform.Api.Services;

public class GatewayStatusDto
{
    public bool IsConnected { get; set; }
    public string GatewayAddress { get; set; } = "192.168.4.1:8088";
    public long TotalPacketsReceived { get; set; }
    public double IngestFps { get; set; }
    public DateTime? LastPacketTimeUtc { get; set; }
    public string? LastError { get; set; }
    public JsonElement? EspTelemetry { get; set; }
}

public interface IGatewayService
{
    GatewayStatusDto GetStatus();
    Task ConfigureAddressAsync(string address);
    Task RequestEspStatusAsync();
}

public class GatewayClientService : BackgroundService, IGatewayService
{
    private readonly IHardwareManager _hardwareManager;
    private readonly Esp8266Transport _espTransport;
    private readonly ILogger<GatewayClientService> _logger;

    public GatewayClientService(
        IHardwareManager hardwareManager,
        Esp8266Transport espTransport,
        ILogger<GatewayClientService> logger)
    {
        _hardwareManager = hardwareManager;
        _espTransport = espTransport;
        _logger = logger;
    }

    public GatewayStatusDto GetStatus()
    {
        var status = _espTransport.GetStatus();
        return new GatewayStatusDto
        {
            IsConnected = status.IsConnected,
            GatewayAddress = _espTransport.GatewayAddress,
            TotalPacketsReceived = status.TotalFramesReceived,
            IngestFps = status.RxFrameRate,
            LastPacketTimeUtc = status.LastSeenUtc,
            LastError = status.LastError,
            EspTelemetry = _espTransport.EspTelemetry
        };
    }

    public Task ConfigureAddressAsync(string address)
    {
        _espTransport.ConfigureAddress(address);
        return Task.CompletedTask;
    }

    public Task RequestEspStatusAsync()
    {
        return _espTransport.RequestEspStatusAsync();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Hardware Gateway Service starting concurrent dual hardware streaming (Nano COM8 + ESP8266 COM14)...");
        try
        {
            // Auto-enable dual hardware ingestion on startup if ports are present
            await _hardwareManager.EnableDualHardwareStreamingAsync("COM8", "COM14", stoppingToken);

            // Only auto-connect USB transports — never auto-connect the Wi-Fi WebSocket adapter.
            var active = _hardwareManager.GetActiveTransport();
            if (active != null
                && active is not Esp8266Transport      // Wi-Fi adapter — must stay idle unless user explicitly connects
                && !active.GetStatus().IsConnected)
            {
                await active.ConnectAsync(stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                // Auto-healing loop: check available serial ports and ensure connected state
                try
                {
                    var availablePorts = GvretSerialTransport.GetAvailablePorts();
                    if (availablePorts.Contains("COM8", StringComparer.OrdinalIgnoreCase))
                    {
                        var gvret = _hardwareManager.GetTransport(CanEngineeringPlatform.Core.Hardware.Transports.GvretSerialTransport.TransportIdentifier);
                        if (gvret != null && !gvret.GetStatus().IsConnected)
                        {
                            await gvret.ConnectAsync(stoppingToken);
                        }
                    }

                    if (availablePorts.Contains("COM14", StringComparer.OrdinalIgnoreCase))
                    {
                        var espUsb = _hardwareManager.GetTransport("esp8266-usb-serial");
                        if (espUsb != null && !espUsb.GetStatus().IsConnected)
                        {
                            await espUsb.ConnectAsync(stoppingToken);
                        }
                    }
                }
                catch
                {
                    // Ignore transient COM port polling errors
                }

                await Task.Delay(2000, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Hardware Gateway execution loop");
        }
    }
}
