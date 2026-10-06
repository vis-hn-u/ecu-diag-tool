using System;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.FlexRay;

/// <summary>
/// Abstraction for ISO 17458 FlexRay high-speed automotive bus adapters.
/// </summary>
public interface IFlexRayTransport : IAutomotiveTransport
{
    new FlexRayCapabilities Capabilities { get; }
    IAutomotiveCapabilities IAutomotiveTransport.Capabilities => Capabilities;
    AutomotiveProtocol IAutomotiveTransport.Protocol => AutomotiveProtocol.FlexRay;

    ValueTask<bool> SendFrameAsync(FlexRayFrame frame, CancellationToken cancellationToken = default);

    event Action<FlexRayFrame>? OnFlexRayFrameReceived;
}
