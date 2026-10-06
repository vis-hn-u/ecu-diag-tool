using System;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.Lin;

/// <summary>
/// Abstraction for physical or virtual LIN (Local Interconnect Network / ISO 17987) adapters.
/// </summary>
public interface ILinTransport : IAutomotiveTransport
{
    new LinCapabilities Capabilities { get; }
    IAutomotiveCapabilities IAutomotiveTransport.Capabilities => Capabilities;
    AutomotiveProtocol IAutomotiveTransport.Protocol => AutomotiveProtocol.Lin;

    ValueTask<bool> SendFrameAsync(LinFrame frame, CancellationToken cancellationToken = default);

    event Action<LinFrame>? OnLinFrameReceived;
}
