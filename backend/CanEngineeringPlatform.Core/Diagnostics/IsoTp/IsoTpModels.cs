using System;

namespace CanEngineeringPlatform.Core.Diagnostics.IsoTp;

public enum IsoTpFrameType : byte
{
    SingleFrame = 0,
    FirstFrame = 1,
    ConsecutiveFrame = 2,
    FlowControl = 3
}

public enum FlowStatus : byte
{
    ContinueToSend = 0,
    Wait = 1,
    Overflow = 2
}

public enum IsoTpState
{
    Idle,
    ReceivingConsecutiveFrames,
    TransmittingConsecutiveFrames,
    WaitingForFlowControl,
    Completed,
    Error
}

public class IsoTpMessage
{
    public uint ChannelId { get; set; }
    public uint CanId { get; set; }
    public byte[] Payload { get; set; } = Array.Empty<byte>();
    public int Length => Payload.Length;
    public uint TimestampUs { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Direction { get; set; } = "RX"; // "RX" or "TX"
    public bool IsComplete { get; set; } = true;
    public string? ErrorMessage { get; set; }

    public IsoTpMessage() { }

    public IsoTpMessage(uint canId, byte[] payload, string direction = "RX", uint timestampUs = 0)
    {
        CanId = canId;
        Payload = payload ?? Array.Empty<byte>();
        Direction = direction;
        TimestampUs = timestampUs;
        TimestampUtc = DateTime.UtcNow;
        IsComplete = true;
    }
}

public class IsoTpConfig
{
    public byte BlockSize { get; set; } = 0; // 0 = send all without FC
    public byte SeparationTimeMin { get; set; } = 10; // STmin in ms (0-127 ms or 0xF1-0xF9 for 100-900us)
    public int TimeoutMs { get; set; } = 1000; // 1 second timeout
}
