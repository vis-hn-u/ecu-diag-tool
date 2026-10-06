using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.J1939;

public enum J1939ProtocolType
{
    ClassicJ1939,     // Standard J1939 over Classic CAN (8-byte frames, TP up to 1785 bytes)
    J1939_22_CanFd    // SAE J1939-22 over CAN FD (up to 64 bytes per frame)
}

public class J1939Message
{
    public uint Pgn { get; init; }
    public byte Priority { get; init; } = 6;
    public byte DataPage { get; init; }
    public byte PduFormat { get; init; }
    public byte PduSpecific { get; init; }
    public byte SourceAddress { get; init; }
    public byte DestinationAddress { get; init; } = 0xFF; // 0xFF = Global / Broadcast
    public bool IsBroadcast => DestinationAddress == 0xFF || PduFormat >= 240;
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public J1939ProtocolType ProtocolType { get; init; } = J1939ProtocolType.ClassicJ1939;
    public byte Channel { get; init; }
    public ulong TimestampUs { get; init; }
    public string? SourceId { get; init; }

    public J1939Message() { }

    public J1939Message(
        uint pgn,
        byte[] data,
        byte sourceAddress,
        byte destinationAddress = 0xFF,
        byte priority = 6,
        J1939ProtocolType protocolType = J1939ProtocolType.ClassicJ1939,
        byte channel = 0,
        ulong timestampUs = 0,
        string? sourceId = null)
    {
        Pgn = pgn;
        Data = data ?? Array.Empty<byte>();
        SourceAddress = sourceAddress;
        DestinationAddress = destinationAddress;
        Priority = (byte)(priority & 0x07);
        ProtocolType = protocolType;
        Channel = channel;
        TimestampUs = timestampUs;
        SourceId = sourceId;

        DataPage = (byte)((pgn >> 16) & 0x01);
        PduFormat = (byte)((pgn >> 8) & 0xFF);
        PduSpecific = (PduFormat < 240) ? destinationAddress : (byte)(pgn & 0xFF);
    }

    /// <summary>
    /// Converts a 29-bit CAN identifier into J1939 metadata and PGN.
    /// </summary>
    public static J1939Message FromCanId(uint canId, byte[] data, byte channel = 0, ulong timestampUs = 0, string? sourceId = null)
    {
        byte priority = (byte)((canId >> 26) & 0x07);
        byte dp = (byte)((canId >> 24) & 0x01);
        byte pf = (byte)((canId >> 16) & 0xFF);
        byte ps = (byte)((canId >> 8) & 0xFF);
        byte sa = (byte)(canId & 0xFF);

        uint pgn;
        byte da;
        if (pf < 240)
        {
            // PDU1: PS is Destination Address
            da = ps;
            pgn = (uint)((dp << 16) | (pf << 8));
        }
        else
        {
            // PDU2: Broadcast, PS is Group Extension
            da = 0xFF;
            pgn = (uint)((dp << 16) | (pf << 8) | ps);
        }

        return new J1939Message
        {
            Pgn = pgn,
            Priority = priority,
            DataPage = dp,
            PduFormat = pf,
            PduSpecific = ps,
            SourceAddress = sa,
            DestinationAddress = da,
            Data = data ?? Array.Empty<byte>(),
            Channel = channel,
            TimestampUs = timestampUs,
            SourceId = sourceId
        };
    }

    /// <summary>
    /// Encodes J1939 message parameters into a 29-bit extended CAN ID.
    /// </summary>
    public uint ToCanId()
    {
        uint canId = (uint)((Priority & 0x07) << 26);
        canId |= (uint)((DataPage & 0x01) << 24);
        canId |= (uint)(PduFormat << 16);

        if (PduFormat < 240)
            canId |= (uint)(DestinationAddress << 8);
        else
            canId |= (uint)(PduSpecific << 8);

        canId |= SourceAddress;
        return canId;
    }
}

public class J1939Capabilities : IAutomotiveCapabilities
{
    public AutomotiveProtocol Protocol => AutomotiveProtocol.J1939;
    public bool SupportsTransmit { get; init; } = true;
    public bool SupportsHardwareTimestamps { get; init; } = true;
    public int ChannelCount { get; init; } = 1;

    public bool SupportsTransportProtocol { get; init; } = true; // BAM and RTS/CTS CM/DT up to 1785 bytes
    public bool SupportsJ1939_22CanFd { get; init; } = true;
    public bool SupportsAddressClaiming { get; init; } = true;
}
