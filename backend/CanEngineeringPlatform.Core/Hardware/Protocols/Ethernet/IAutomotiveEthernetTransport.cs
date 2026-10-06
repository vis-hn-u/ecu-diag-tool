using System;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.Ethernet;

/// <summary>
/// Abstraction for Automotive Ethernet (100BASE-T1 / 1000BASE-T1) hardware interfaces.
/// </summary>
public interface IAutomotiveEthernetTransport : IAutomotiveTransport
{
    new AutomotiveEthernetCapabilities Capabilities { get; }
    IAutomotiveCapabilities IAutomotiveTransport.Capabilities => Capabilities;
    AutomotiveProtocol IAutomotiveTransport.Protocol => AutomotiveProtocol.AutomotiveEthernet;

    ValueTask<bool> SendFrameAsync(AutomotiveEthernetFrame frame, CancellationToken cancellationToken = default);

    event Action<AutomotiveEthernetFrame>? OnEthernetFrameReceived;
}
