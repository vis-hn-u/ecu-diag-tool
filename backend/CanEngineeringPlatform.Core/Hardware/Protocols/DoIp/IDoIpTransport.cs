using System;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.DoIp;

/// <summary>
/// Abstraction for ISO 13400 Diagnostics over IP (DoIP) network transport.
/// </summary>
public interface IDoIpTransport : IAutomotiveTransport
{
    new DoIpCapabilities Capabilities { get; }
    IAutomotiveCapabilities IAutomotiveTransport.Capabilities => Capabilities;
    AutomotiveProtocol IAutomotiveTransport.Protocol => AutomotiveProtocol.DoIp;

    Task<bool> ActivateRoutingAsync(ushort sourceLogicalAddress, CancellationToken cancellationToken = default);
    ValueTask<bool> SendDiagnosticMessageAsync(ushort targetLogicalAddress, byte[] udsPayload, CancellationToken cancellationToken = default);
    ValueTask<bool> SendMessageAsync(DoIpMessage message, CancellationToken cancellationToken = default);

    event Action<DoIpMessage>? OnDoIpMessageReceived;
}
