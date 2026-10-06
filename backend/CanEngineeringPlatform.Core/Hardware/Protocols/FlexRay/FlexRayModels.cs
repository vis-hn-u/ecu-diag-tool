using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.FlexRay;

public enum FlexRayChannel : byte
{
    ChannelA = 0x01,
    ChannelB = 0x02,
    Both = 0x03
}

public class FlexRayFrame
{
    public FlexRayChannel Channel { get; init; } = FlexRayChannel.ChannelA;
    public ushort FrameId { get; init; } // 1 - 2047
    public byte PayloadLengthWords { get; init; } // 0 - 127 (words = 2 bytes)
    public int PayloadLengthBytes => PayloadLengthWords * 2;
    public ushort HeaderCrc { get; init; }
    public byte CycleCount { get; init; } // 0 - 63
    public bool PayloadPreambleIndicator { get; init; }
    public bool NullFrameIndicator { get; init; } = true;
    public bool SyncFrameIndicator { get; init; }
    public bool StartupFrameIndicator { get; init; }
    public byte[] Payload { get; init; } = Array.Empty<byte>();
    public ulong TimestampUs { get; init; }
    public string? SourceId { get; init; }

    public FlexRayFrame() { }

    public FlexRayFrame(
        ushort frameId,
        byte[] payload,
        byte cycleCount = 0,
        FlexRayChannel channel = FlexRayChannel.ChannelA,
        bool isStartup = false,
        bool isSync = false,
        bool isPreamble = false,
        ulong timestampUs = 0,
        string? sourceId = null)
    {
        FrameId = frameId;
        Payload = payload ?? Array.Empty<byte>();
        PayloadLengthWords = (byte)Math.Min(127, (Payload.Length + 1) / 2);
        CycleCount = (byte)(cycleCount & 0x3F);
        Channel = channel;
        StartupFrameIndicator = isStartup;
        SyncFrameIndicator = isSync;
        PayloadPreambleIndicator = isPreamble;
        NullFrameIndicator = (Payload.Length == 0);
        TimestampUs = timestampUs;
        SourceId = sourceId;
        HeaderCrc = CalculateHeaderCrc(StartupFrameIndicator, SyncFrameIndicator, FrameId, PayloadLengthWords);
    }

    public static ushort CalculateHeaderCrc(bool startup, bool sync, ushort frameId, byte lengthWords)
    {
        // 11-bit header CRC polynomial 0x385 (x^11 + x^9 + x^8 + x^7 + x^2 + 1)
        uint v = (uint)(((startup ? 1 : 0) << 19) |
                        ((sync ? 1 : 0) << 18) |
                        ((frameId & 0x7FF) << 7) |
                        (lengthWords & 0x7F));

        ushort crc = 0x01A;
        for (int i = 19; i >= 0; i--)
        {
            bool bit = ((v >> i) & 1) == 1;
            bool c10 = ((crc >> 10) & 1) == 1;
            crc = (ushort)((crc << 1) & 0x7FF);
            if (bit ^ c10) crc ^= 0x385;
        }
        return (ushort)(crc & 0x7FF);
    }
}

public class FlexRayCapabilities : IAutomotiveCapabilities
{
    public AutomotiveProtocol Protocol => AutomotiveProtocol.FlexRay;
    public bool SupportsTransmit { get; init; } = true;
    public bool SupportsHardwareTimestamps { get; init; } = true;
    public int ChannelCount { get; init; } = 2; // Dual channel A + B

    public bool SupportsDualChannel { get; init; } = true;
    public uint BitRateMbps { get; init; } = 10;
    public ushort MaxFrameId { get; init; } = 2047;
    public byte MaxCycleCount { get; init; } = 64;
    public ushort StaticSlotCount { get; init; } = 60;
    public ushort DynamicSlotCount { get; init; } = 40;
}
