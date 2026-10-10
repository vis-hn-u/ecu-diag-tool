using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.Obd;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Replay;
using CanEngineeringPlatform.Core.Topology;

namespace CanEngineeringPlatform.Api.Hubs;

public interface ICanHubClient
{
    Task ReceiveFrameBatch(CanFrameBatch batch);
    Task ReceiveTopologyUpdate(NetworkTopologyState topology);
    Task ReceiveReplayStatus(ReplayStatus status);
    Task ReceiveGatewayStatus(object status);
    Task ReceiveHardwareStatus(CanEngineeringPlatform.Core.Hardware.HardwareStatus status);
    Task ReceiveDiagnosticExchange(DiagnosticExchange exchange);
    Task ReceiveDtcUpdate(System.Collections.Generic.IReadOnlyList<DiagnosticTroubleCode> dtcs);
    Task ReceiveObdValues(System.Collections.Generic.IReadOnlyList<ObdPidValue> values);
}

public class CanHub : Hub<ICanHubClient>
{
    private readonly ITopologyEngine _topologyEngine;
    private readonly ICanReplayService _replayService;
    private readonly IDiagnosticSessionService _diagnosticService;
    private readonly CanEngineeringPlatform.Core.Hardware.IHardwareManager _hardwareManager;
    private readonly ICanPipeline _pipeline;

    public CanHub(
        ITopologyEngine topologyEngine,
        ICanReplayService replayService,
        IDiagnosticSessionService diagnosticService,
        CanEngineeringPlatform.Core.Hardware.IHardwareManager hardwareManager,
        ICanPipeline pipeline)
    {
        _topologyEngine = topologyEngine;
        _replayService = replayService;
        _diagnosticService = diagnosticService;
        _hardwareManager = hardwareManager;
        _pipeline = pipeline;
    }

    public async Task RequestInitialState()
    {
        await Clients.Caller.ReceiveTopologyUpdate(_topologyEngine.GetSnapshot());
        await Clients.Caller.ReceiveReplayStatus(_replayService.GetStatus());
        await Clients.Caller.ReceiveDtcUpdate(_diagnosticService.GetActiveDtcList());
        await Clients.Caller.ReceiveObdValues(_diagnosticService.GetLatestObdValues());
        var hwStatus = _hardwareManager.GetStatus();
        if (hwStatus != null)
        {
            await Clients.Caller.ReceiveHardwareStatus(hwStatus);
        }

        var recent = _pipeline.GetRecentFrames(200);
        if (recent.Count > 0)
        {
            await Clients.Caller.ReceiveFrameBatch(new CanFrameBatch
            {
                Frames = System.Linq.Enumerable.ToList(recent),
                FrameRate = _pipeline.CurrentFps
            });
        }
    }
}
