using System;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware.Automotive;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Hardware;

public interface ICanTransport : IAutomotiveTransport
{
    TransportType Type { get; }
    new HardwareCapabilities Capabilities { get; }
    HardwareStatus GetStatus();

    ValueTask<bool> SendFrameAsync(CanFrame frame, CancellationToken cancellationToken = default);

    event Action<CanFrame>? OnFrameReceived;
    event Action<HardwareStatus>? OnStatusChanged;

    // Explicit default implementations bridging to universal IAutomotiveTransport
    AutomotiveProtocol IAutomotiveTransport.Protocol => AutomotiveProtocol.Can;
    TransportState IAutomotiveTransport.State => GetStatus().IsConnected ? TransportState.Connected : TransportState.Disconnected;
    IAutomotiveCapabilities IAutomotiveTransport.Capabilities => Capabilities;
    
    AutomotiveTransportStatus IAutomotiveTransport.GetAutomotiveStatus()
    {
        var s = GetStatus();
        return new AutomotiveTransportStatus
        {
            TransportId = s.TransportId,
            Name = s.Name,
            Protocol = AutomotiveProtocol.Can,
            State = s.IsConnected ? TransportState.Connected : TransportState.Disconnected,
            ConnectionDetails = s.ConnectionDetails,
            TotalPacketsReceived = s.TotalFramesReceived,
            TotalPacketsTransmitted = s.TotalFramesTransmitted,
            RxPacketRate = s.RxFrameRate,
            TxPacketRate = s.TxFrameRate,
            LastSeenUtc = s.LastSeenUtc,
            LastError = s.LastError,
            Capabilities = Capabilities,
            ExtendedTelemetry = s.ExtendedTelemetry
        };
    }

    event Action<AutomotiveTransportStatus>? IAutomotiveTransport.OnAutomotiveStatusChanged
    {
        add { }
        remove { }
    }
}
