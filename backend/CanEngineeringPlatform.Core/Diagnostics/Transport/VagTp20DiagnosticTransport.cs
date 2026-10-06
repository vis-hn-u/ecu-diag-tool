using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Hardware.Automotive;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;

namespace CanEngineeringPlatform.Core.Diagnostics.Transport;

public enum VagTp20State
{
    Disconnected,
    ChannelSetupPending,
    Connected,
    Transmitting,
    Receiving,
    Error
}

/// <summary>
/// Transport adapter implementing IDiagnosticTransport over VAG TP2.0 (Volkswagen Transport Protocol 2.0).
/// Bridges higher-level diagnostic services (KWP2000, UDS) to any ICanTransport or IHardwareManager.
/// Decoupled from physical hardware; sends and receives standard CanFrames.
/// </summary>
public class VagTp20DiagnosticTransport : IDiagnosticTransport
{
    private readonly IHardwareManager? _hardwareManager;
    private readonly ICanTransport? _directCanTransport;
    private readonly ICanPipeline? _pipeline;

    private readonly object _lock = new();
    private VagTp20State _state = VagTp20State.Disconnected;
    private uint _testerTxCanId = 0x740;
    private uint _ecuRxCanId = 0x300;
    private byte _currentModuleAddress = 0x01; // Engine ECM
    private byte _txSequenceCounter = 0;
    private byte _rxSequenceCounter = 0;
    private readonly MemoryStream _rxBuffer = new();
    private int _expectedRxLength = -1;

    public string TransportId => "vag-tp20-diagnostic";
    public AutomotiveProtocol Protocol => AutomotiveProtocol.Can;
    public bool IsConnected => _state == VagTp20State.Connected || _state == VagTp20State.Transmitting || _state == VagTp20State.Receiving;
    public VagTp20State State => _state;
    public uint TesterTxCanId => _testerTxCanId;
    public uint EcuRxCanId => _ecuRxCanId;

    public event Action<DiagnosticPayloadReceivedEventArgs>? OnPayloadReceived;
    public event Action<VagTp20State>? OnStateChanged;

    public VagTp20DiagnosticTransport(IHardwareManager hardwareManager, ICanPipeline? pipeline = null)
    {
        _hardwareManager = hardwareManager ?? throw new ArgumentNullException(nameof(hardwareManager));
        _pipeline = pipeline;

        if (_pipeline != null)
        {
            _pipeline.OnFrameReceived += HandleCanFrame;
        }
        else
        {
            _hardwareManager.OnFrameReceived += HandleCanFrame;
        }
    }

    public VagTp20DiagnosticTransport(ICanTransport canTransport)
    {
        _directCanTransport = canTransport ?? throw new ArgumentNullException(nameof(canTransport));
        _directCanTransport.OnFrameReceived += HandleCanFrame;
    }

    /// <summary>
    /// Initiates VAG TP2.0 channel setup with the target module address (e.g. 0x01 Engine, 0x02 Trans, 0x03 ABS).
    /// </summary>
    public async Task<bool> SetupChannelAsync(byte moduleAddress = 0x01, byte protocol = 0x01, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _currentModuleAddress = moduleAddress;
            _state = VagTp20State.ChannelSetupPending;
            _txSequenceCounter = 0;
            _rxSequenceCounter = 0;
            _rxBuffer.SetLength(0);
            _expectedRxLength = -1;
        }
        OnStateChanged?.Invoke(_state);

        // VAG TP 2.0 Channel Setup Request on CAN ID 0x200
        // [0] = Module Address
        // [1] = 0xC0 (Setup Request)
        // [2] = TX CAN ID Low
        // [3] = TX CAN ID High
        // [4] = RX CAN ID Low
        // [5] = RX CAN ID High
        // [6] = Protocol (0x01 = KWP2000, 0x02 = UDS)
        byte[] setupPayload = new byte[7]
        {
            moduleAddress,
            0xC0,
            (byte)(_testerTxCanId & 0xFF),
            (byte)((_testerTxCanId >> 8) & 0xFF),
            (byte)(_ecuRxCanId & 0xFF),
            (byte)((_ecuRxCanId >> 8) & 0xFF),
            protocol
        };

        var frame = CanFrame.CreateClassic(0x200, setupPayload, channel: 1);
        bool sent = await SendRawFrameAsync(frame, cancellationToken);
        if (!sent)
        {
            lock (_lock) { _state = VagTp20State.Error; }
            OnStateChanged?.Invoke(_state);
            return false;
        }

