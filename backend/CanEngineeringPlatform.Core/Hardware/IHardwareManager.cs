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

    event Action<ICanTransport?>? OnActiveTransportChanged;
    event Action<CanFrame>? OnFrameReceived;
    event Action<HardwareStatus>? OnHardwareStatusChanged;
}
