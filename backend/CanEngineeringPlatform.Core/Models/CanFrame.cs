using System;
using System.Text;

namespace CanEngineeringPlatform.Core.Models;

public enum CanDirection : byte
{
    Rx = 0,
    Tx = 1
}

public struct CanFrame : IEquatable<CanFrame>
{
    public const byte FlagExtended = 0x01;
    public const byte FlagRtr = 0x02;
    public const byte FlagCanFd = 0x04;
    public const byte FlagBrs = 0x08;
    public const byte FlagEsi = 0x10;
    public const byte FlagTx = 0x20;
    public const byte FlagMask = FlagExtended | FlagRtr | FlagCanFd | FlagBrs | FlagEsi | FlagTx;

    public const uint StandardIdMax = 0x7FFU;
    public const uint ExtendedIdMax = 0x1FFFFFFFU;

    public uint TimestampUs { get; set; }
    public uint Id { get; set; }
    public byte Dlc { get; set; }
    public byte Flags { get; set; }
    public byte[] Data { get; set; }
    public byte Channel { get; set; }
    public string SourceId { get; set; }

    public bool IsExtended => (Flags & FlagExtended) != 0;
    public bool IsRtr => (Flags & FlagRtr) != 0;
    public bool IsCanFd => (Flags & FlagCanFd) != 0;
    public bool BitRateSwitch => (Flags & FlagBrs) != 0;
    public bool ErrorStateIndicator => (Flags & FlagEsi) != 0;
    public CanDirection Direction => (Flags & FlagTx) != 0 ? CanDirection.Tx : CanDirection.Rx;

    public byte ActualLength => (byte)(Data != null ? Math.Min(Data.Length, DlcToLength(Dlc)) : 0);

    public CanFrame()
    {
        TimestampUs = 0;
        Id = 0;
        Dlc = 0;
        Flags = 0;
        Data = new byte[8];
        Channel = 0;
        SourceId = string.Empty;
    }

    /// <summary>
    /// Classic CAN Frame Constructor (up to 8 bytes payload).
    /// Preserves complete backwards compatibility with Phase 1-5 firmware and tests.
    /// </summary>
    public CanFrame(uint timestampUs, uint id, byte dlc, byte flags, ReadOnlySpan<byte> data)
    {
        TimestampUs = timestampUs;
        Id = id;
        Dlc = dlc;
        Flags = (byte)(flags & FlagMask);
        Channel = 0;
        SourceId = string.Empty;

        bool isFd = (Flags & FlagCanFd) != 0;
        int maxLen = isFd ? 64 : 8;
        int targetLen = isFd ? DlcToLength(dlc) : 8;
        Data = new byte[Math.Min(maxLen, targetLen)];

        int copyLen = Math.Min(Data.Length, data.Length);
        if (copyLen > 0)
        {
            data.Slice(0, copyLen).CopyTo(Data);
        }
    }

    /// <summary>
    /// Flexible CAN / CAN FD Frame Constructor (up to 64 bytes payload with channel and hardware source).
    /// </summary>
    public CanFrame(
        uint timestampUs,
        uint id,
        byte dlc,
        byte flags,
        ReadOnlySpan<byte> data,
        byte channel,
        string? sourceId = null)
    {
        TimestampUs = timestampUs;
        Id = id;
        Dlc = dlc;
        Flags = (byte)(flags & FlagMask);
        Channel = channel;
        SourceId = sourceId ?? string.Empty;

        bool isFd = (Flags & FlagCanFd) != 0;
        int maxLen = isFd ? 64 : 8;
        int targetLen = isFd ? DlcToLength(dlc) : Math.Min(8, (int)dlc);
        Data = new byte[Math.Min(maxLen, targetLen)];

        int copyLen = Math.Min(Data.Length, data.Length);
        if (copyLen > 0)
        {
            data.Slice(0, copyLen).CopyTo(Data);
        }
    }

    public static CanFrame CreateClassic(
        uint id,
        ReadOnlySpan<byte> data,
        bool isExtended = false,
        bool isRtr = false,
        uint timestampUs = 0,
        byte channel = 0,
        string? sourceId = null,
        CanDirection direction = CanDirection.Rx)
    {
        byte flags = 0;
        if (isExtended) flags |= FlagExtended;
        if (isRtr) flags |= FlagRtr;
        if (direction == CanDirection.Tx) flags |= FlagTx;

        byte dlc = (byte)Math.Min(8, data.Length);
        return new CanFrame(timestampUs, id, dlc, flags, data, channel, sourceId);
    }

    public static CanFrame CreateCanFd(
        uint id,
        ReadOnlySpan<byte> data,
        bool isExtended = false,
        bool bitRateSwitch = true,
        bool errorStateIndicator = false,
        uint timestampUs = 0,
        byte channel = 0,
        string? sourceId = null,
        CanDirection direction = CanDirection.Rx)
    {
        byte flags = FlagCanFd;
        if (isExtended) flags |= FlagExtended;
        if (bitRateSwitch) flags |= FlagBrs;
        if (errorStateIndicator) flags |= FlagEsi;
        if (direction == CanDirection.Tx) flags |= FlagTx;

        byte dlc = LengthToDlc(data.Length);
        return new CanFrame(timestampUs, id, dlc, flags, data, channel, sourceId);
    }

    public static int DlcToLength(byte dlc) => dlc switch
    {
        <= 8 => dlc,
        9 => 12,
        10 => 16,
        11 => 20,
        12 => 24,
        13 => 32,
        14 => 48,
        _ => 64
    };

    public static byte LengthToDlc(int length) => length switch
    {
        <= 0 => 0,
        <= 8 => (byte)length,
        <= 12 => 9,
        <= 16 => 10,
        <= 20 => 11,
        <= 24 => 12,
        <= 32 => 13,
        <= 48 => 14,
        _ => 15
    };

    public bool IsValid()
    {
        if (IsCanFd)
        {
            if (Dlc > 15) return false;
            if (IsRtr) return false; // ISO 11898-1: CAN FD does not support RTR
            if (Data != null && Data.Length > 64) return false;
        }
        else
        {
            if (Dlc > 8) return false;
            if (Data != null && Data.Length > 8) return false;
        }

        if ((Flags & ~FlagMask) != 0) return false;

        if (IsExtended)
        {
            return Id <= ExtendedIdMax;
        }
        return Id <= StandardIdMax;
    }

    public string ToHexString()
    {
        int len = ActualLength;
        if (len == 0 || Data == null) return string.Empty;

        var sb = new StringBuilder(len * 3);
        for (int i = 0; i < len; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(Data[i].ToString("X2"));
        }
        return sb.ToString();
    }

    public bool Equals(CanFrame other)
    {
        if (TimestampUs != other.TimestampUs ||
            Id != other.Id ||
            Dlc != other.Dlc ||
            Flags != other.Flags ||
            Channel != other.Channel ||
            !string.Equals(SourceId, other.SourceId, StringComparison.Ordinal))
        {
            return false;
        }

        int len = ActualLength;
        int otherLen = other.ActualLength;
        if (len != otherLen) return false;

        if (Data == null && other.Data == null) return true;
        if (Data == null || other.Data == null) return false;

        for (int i = 0; i < len; i++)
        {
            if (Data[i] != other.Data[i]) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is CanFrame frame && Equals(frame);

    public override int GetHashCode() => HashCode.Combine(TimestampUs, Id, Dlc, Flags, Channel, SourceId);

    public static bool operator ==(CanFrame left, CanFrame right) => left.Equals(right);
    public static bool operator !=(CanFrame left, CanFrame right) => !left.Equals(right);
}
