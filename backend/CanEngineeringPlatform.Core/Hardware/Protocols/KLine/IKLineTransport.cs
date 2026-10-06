using System;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.KLine;

/// <summary>
/// Abstraction for ISO 9141-2 / K-Line physical hardware adapters.
/// </summary>
public interface IKLineTransport : IAutomotiveTransport
{
    new KLineCapabilities Capabilities { get; }
    IAutomotiveCapabilities IAutomotiveTransport.Capabilities => Capabilities;
    AutomotiveProtocol IAutomotiveTransport.Protocol => AutomotiveProtocol.KLine;

    Task<bool> InitializeBusAsync(KLineInitType initType, byte targetAddress, CancellationToken cancellationToken = default);
    ValueTask<bool> SendMessageAsync(KLineMessage message, CancellationToken cancellationToken = default);

    event Action<KLineMessage>? OnKLineMessageReceived;
}
