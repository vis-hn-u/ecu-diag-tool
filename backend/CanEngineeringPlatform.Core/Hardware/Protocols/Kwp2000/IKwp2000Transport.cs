using System;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.Kwp2000;

/// <summary>
/// Abstraction for Keyword Protocol 2000 (ISO 14230) communication adapters.
/// </summary>
public interface IKwp2000Transport : IAutomotiveTransport
{
    new Kwp2000Capabilities Capabilities { get; }
    IAutomotiveCapabilities IAutomotiveTransport.Capabilities => Capabilities;
    AutomotiveProtocol IAutomotiveTransport.Protocol => AutomotiveProtocol.Kwp2000;

    ValueTask<bool> SendMessageAsync(Kwp2000Message message, CancellationToken cancellationToken = default);

    event Action<Kwp2000Message>? OnKwpMessageReceived;
}
