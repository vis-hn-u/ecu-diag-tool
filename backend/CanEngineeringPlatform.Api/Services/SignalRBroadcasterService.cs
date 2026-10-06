using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using CanEngineeringPlatform.Api.Hubs;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Replay;
using CanEngineeringPlatform.Core.Topology;

namespace CanEngineeringPlatform.Api.Services;

public class SignalRBroadcasterService : BackgroundService
{
    private readonly ICanPipeline _pipeline;
    private readonly ITopologyEngine _topologyEngine;
    private readonly ICanReplayService _replayService;
    private readonly IGatewayService _gatewayService;
    private readonly CanEngineeringPlatform.Core.Hardware.IHardwareManager _hardwareManager;
    private readonly IHubContext<CanHub, ICanHubClient> _hubContext;

    public SignalRBroadcasterService(
        ICanPipeline pipeline,
        ITopologyEngine topologyEngine,
        ICanReplayService replayService,
        IGatewayService gatewayService,
        CanEngineeringPlatform.Core.Hardware.IHardwareManager hardwareManager,
        IHubContext<CanHub, ICanHubClient> hubContext)
    {
        _pipeline = pipeline;
        _topologyEngine = topologyEngine;
        _replayService = replayService;
        _gatewayService = gatewayService;
        _hardwareManager = hardwareManager;
        _hubContext = hubContext;

        _pipeline.OnBatchProcessed += OnBatchProcessed;
        _hardwareManager.OnHardwareStatusChanged += OnHardwareStatusChanged;
    }

    private void OnHardwareStatusChanged(CanEngineeringPlatform.Core.Hardware.HardwareStatus status)
    {
        _ = _hubContext.Clients.All.ReceiveHardwareStatus(status);
    }

    private void OnBatchProcessed(CanFrameBatch batch)
    {
        _ = _hubContext.Clients.All.ReceiveFrameBatch(batch);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Periodic broadcast of topology, replay status, and gateway status (every 250ms)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var topology = _topologyEngine.GetSnapshot();
                await _hubContext.Clients.All.ReceiveTopologyUpdate(topology);

                var replayStatus = _replayService.GetStatus();
                if (replayStatus.IsLoaded)
                {
                    await _hubContext.Clients.All.ReceiveReplayStatus(replayStatus);
                }

                var gatewayStatus = _gatewayService.GetStatus();
                await _hubContext.Clients.All.ReceiveGatewayStatus(gatewayStatus);

                var hwStatus = _hardwareManager.GetStatus();
                if (hwStatus != null)
                {
                    await _hubContext.Clients.All.ReceiveHardwareStatus(hwStatus);
                }
            }
            catch
            {
                // Continue broadcast loop on client disconnects
            }

            await Task.Delay(250, stoppingToken);
        }
    }
}
