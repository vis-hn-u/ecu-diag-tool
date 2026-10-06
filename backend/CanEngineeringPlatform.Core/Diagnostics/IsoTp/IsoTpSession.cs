using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Diagnostics.IsoTp;

public class IsoTpSession
{
    private readonly object _lock = new();
    private readonly IsoTpConfig _config;

    public uint RxCanId { get; }
    public uint TxCanId { get; }
    public IsoTpState State { get; private set; } = IsoTpState.Idle;

    private byte[]? _rxBuffer;
    private int _rxExpectedLength;
    private int _rxCurrentOffset;
    private byte _rxNextSequenceNumber;
    private uint _rxStartTimestampUs;
    private DateTime _rxLastFrameTime = DateTime.UtcNow;

    public event Action<IsoTpMessage>? OnMessageReceived;
    public event Action<CanFrame>? OnFlowControlRequired;
    public event Action<string>? OnTransportError;

    public IsoTpSession(uint rxCanId, uint txCanId, IsoTpConfig? config = null)
    {
        RxCanId = rxCanId;
        TxCanId = txCanId;
        _config = config ?? new IsoTpConfig();
    }

    public bool ProcessIncomingCanFrame(CanFrame frame, out IsoTpMessage? completedMessage)
    {
        completedMessage = null;
        if (frame.Dlc == 0 || frame.Data == null || frame.Data.Length == 0)
        {
            return false;
        }

        lock (_lock)
        {
            // Check timeout on ongoing reception
            if (State == IsoTpState.ReceivingConsecutiveFrames)
            {
                if ((DateTime.UtcNow - _rxLastFrameTime).TotalMilliseconds > _config.TimeoutMs)
                {
                    Reset();
                    OnTransportError?.Invoke($"ISO-TP timeout receiving consecutive frames on CAN ID 0x{RxCanId:X}");
                }
            }

            byte pciType = (byte)((frame.Data[0] >> 4) & 0x0F);

            switch ((IsoTpFrameType)pciType)
            {
                case IsoTpFrameType.SingleFrame:
                    return HandleSingleFrame(frame, out completedMessage);

                case IsoTpFrameType.FirstFrame:
                    return HandleFirstFrame(frame, out completedMessage);

                case IsoTpFrameType.ConsecutiveFrame:
                    return HandleConsecutiveFrame(frame, out completedMessage);

                case IsoTpFrameType.FlowControl:
                    HandleFlowControlFrame(frame);
                    return false;

                default:
                    OnTransportError?.Invoke($"Unknown ISO-TP PCI type {pciType} on CAN ID 0x{RxCanId:X}");
                    return false;
            }
        }
    }

    private bool HandleSingleFrame(CanFrame frame, out IsoTpMessage? completedMessage)
    {
        completedMessage = null;
        byte length = (byte)(frame.Data[0] & 0x0F);

        if (length == 0 || length > 7 || length > frame.Dlc - 1)
        {
            OnTransportError?.Invoke($"Invalid Single Frame length {length} (DLC {frame.Dlc}) on CAN ID 0x{RxCanId:X}");
            Reset();
            return false;
        }

        byte[] payload = new byte[length];
        Array.Copy(frame.Data, 1, payload, 0, length);

        Reset();
        completedMessage = new IsoTpMessage(RxCanId, payload, "RX", frame.TimestampUs);
        OnMessageReceived?.Invoke(completedMessage);
        return true;
    }

    private bool HandleFirstFrame(CanFrame frame, out IsoTpMessage? completedMessage)
    {
        completedMessage = null;
        if (frame.Dlc < 8)
        {
            OnTransportError?.Invoke($"First Frame DLC must be 8, got {frame.Dlc} on CAN ID 0x{RxCanId:X}");
            Reset();
            return false;
        }

        int length = ((frame.Data[0] & 0x0F) << 8) | frame.Data[1];
        if (length < 8)
        {
            OnTransportError?.Invoke($"First Frame total length must be >= 8, got {length} on CAN ID 0x{RxCanId:X}");
            Reset();
            return false;
        }

        _rxExpectedLength = length;
        _rxBuffer = new byte[length];
        _rxCurrentOffset = 0;
        _rxNextSequenceNumber = 1;
        _rxStartTimestampUs = frame.TimestampUs;
        _rxLastFrameTime = DateTime.UtcNow;

        // Copy initial 6 data bytes from First Frame (Data[2..7])
        int firstChunkLen = Math.Min(6, length);
        Array.Copy(frame.Data, 2, _rxBuffer, 0, firstChunkLen);
        _rxCurrentOffset = firstChunkLen;

        State = IsoTpState.ReceivingConsecutiveFrames;

        // Emit Flow Control frame (CTS) if TxCanId is defined
        if (TxCanId != 0)
        {
            byte[] fcData = new byte[8];
            fcData[0] = (byte)(0x30 | ((byte)FlowStatus.ContinueToSend & 0x0F)); // FC: CTS
            fcData[1] = _config.BlockSize;
            fcData[2] = _config.SeparationTimeMin;
            // Pad remainder with zeros
            var fcFrame = new CanFrame(frame.TimestampUs, TxCanId, 8, 0, fcData);
            OnFlowControlRequired?.Invoke(fcFrame);
        }

        return false;
    }

