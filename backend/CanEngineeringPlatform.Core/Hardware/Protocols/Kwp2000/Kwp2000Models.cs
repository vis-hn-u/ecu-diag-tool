using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.Kwp2000;

public enum KwpHeaderFormat
{
    NoAddressWithLength,        // Format byte contains length directly, no target/source
    TargetSourceWithLength,     // Format byte contains length + target and source addresses
    TargetSourceWithoutLength   // Format byte length is 0, additional length byte present
}

public enum KwpPhysicalLayer
{
    KLine,       // ISO 14230-1 / ISO 14230-2
    Can          // ISO 14230-4 (Keyword Protocol 2000 over CAN)
}

public class Kwp2000Message
{
    public byte FormatByte { get; init; }
    public KwpHeaderFormat Format { get; init; }
    public byte? TargetAddress { get; init; }
    public byte? SourceAddress { get; init; }
    public byte? AdditionalLength { get; init; }
    public byte ServiceId { get; init; }
    public byte[] Payload { get; init; } = Array.Empty<byte>();
    public byte Checksum { get; init; }
    public bool IsChecksumValid { get; init; } = true;
    public KwpPhysicalLayer PhysicalLayer { get; init; } = KwpPhysicalLayer.KLine;
    public byte Channel { get; init; }
    public ulong TimestampUs { get; init; }
    public string? SourceId { get; init; }

    public Kwp2000Message() { }

    public Kwp2000Message(byte serviceId, byte[] payload, byte? targetAddress = null, byte? sourceAddress = null, KwpPhysicalLayer physicalLayer = KwpPhysicalLayer.KLine, byte channel = 0, ulong timestampUs = 0, string? sourceId = null)
    {
        ServiceId = serviceId;
        Payload = payload ?? Array.Empty<byte>();
        TargetAddress = targetAddress;
        SourceAddress = sourceAddress;
        PhysicalLayer = physicalLayer;
        Channel = channel;
        TimestampUs = timestampUs;
        SourceId = sourceId;

        int length = 1 + Payload.Length; // SID + payload
        if (targetAddress.HasValue && sourceAddress.HasValue)
        {
            if (length <= 63)
            {
                Format = KwpHeaderFormat.TargetSourceWithLength;
                FormatByte = (byte)(0x80 | length);
            }
            else
            {
                Format = KwpHeaderFormat.TargetSourceWithoutLength;
                FormatByte = 0x80;
                AdditionalLength = (byte)length;
            }
        }
        else
        {
            Format = KwpHeaderFormat.NoAddressWithLength;
            FormatByte = (byte)(length & 0x3F);
        }
    }

    public byte[] ToRawBytes()
    {
        var bytes = new List<byte> { FormatByte };
        if (TargetAddress.HasValue) bytes.Add(TargetAddress.Value);
        if (SourceAddress.HasValue) bytes.Add(SourceAddress.Value);
        if (AdditionalLength.HasValue) bytes.Add(AdditionalLength.Value);
        bytes.Add(ServiceId);
        bytes.AddRange(Payload);

        byte sum = 0;
        foreach (var b in bytes) sum += b;
        bytes.Add(sum);
        return bytes.ToArray();
    }
}

public class Kwp2000Capabilities : IAutomotiveCapabilities
{
    public AutomotiveProtocol Protocol => AutomotiveProtocol.Kwp2000;
    public bool SupportsTransmit { get; init; } = true;
    public bool SupportsHardwareTimestamps { get; init; } = true;
    public int ChannelCount { get; init; } = 1;

    public bool SupportsKLineLayer { get; init; } = true;
    public bool SupportsCanLayer { get; init; } = true;
    public IReadOnlyList<KwpHeaderFormat> SupportedHeaderFormats { get; init; } = new[]
    {
        KwpHeaderFormat.TargetSourceWithLength,
        KwpHeaderFormat.TargetSourceWithoutLength,
        KwpHeaderFormat.NoAddressWithLength
    };
    public IReadOnlyList<uint> SupportedBaudRates { get; init; } = new uint[] { 9600, 10400, 500000 };
}
