using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Hardware.Automotive;
using CanEngineeringPlatform.Core.Hardware.Protocols.Kwp2000;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;

namespace CanEngineeringPlatform.Core.Diagnostics.Transport;

/// <summary>
/// Transport adapter implementing IDiagnosticTransport for Keyword Protocol 2000 (ISO 14230).
/// Supports KWP2000 over CAN (ISO 14230-4) and K-Line encapsulation.
/// Handles checksum calculation, framing, and diagnostic service payload dispatch.
/// </summary>
public class Kwp2000DiagnosticTransport : IDiagnosticTransport
{
    private readonly IHardwareManager? _hardwareManager;
    private readonly ICanTransport? _directCanTransport;
    private readonly ICanPipeline? _pipeline;

    private readonly object _lock = new();
    private bool _isConnected = true;
    private byte _testerAddress = 0xF1;
    private byte _targetEcuAddress = 0x11; // Standard Engine ECU in ISO 14230-4 (or 0x01)
    private uint _canRequestId = 0x7E0;
    private uint _canResponseId = 0x7E8;

    public string TransportId => "kwp2000-diagnostic-transport";
    public AutomotiveProtocol Protocol => AutomotiveProtocol.Kwp2000;
    public bool IsConnected => _isConnected;
    public byte TesterAddress => _testerAddress;
    public byte TargetEcuAddress => _targetEcuAddress;

    public event Action<DiagnosticPayloadReceivedEventArgs>? OnPayloadReceived;

    public Kwp2000DiagnosticTransport(IHardwareManager hardwareManager, ICanPipeline? pipeline = null)
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

    public Kwp2000DiagnosticTransport(ICanTransport canTransport)
    {
        _directCanTransport = canTransport ?? throw new ArgumentNullException(nameof(canTransport));
        _directCanTransport.OnFrameReceived += HandleCanFrame;
    }

    /// <summary>
    /// Sends a diagnostic payload formatted as an ISO 14230 KWP2000 message.
    /// </summary>
    public async Task<bool> SendDiagnosticPayloadAsync(uint targetAddress, byte[] payload, CancellationToken cancellationToken = default)
    {
        if (payload == null || payload.Length == 0) return false;

        byte ecuAddr = (byte)(targetAddress & 0xFF);
        if (ecuAddr == 0) ecuAddr = _targetEcuAddress;

        // Construct ISO 14230 Frame: [Fmt] [Target] [Source] [Length (if >63)] [SID] [Data] [CS]
        byte sid = payload[0];
        byte[] serviceData = payload.Length > 1 ? payload[1..] : Array.Empty<byte>();
        int dataLen = payload.Length;

        byte formatByte;
        byte? addLen = null;
        if (dataLen <= 63)
        {
            formatByte = (byte)(0x80 | dataLen);
        }
        else
        {
            formatByte = 0x80;
            addLen = (byte)dataLen;
        }

        using var ms = new MemoryStream();
        ms.WriteByte(formatByte);
        ms.WriteByte(ecuAddr);
        ms.WriteByte(_testerAddress);
        if (addLen.HasValue)
        {
            ms.WriteByte(addLen.Value);
        }
        ms.Write(payload, 0, payload.Length);

        // Checksum = sum of all preceding bytes modulo 256
        byte[] frameBytes = ms.ToArray();
        byte checksum = CalculateChecksum(frameBytes);
        ms.WriteByte(checksum);

        byte[] fullKwpFrame = ms.ToArray();

        // Over CAN: transmit via CAN frames (either single frame if <=7 bytes or segmented)
        uint canId = targetAddress > 0xFF ? targetAddress : _canRequestId;

        // If payload fits in single CAN frame or standard ISO 14230-4 CAN format:
        // Byte 0 = PCI (length), Byte 1 = SID, Byte 2..7 = Data
        if (payload.Length <= 7)
        {
            byte[] canData = new byte[8];
            canData[0] = (byte)payload.Length;
            Array.Copy(payload, 0, canData, 1, payload.Length);

            var frame = CanFrame.CreateClassic(canId, canData, channel: 1);
            return await SendRawFrameAsync(frame, cancellationToken);
        }
        else
        {
            // Segmented KWP frame
            int totalBytes = fullKwpFrame.Length;
            int offset = 0;
            while (offset < totalBytes)
            {
                int chunk = Math.Min(8, totalBytes - offset);
                byte[] chunkBytes = new byte[chunk];
                Array.Copy(fullKwpFrame, offset, chunkBytes, 0, chunk);
                offset += chunk;

                var frame = CanFrame.CreateClassic(canId, chunkBytes, channel: 1);
                if (!await SendRawFrameAsync(frame, cancellationToken))
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Processes incoming CAN frames containing KWP2000 responses.
    /// </summary>
    public void HandleCanFrame(CanFrame frame)
    {
        if (frame.Data == null || frame.Data.Length == 0) return;

        // Check if message is from target ECU response ID
        if (frame.Id != _canResponseId && frame.Id != 0x7E8 && frame.Id != 0x7E9)
        {
            return;
        }

        // Decode ISO 14230-4 single-frame CAN response:
        // Byte 0 = length (1..7)
        // Byte 1 = Response Service ID (e.g. 0xC1 StartComm, 0x61 ReadLocalId, 0x58 ReadDTC, 0x7F Negative)
        byte len = frame.Data[0];
        if (len > 0 && len < frame.Data.Length)
        {
            byte[] diagnosticPayload = new byte[len];
            Array.Copy(frame.Data, 1, diagnosticPayload, 0, len);

            OnPayloadReceived?.Invoke(new DiagnosticPayloadReceivedEventArgs
            {
                SourceAddress = frame.Id,
                Payload = diagnosticPayload,
                TimestampUs = (ulong)(DateTime.UtcNow.Ticks / 10),
                Protocol = AutomotiveProtocol.Kwp2000
            });
            return;
        }

        // Full ISO 14230 header response: [Format] [Target] [Source] [Payload...] [CS]
        if (frame.Data.Length >= 4 && (frame.Data[0] & 0xC0) == 0x80)
        {
            int payloadLen = frame.Data[0] & 0x3F;
            int headerOffset = 3;
            if (payloadLen == 0 && frame.Data.Length >= 5)
            {
                payloadLen = frame.Data[3];
                headerOffset = 4;
            }

            if (headerOffset + payloadLen <= frame.Data.Length)
            {
                byte[] diagnosticPayload = new byte[payloadLen];
                Array.Copy(frame.Data, headerOffset, diagnosticPayload, 0, payloadLen);

                OnPayloadReceived?.Invoke(new DiagnosticPayloadReceivedEventArgs
                {
                    SourceAddress = frame.Id,
                    Payload = diagnosticPayload,
                    TimestampUs = (ulong)(DateTime.UtcNow.Ticks / 10),
                    Protocol = AutomotiveProtocol.Kwp2000
                });
            }
        }
    }

    private static byte CalculateChecksum(byte[] data)
    {
        uint sum = 0;
        foreach (byte b in data)
        {
            sum += b;
        }
        return (byte)(sum & 0xFF);
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
    }
}
