using System;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Hardware.Transports;

public class SimulationTransport : ICanTransport
{
    public string TransportId => "virtual-simulation";
    public string Name => "Virtual CAN / CAN FD Simulation Transport";
    public TransportType Type => TransportType.Simulation;

    public HardwareCapabilities Capabilities { get; } = new()
    {
        SupportsCanFd = true,
        SupportsBitRateSwitch = true,
        SupportsClassicCan = true,
        ChannelCount = 2,
        MaxPayloadBytes = 64,
        SupportsTransmit = true,
        SupportsHardwareTimestamps = true,
        SupportedNominalBaudRates = new uint[] { 125000, 250000, 500000, 1000000 },
        SupportedDataBaudRates = new uint[] { 1000000, 2000000, 4000000, 8000000 }
    };

    private readonly object _lock = new();
    private bool _isConnected;
    private long _totalFramesReceived;
    private long _totalFramesTransmitted;
    private DateTime? _lastSeenUtc;
    private string? _lastError;

    public event Action<CanFrame>? OnFrameReceived;
    public event Action<HardwareStatus>? OnStatusChanged;

    public HardwareStatus GetStatus()
    {
        lock (_lock)
        {
            return new HardwareStatus
            {
                TransportId = TransportId,
                Name = Name,
                Type = Type,
                IsConnected = _isConnected,
                ConnectionDetails = "In-Process Virtual Loopback Bus (CAN1 & CAN2)",
                TotalFramesReceived = _totalFramesReceived,
                TotalFramesTransmitted = _totalFramesTransmitted,
                RxFrameRate = 0,
                TxFrameRate = 0,
                LastSeenUtc = _lastSeenUtc,
                LastError = _lastError,
                Capabilities = Capabilities,
                ExtendedTelemetry = new
                {
                    Mode = "Simulation Loopback",
                    CanFdEnabled = true
                }
            };
        }
    }

    public Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _isConnected = true;
            _lastError = null;
        }
        OnStatusChanged?.Invoke(GetStatus());
        return Task.FromResult(true);
    }

    public Task DisconnectAsync()
    {
        lock (_lock)
        {
            _isConnected = false;
        }
        OnStatusChanged?.Invoke(GetStatus());
        return Task.CompletedTask;
    }

    public ValueTask<bool> SendFrameAsync(CanFrame frame, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_isConnected) return ValueTask.FromResult(false);

            _totalFramesTransmitted++;
            _lastSeenUtc = DateTime.UtcNow;
        }

        // Loopback transmitted frame with TX flag so consumers observe outgoing frame
        var txObserved = frame;
        txObserved.Flags |= CanFrame.FlagTx;
        if (string.IsNullOrEmpty(txObserved.SourceId))
        {
            txObserved.SourceId = "Simulation-Tx";
        }
        OnFrameReceived?.Invoke(txObserved);

        return ValueTask.FromResult(true);
    }

    /// <summary>
    /// Injects a frame from simulation generators (e.g. Engine Simulator, Virtual ECUs).
    /// </summary>
    public void InjectSimulatedFrame(CanFrame frame)
    {
        lock (_lock)
        {
            if (!_isConnected) return;
            _totalFramesReceived++;
            _lastSeenUtc = DateTime.UtcNow;
        }

        if (string.IsNullOrEmpty(frame.SourceId))
        {
            frame.SourceId = "Simulation";
        }

        OnFrameReceived?.Invoke(frame);
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
    }
}
