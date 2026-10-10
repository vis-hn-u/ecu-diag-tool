using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Hardware;

public interface IHardwareManager : IDisposable
{
    IReadOnlyList<ICanTransport> GetAllTransports();
    ICanTransport? GetActiveTransport();
    ICanTransport? GetTransport(string transportId);
    string? ActiveTransportId { get; }
    HardwareStatus? GetStatus();
    Task<bool> SetActiveTransportAsync(string transportId, CancellationToken cancellationToken = default);
    Task<bool> SendFrameAsync(CanFrame frame, CancellationToken cancellationToken = default);
    Task<bool> ConnectTransportAsync(string transportId, CancellationToken cancellationToken = default);
    Task<bool> DisconnectTransportAsync(string transportId, CancellationToken cancellationToken = default);
    Task<bool> EnableDualHardwareStreamingAsync(string gvretPort = "COM8", string espPort = "COM14", uint canBaud = 500000u, bool listenOnly = false, CancellationToken cancellationToken = default);
    IReadOnlyList<ICanTransport> GetConnectedTransports();

    event Action<ICanTransport?>? OnActiveTransportChanged;
    event Action<CanFrame>? OnFrameReceived;
    event Action<HardwareStatus>? OnHardwareStatusChanged;
}
