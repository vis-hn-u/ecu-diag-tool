using System;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.J1939;

/// <summary>
/// Abstraction for heavy-duty commercial vehicle SAE J1939 / J1939-22 adapters.
/// </summary>
public interface IJ1939Transport : IAutomotiveTransport
{
    new J1939Capabilities Capabilities { get; }
    IAutomotiveCapabilities IAutomotiveTransport.Capabilities => Capabilities;
    AutomotiveProtocol IAutomotiveTransport.Protocol => AutomotiveProtocol.J1939;

    ValueTask<bool> SendMessageAsync(J1939Message message, CancellationToken cancellationToken = default);

    event Action<J1939Message>? OnJ1939MessageReceived;
}
