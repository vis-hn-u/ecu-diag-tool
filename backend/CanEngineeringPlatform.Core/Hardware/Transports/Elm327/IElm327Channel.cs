using System;
using System.Threading;
using System.Threading.Tasks;

namespace CanEngineeringPlatform.Core.Hardware.Transports.Elm327;

/// <summary>
/// Abstraction for the underlying bidirectional communication stream to an ELM327 device
/// (Bluetooth RFCOMM virtual COM port, Bluetooth LE characteristic, or in-memory simulation).
/// </summary>
public interface IElm327CommunicationChannel : IDisposable
{
    string ChannelName { get; }
    bool IsOpen { get; }

    Task<bool> OpenAsync(CancellationToken cancellationToken = default);
    Task CloseAsync();
    Task SendLineAsync(string command, CancellationToken cancellationToken = default);
    Task<string> ReadResponseAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
    void Drain();
}
