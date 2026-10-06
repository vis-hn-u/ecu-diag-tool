using System;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.Oem;

/// <summary>
/// Abstraction for proprietary or OEM-specific vehicle network adapters and protocols.
/// </summary>
public interface IOemTransport : IAutomotiveTransport
{
    new OemCapabilities Capabilities { get; }
    IAutomotiveCapabilities IAutomotiveTransport.Capabilities => Capabilities;
    AutomotiveProtocol IAutomotiveTransport.Protocol => AutomotiveProtocol.OemSpecific;

    ValueTask<bool> SendMessageAsync(OemMessage message, CancellationToken cancellationToken = default);

    event Action<OemMessage>? OnOemMessageReceived;
}
