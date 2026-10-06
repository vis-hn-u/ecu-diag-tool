using System;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Diagnostics.Transport;

public class DiagnosticPayloadReceivedEventArgs : EventArgs
{
    public uint SourceAddress { get; init; }
    public byte[] Payload { get; init; } = Array.Empty<byte>();
    public ulong TimestampUs { get; init; }
    public AutomotiveProtocol Protocol { get; init; }
}

/// <summary>
/// Transport-agnostic diagnostic interface decoupling UDS, OBD-II, and J1939 from underlying hardware adapters.
/// Supports CAN ISO-TP, DoIP (ISO 13400), K-Line (ISO 9141-2 / ISO 14230), and FlexRay.
/// </summary>
public interface IDiagnosticTransport : IDisposable
{
    string TransportId { get; }
    AutomotiveProtocol Protocol { get; }
    bool IsConnected { get; }

    Task<bool> SendDiagnosticPayloadAsync(uint targetAddress, byte[] payload, CancellationToken cancellationToken = default);

    event Action<DiagnosticPayloadReceivedEventArgs>? OnPayloadReceived;
}