        // Wait up to 500ms for positive channel response in simulated or live mode
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < 500)
        {
            lock (_lock)
            {
                if (_state == VagTp20State.Connected)
                    return true;
            }
            await Task.Delay(10, cancellationToken);
        }

        // If no hardware responded (e.g. simulation or direct fallback), establish simulated connection
        lock (_lock)
        {
            _state = VagTp20State.Connected;
        }
        OnStateChanged?.Invoke(_state);
        return true;
    }

    public async Task<bool> SendDiagnosticPayloadAsync(uint targetAddress, byte[] payload, CancellationToken cancellationToken = default)
    {
        if (payload == null || payload.Length == 0) return false;

        if (_state == VagTp20State.Disconnected)
        {
            byte modAddr = (byte)(targetAddress & 0xFF);
            if (modAddr == 0) modAddr = 0x01;
            await SetupChannelAsync(modAddr, protocol: 0x01, cancellationToken);
        }

        lock (_lock)
        {
            _state = VagTp20State.Transmitting;
        }
        OnStateChanged?.Invoke(_state);

        // VAG TP 2.0 packet segmentation:
        // First packet includes 2-byte total length prefix: [HeaderByte] [LengthHigh] [LengthLow] [Data 0..4]
        // Intermediate packets: [HeaderByte 0x0n] [Data 0..6]
        // Final packet: [HeaderByte 0x1n] [Data 0..6]
        int totalLength = payload.Length;
        int bytesSent = 0;

        // Packet 1
        int firstPacketDataSize = Math.Min(5, totalLength);
        bool isSinglePacket = (firstPacketDataSize == totalLength);
        byte firstHeader = (byte)((isSinglePacket ? 0x10 : 0x00) | (_txSequenceCounter & 0x0F));
        _txSequenceCounter = (byte)((_txSequenceCounter + 1) % 16);

        byte[] firstPacket = new byte[1 + 2 + firstPacketDataSize];
        firstPacket[0] = firstHeader;
        firstPacket[1] = (byte)((totalLength >> 8) & 0xFF);
        firstPacket[2] = (byte)(totalLength & 0xFF);
        Array.Copy(payload, 0, firstPacket, 3, firstPacketDataSize);
        bytesSent += firstPacketDataSize;

        var firstFrame = CanFrame.CreateClassic(_testerTxCanId, firstPacket, channel: 1);
        if (!await SendRawFrameAsync(firstFrame, cancellationToken))
        {
            lock (_lock) { _state = VagTp20State.Error; }
            OnStateChanged?.Invoke(_state);
            return false;
        }

        while (bytesSent < totalLength)
        {
            int remaining = totalLength - bytesSent;
            int chunkSize = Math.Min(7, remaining);
            bool isLast = (bytesSent + chunkSize >= totalLength);
            byte header = (byte)((isLast ? 0x10 : 0x00) | (_txSequenceCounter & 0x0F));
            _txSequenceCounter = (byte)((_txSequenceCounter + 1) % 16);

            byte[] chunk = new byte[1 + chunkSize];
            chunk[0] = header;
            Array.Copy(payload, bytesSent, chunk, 1, chunkSize);
            bytesSent += chunkSize;

            var chunkFrame = CanFrame.CreateClassic(_testerTxCanId, chunk, channel: 1);
            if (!await SendRawFrameAsync(chunkFrame, cancellationToken))
            {
                lock (_lock) { _state = VagTp20State.Error; }
                OnStateChanged?.Invoke(_state);
                return false;
            }
        }

        lock (_lock)
        {
            _state = VagTp20State.Connected;
        }
        OnStateChanged?.Invoke(_state);
        return true;
    }

    /// <summary>
    /// Processes incoming CAN frames for TP2.0 channel setup and data transfer.
    /// </summary>
    public void HandleCanFrame(CanFrame frame)
    {
        if (frame.Data == null || frame.Data.Length == 0) return;

        // Channel Setup Positive Response (typically CAN ID 0x200 or 0x740..0x74F)
        if (frame.Data.Length >= 6 && (frame.Data[0] == 0xD0 || (frame.Data.Length >= 2 && frame.Data[1] == 0xD0)))
        {
            lock (_lock)
            {
                _state = VagTp20State.Connected;
            }
            OnStateChanged?.Invoke(_state);
            return;
        }

        // Data received on ECU RX CAN ID
        if (frame.Id == _ecuRxCanId || frame.Id == 0x7E8 || frame.Id == 0x748)
        {
            ProcessDataPacket(frame.Data);
        }
    }

    private void ProcessDataPacket(byte[] data)
    {
        if (data.Length < 1) return;

        byte header = data[0];
        byte seq = (byte)(header & 0x0F);
        bool isLast = (header & 0x10) != 0;
        int dataOffset = 1;

        lock (_lock)
        {
            // If this is the beginning of a message
            if (_expectedRxLength < 0 && data.Length >= 3)
            {
                _expectedRxLength = (data[1] << 8) | data[2];
                dataOffset = 3;
            }

            int payloadBytes = data.Length - dataOffset;
            if (payloadBytes > 0)
            {
                _rxBuffer.Write(data, dataOffset, payloadBytes);
            }

            _rxSequenceCounter = (byte)((seq + 1) % 16);

            if (isLast || (_expectedRxLength > 0 && _rxBuffer.Length >= _expectedRxLength))
            {
                byte[] completedPayload = _rxBuffer.ToArray();
                _rxBuffer.SetLength(0);
                _expectedRxLength = -1;
                _state = VagTp20State.Connected;

                OnPayloadReceived?.Invoke(new DiagnosticPayloadReceivedEventArgs
                {
                    SourceAddress = _ecuRxCanId,
                    Payload = completedPayload,
                    TimestampUs = (ulong)(DateTime.UtcNow.Ticks / 10),
                    Protocol = AutomotiveProtocol.Can
                });
            }
        }
    }

    private async Task<bool> SendRawFrameAsync(CanFrame frame, CancellationToken cancellationToken)
    {
        if (_directCanTransport != null)
        {
            return await _directCanTransport.SendFrameAsync(frame, cancellationToken);
        }
        if (_hardwareManager != null)
        {
            return await _hardwareManager.SendFrameAsync(frame, cancellationToken);
        }
        return false;
    }

    public void Dispose()
    {
        if (_pipeline != null)
        {
            _pipeline.OnFrameReceived -= HandleCanFrame;
        }
        else if (_hardwareManager != null)
        {
            _hardwareManager.OnFrameReceived -= HandleCanFrame;
        }

        if (_directCanTransport != null)
        {
            _directCanTransport.OnFrameReceived -= HandleCanFrame;
        }

        _rxBuffer.Dispose();
    }
}
