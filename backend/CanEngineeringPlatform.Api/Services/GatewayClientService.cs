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
        _logger.LogInformation("Hardware Gateway Service starting active transport...");
        try
        {
            var active = _hardwareManager.GetActiveTransport();
            if (active != null)
            {
                await active.ConnectAsync(stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
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
