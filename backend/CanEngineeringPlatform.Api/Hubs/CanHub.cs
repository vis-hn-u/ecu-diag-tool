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

    public CanHub(
        ITopologyEngine topologyEngine,
        ICanReplayService replayService,
        IDiagnosticSessionService diagnosticService,
        CanEngineeringPlatform.Core.Hardware.IHardwareManager hardwareManager)
    {
        _topologyEngine = topologyEngine;
        _replayService = replayService;
        _diagnosticService = diagnosticService;
        _hardwareManager = hardwareManager;
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
    }
}
