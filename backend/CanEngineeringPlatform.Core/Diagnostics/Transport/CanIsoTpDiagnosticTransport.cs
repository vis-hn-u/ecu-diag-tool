using System;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Hardware.Automotive;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;

namespace CanEngineeringPlatform.Core.Diagnostics.Transport;

/// <summary>
/// Adapter implementing IDiagnosticTransport over Classic CAN / CAN FD using ISO 15765-2 (ISO-TP).
/// Bridges higher-level diagnostic services (UDS, OBD-II) to any ICanTransport or IHardwareManager.
/// </summary>
public class CanIsoTpDiagnosticTransport : IDiagnosticTransport
{
    private readonly IIsoTpRouter _isoTpRouter;
    private readonly IHardwareManager? _hardwareManager;
    private readonly ICanTransport? _directCanTransport;
    private readonly ICanPipeline? _pipeline;

    public string TransportId => _directCanTransport?.TransportId ?? _hardwareManager?.ActiveTransportId ?? "can-isotp-diagnostic";
    public AutomotiveProtocol Protocol => AutomotiveProtocol.Can;
    public bool IsConnected => _directCanTransport?.GetStatus().IsConnected ?? _hardwareManager?.GetStatus()?.IsConnected ?? true;

    public event Action<DiagnosticPayloadReceivedEventArgs>? OnPayloadReceived;

    public CanIsoTpDiagnosticTransport(IIsoTpRouter isoTpRouter, IHardwareManager hardwareManager, ICanPipeline? pipeline = null)
    {
        _isoTpRouter = isoTpRouter ?? throw new ArgumentNullException(nameof(isoTpRouter));
        _hardwareManager = hardwareManager ?? throw new ArgumentNullException(nameof(hardwareManager));
        _pipeline = pipeline;

        if (_pipeline != null)
        {
            _pipeline.OnFrameReceived += HandleFrame;
        }
        else
        {
            _hardwareManager.OnFrameReceived += HandleFrame;
        }
    }

    public CanIsoTpDiagnosticTransport(IIsoTpRouter isoTpRouter, ICanTransport canTransport)
    {
        _isoTpRouter = isoTpRouter ?? throw new ArgumentNullException(nameof(isoTpRouter));
        _directCanTransport = canTransport ?? throw new ArgumentNullException(nameof(canTransport));
        _directCanTransport.OnFrameReceived += HandleFrame;
    }

    public async Task<bool> SendDiagnosticPayloadAsync(uint targetAddress, byte[] payload, CancellationToken cancellationToken = default)
    {
        var frames = _isoTpRouter.SegmentMessage(payload ?? Array.Empty<byte>(), targetAddress);
        bool allSent = true;

        foreach (var frame in frames)
        {
            if (_directCanTransport != null)
            {
                bool ok = await _directCanTransport.SendFrameAsync(frame, cancellationToken);
                if (!ok) allSent = false;
            }
            else if (_hardwareManager != null)
            {
                bool ok = await _hardwareManager.SendFrameAsync(frame, cancellationToken);
                if (!ok) allSent = false;
            }
            else
            {
                allSent = false;
            }
        }

        return allSent;
    }

    private void HandleFrame(CanFrame frame)
    {
        if (_isoTpRouter.RouteCanFrame(frame, out var message) && message != null && message.Payload != null)
        {
            OnPayloadReceived?.Invoke(new DiagnosticPayloadReceivedEventArgs
            {
                SourceAddress = message.CanId,
                Payload = message.Payload,
                TimestampUs = message.TimestampUs,
                Protocol = AutomotiveProtocol.Can
            });
        }
    }

    public void Dispose()
    {
        if (_pipeline != null)
        {
            _pipeline.OnFrameReceived -= HandleFrame;
        }
        else if (_hardwareManager != null)
        {
            _hardwareManager.OnFrameReceived -= HandleFrame;
        }

        if (_directCanTransport != null)
        {
            _directCanTransport.OnFrameReceived -= HandleFrame;
        }
    }
}
