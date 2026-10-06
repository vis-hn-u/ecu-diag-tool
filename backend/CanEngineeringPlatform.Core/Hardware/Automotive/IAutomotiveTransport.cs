using System;
using System.Threading;
using System.Threading.Tasks;

namespace CanEngineeringPlatform.Core.Hardware.Automotive;

/// <summary>
/// Universal interface representing any automotive physical bus or protocol transport.
/// </summary>
public interface IAutomotiveTransport : IDisposable
{
    string TransportId { get; }
    string Name { get; }
    AutomotiveProtocol Protocol { get; }
    TransportState State { get; }
    IAutomotiveCapabilities Capabilities { get; }
    AutomotiveTransportStatus GetAutomotiveStatus();

    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();

    event Action<AutomotiveTransportStatus>? OnAutomotiveStatusChanged;
}
