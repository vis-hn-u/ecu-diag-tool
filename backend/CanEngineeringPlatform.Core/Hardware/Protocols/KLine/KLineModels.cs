using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.KLine;

public enum KLineInitType
{
    None,
    FiveBaudInit,
    FastInit
}

public enum KLineDirection
{
    Rx,
    Tx
}

public class KLineMessage
{
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public KLineInitType InitType { get; init; } = KLineInitType.None;
    public byte? KeyByte1 { get; init; }
    public byte? KeyByte2 { get; init; }
    public byte Checksum { get; init; }
    public bool IsChecksumValid { get; init; } = true;
    public byte Channel { get; init; }
    public ulong TimestampUs { get; init; }
    public KLineDirection Direction { get; init; } = KLineDirection.Rx;
    public string? SourceId { get; init; }

    public KLineMessage() { }

    public KLineMessage(byte[] data, byte channel = 0, ulong timestampUs = 0, KLineDirection direction = KLineDirection.Rx, string? sourceId = null)
    {
        Data = data ?? Array.Empty<byte>();
        Channel = channel;
        TimestampUs = timestampUs;
        Direction = direction;
        SourceId = sourceId;

        if (Data.Length > 0)
        {
            Checksum = Data[^1];
            byte calculated = CalculateChecksum(Data, 0, Data.Length - 1);
            IsChecksumValid = (Checksum == calculated);
        }
    }

    public static byte CalculateChecksum(byte[] buffer, int offset, int length)
    {
        byte sum = 0;
        for (int i = offset; i < offset + length && i < buffer.Length; i++)
        {
            sum += buffer[i];
        }
        return sum;
    }
}

public class KLineCapabilities : IAutomotiveCapabilities
{
    public AutomotiveProtocol Protocol => AutomotiveProtocol.KLine;
    public bool SupportsTransmit { get; init; } = true;
    public bool SupportsHardwareTimestamps { get; init; } = true;
    public int ChannelCount { get; init; } = 1;

    public bool Supports5BaudInit { get; init; } = true;
    public bool SupportsFastInit { get; init; } = true;
    public IReadOnlyList<uint> SupportedBaudRates { get; init; } = new uint[] { 4800, 9600, 10400 };
    public uint MinInterByteDelayMs { get; init; } = 5;
    public uint TargetResponseTimeoutMs { get; init; } = 300;
}
