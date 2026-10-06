using System;
using CanEngineeringPlatform.Core.Decoders;
using CanEngineeringPlatform.Core.Models;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class BinaryPacketDecoderTests
{
    [Fact]
    public void TryDecode_ValidStandardFrame_DecodesCorrectly()
    {
        // Setup 20-byte packet: AA 55, ts=0x0001E240 (123456us), id=0x7DF, dlc=8, flags=0, data=[02 01 0D 00 00 00 00 00]
        byte[] packet = new byte[20]
        {
            0xAA, 0x55,
            0x40, 0xE2, 0x01, 0x00, // 123456 us LE
            0xDF, 0x07, 0x00, 0x00, // 0x7DF LE
            0x08,                   // DLC 8
            0x00,                   // Standard, non-RTR
            0x02, 0x01, 0x0D, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        bool result = BinaryPacketDecoder.TryDecode(packet, out var frame);

        Assert.True(result);
        Assert.Equal(123456U, frame.TimestampUs);
        Assert.Equal(0x7DFU, frame.Id);
        Assert.Equal(8, frame.Dlc);
        Assert.False(frame.IsExtended);
        Assert.False(frame.IsRtr);
        Assert.Equal(0x02, frame.Data[0]);
        Assert.Equal(0x01, frame.Data[1]);
        Assert.Equal(0x0D, frame.Data[2]);
    }

    [Fact]
    public void TryDecode_ValidExtendedRtrFrame_DecodesCorrectly()
    {
        // Extended ID: 0x18DAF110, RTR=1
        byte[] packet = new byte[20]
        {
            0xAA, 0x55,
            0x10, 0x27, 0x00, 0x00, // 10000 us LE
            0x10, 0xF1, 0xDA, 0x18, // 0x18DAF110 LE
            0x00,                   // DLC 0
            0x03,                   // Extended (0x01) | RTR (0x02)
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        bool result = BinaryPacketDecoder.TryDecode(packet, out var frame);

        Assert.True(result);
        Assert.Equal(10000U, frame.TimestampUs);
        Assert.Equal(0x18DAF110U, frame.Id);
        Assert.Equal(0, frame.Dlc);
        Assert.True(frame.IsExtended);
        Assert.True(frame.IsRtr);
    }

    [Fact]
    public void TryDecode_InvalidMagicBytes_ReturnsFalse()
    {
        byte[] packet = new byte[20];
        packet[0] = 0x55;
        packet[1] = 0xAA;

        bool result = BinaryPacketDecoder.TryDecode(packet, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryDecode_PacketTooShort_ReturnsFalse()
    {
        byte[] packet = new byte[19];
        packet[0] = 0xAA;
        packet[1] = 0x55;

        bool result = BinaryPacketDecoder.TryDecode(packet, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryDecode_DlcGreaterThan8_ReturnsFalse()
    {
        byte[] packet = new byte[20]
        {
            0xAA, 0x55, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00,
            0x09, // DLC 9
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        bool result = BinaryPacketDecoder.TryDecode(packet, out _);
        Assert.False(result);
    }

    [Fact]
    public void Encode_DecodedFrame_RoundtripsIdenticalBytes()
    {
        var original = new CanFrame(987654U, 0x123U, 4, CanFrame.FlagExtended, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
        byte[] encoded = BinaryPacketDecoder.Encode(original);

        Assert.Equal(20, encoded.Length);
        Assert.Equal(0xAA, encoded[0]);
        Assert.Equal(0x55, encoded[1]);

        bool success = BinaryPacketDecoder.TryDecode(encoded, out var roundtrip);
        Assert.True(success);
        Assert.Equal(original.TimestampUs, roundtrip.TimestampUs);
        Assert.Equal(original.Id, roundtrip.Id);
        Assert.Equal(original.Dlc, roundtrip.Dlc);
        Assert.Equal(original.Flags, roundtrip.Flags);
        Assert.Equal(original.Data[0], roundtrip.Data[0]);
        Assert.Equal(original.Data[1], roundtrip.Data[1]);
        Assert.Equal(original.Data[2], roundtrip.Data[2]);
        Assert.Equal(original.Data[3], roundtrip.Data[3]);
    }
}