    private bool HandleConsecutiveFrame(CanFrame frame, out IsoTpMessage? completedMessage)
    {
        completedMessage = null;
        if (State != IsoTpState.ReceivingConsecutiveFrames || _rxBuffer == null)
        {
            OnTransportError?.Invoke($"Unexpected Consecutive Frame received in state {State} on CAN ID 0x{RxCanId:X}");
            return false;
        }

        byte sn = (byte)(frame.Data[0] & 0x0F);
        if (sn != _rxNextSequenceNumber)
        {
            OnTransportError?.Invoke($"Sequence number mismatch on 0x{RxCanId:X}: expected {_rxNextSequenceNumber}, got {sn}");
            Reset();
            return false;
        }

        _rxNextSequenceNumber = (byte)((_rxNextSequenceNumber + 1) & 0x0F);
        _rxLastFrameTime = DateTime.UtcNow;

        int remaining = _rxExpectedLength - _rxCurrentOffset;
        int chunkLen = Math.Min(Math.Min(7, (int)frame.Dlc - 1), remaining);

        if (chunkLen > 0)
        {
            Array.Copy(frame.Data, 1, _rxBuffer, _rxCurrentOffset, chunkLen);
            _rxCurrentOffset += chunkLen;
        }

        if (_rxCurrentOffset >= _rxExpectedLength)
        {
            byte[] completePayload = _rxBuffer;
            uint startTs = _rxStartTimestampUs;
            Reset();

            completedMessage = new IsoTpMessage(RxCanId, completePayload, "RX", startTs);
            OnMessageReceived?.Invoke(completedMessage);
            return true;
        }

        return false;
    }

    private void HandleFlowControlFrame(CanFrame frame)
    {
        byte flowStatus = (byte)(frame.Data[0] & 0x0F);
        byte blockSize = frame.Dlc > 1 ? frame.Data[1] : (byte)0;
        byte stMin = frame.Dlc > 2 ? frame.Data[2] : (byte)0;

        // Flow control processed for transmit state machine
    }

    public List<CanFrame> SegmentMessage(byte[] payload, uint txCanId, uint timestampUs = 0)
    {
        var frames = new List<CanFrame>();
        if (payload == null || payload.Length == 0)
        {
            return frames;
        }

        if (payload.Length <= 7)
        {
            // Single Frame
            byte[] data = new byte[8];
            data[0] = (byte)(0x00 | (payload.Length & 0x0F));
            Array.Copy(payload, 0, data, 1, payload.Length);
            frames.Add(new CanFrame(timestampUs, txCanId, 8, 0, data));
            return frames;
        }

        // Multi-frame: First Frame
        int totalLen = payload.Length;
        byte[] ffData = new byte[8];
        ffData[0] = (byte)(0x10 | ((totalLen >> 8) & 0x0F));
        ffData[1] = (byte)(totalLen & 0xFF);
        Array.Copy(payload, 0, ffData, 2, 6);
        frames.Add(new CanFrame(timestampUs, txCanId, 8, 0, ffData));

        // Consecutive Frames
        int offset = 6;
        byte seqNum = 1;
        while (offset < totalLen)
        {
            byte[] cfData = new byte[8];
            cfData[0] = (byte)(0x20 | (seqNum & 0x0F));
            int chunkLen = Math.Min(7, totalLen - offset);
            Array.Copy(payload, offset, cfData, 1, chunkLen);
            frames.Add(new CanFrame(timestampUs, txCanId, 8, 0, cfData));

            offset += chunkLen;
            seqNum = (byte)((seqNum + 1) & 0x0F);
        }

        return frames;
    }

    public void Reset()
    {
        lock (_lock)
        {
            State = IsoTpState.Idle;
            _rxBuffer = null;
            _rxExpectedLength = 0;
            _rxCurrentOffset = 0;
            _rxNextSequenceNumber = 0;
        }
    }
}
