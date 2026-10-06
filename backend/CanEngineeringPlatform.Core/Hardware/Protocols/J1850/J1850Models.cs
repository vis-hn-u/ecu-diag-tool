using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.J1850;

public enum J1850Variant
{
    Vpw_10_4K, // SAE J1850 VPW (10.4 kbps, GM Class 2, Chrysler)
    Pwm_41_6K  // SAE J1850 PWM (41.6 kbps, Ford Standard Corporate Protocol / SCP)
}

public class J1850Frame
{
    public J1850Variant Variant { get; init; } = J1850Variant.Vpw_10_4K;
    public byte[] Header { get; init; } = Array.Empty<byte>();
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public byte Crc { get; init; }
    public bool IsCrcValid { get; init; } = true;
    public byte[]? InFrameResponse { get; init; }
    public bool HasInFrameResponse => InFrameResponse != null && InFrameResponse.Length > 0;
    public byte Channel { get; init; }
    public ulong TimestampUs { get; init; }
    public string? SourceId { get; init; }

    public J1850Frame() { }

    public J1850Frame(J1850Variant variant, byte[] header, byte[] data, byte[]? inFrameResponse = null, byte channel = 0, ulong timestampUs = 0, string? sourceId = null)
    {
        Variant = variant;
        Header = header ?? Array.Empty<byte>();
        Data = data ?? Array.Empty<byte>();
        InFrameResponse = inFrameResponse;
        Channel = channel;
        TimestampUs = timestampUs;
        SourceId = sourceId;

        // Calculate standard SAE J1850 CRC-8 over Header + Data
        Crc = CalculateCrc(Header, Data);
        IsCrcValid = true;
    }

    public static byte CalculateCrc(byte[] header, byte[] data)
    {
        byte crc = 0xFF; // J1850 CRC preset
        foreach (byte b in header) crc = UpdateCrc(crc, b);
        foreach (byte b in data) crc = UpdateCrc(crc, b);
        return (byte)~crc; // One's complement
    }

    private static byte UpdateCrc(byte crc, byte b)
    {
        crc ^= b;
        for (int i = 0; i < 8; i++)
        {
            if ((crc & 0x80) != 0)
                crc = (byte)((crc << 1) ^ 0x1D); // Poly 0x1D (x^8 + x^4 + x^3 + x^2 + 1)
            else
                crc <<= 1;
        }
        return crc;
    }
}

public class J1850Capabilities : IAutomotiveCapabilities
{
    public AutomotiveProtocol Protocol => AutomotiveProtocol.J1850Vpw; // Or J1850Pwm
    public bool SupportsTransmit { get; init; } = true;
    public bool SupportsHardwareTimestamps { get; init; } = true;
    public int ChannelCount { get; init; } = 1;

    public bool SupportsVpw { get; init; } = true;
    public bool SupportsPwm { get; init; } = true;
    public bool SupportsInFrameResponse { get; init; } = true;
    public int MaxPayloadBytes { get; init; } = 11;
}
