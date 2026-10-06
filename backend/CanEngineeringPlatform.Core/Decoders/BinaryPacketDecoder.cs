using System;
using System.Buffers.Binary;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Decoders;

public static class BinaryPacketDecoder
{
    public const int PacketSize = 20;
    public const byte SyncByte0 = 0xAA;
    public const byte SyncByte1 = 0x55;

    public static bool TryDecode(ReadOnlySpan<byte> packet, out CanFrame frame)
    {
        frame = default;
        if (packet.Length < PacketSize)
        {
            return false;
        }

        if (packet[0] != SyncByte0 || packet[1] != SyncByte1)
        {
            return false;
        }

        uint timestampUs = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(2, 4));
        uint id = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(6, 4));
        byte dlc = packet[10];
        byte flags = packet[11];

        if (dlc > 8)
        {
            return false;
        }

        frame = new CanFrame(timestampUs, id, dlc, flags, packet.Slice(12, 8));
        return frame.IsValid();
    }

    public static byte[] Encode(in CanFrame frame)
    {
        byte[] buffer = new byte[PacketSize];
        Encode(frame, buffer.AsSpan());
        return buffer;
    }

    public static void Encode(in CanFrame frame, Span<byte> destination)
    {
        if (destination.Length < PacketSize)
        {
            throw new ArgumentException($"Destination span must be at least {PacketSize} bytes", nameof(destination));
        }

        destination.Clear();
        destination[0] = SyncByte0;
        destination[1] = SyncByte1;
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(2, 4), frame.TimestampUs);
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(6, 4), frame.Id);
        destination[10] = frame.Dlc;
        destination[11] = frame.Flags;

        if (frame.Data != null)
        {
            int copyLen = Math.Min((int)frame.Dlc, frame.Data.Length);
            frame.Data.AsSpan(0, copyLen).CopyTo(destination.Slice(12, 8));
        }
    }
}
