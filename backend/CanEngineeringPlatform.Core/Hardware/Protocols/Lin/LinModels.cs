using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.Lin;

public enum LinChecksumType
{
    Classic,  // Sum of data bytes only (LIN 1.3)
    Enhanced  // Sum of PID + data bytes (LIN 2.0 / 2.1 / 2.2 / ISO 17987)
}

public enum LinFrameType
{
    Unconditional,
    EventTriggered,
    Sporadic,
    DiagnosticMasterRequest, // ID 0x3C
    DiagnosticSlaveResponse   // ID 0x3D
}

public enum LinDirection
{
    Rx,
    Tx
}

[Flags]
public enum LinErrorFlags : byte
{
    None = 0x00,
    ChecksumMismatch = 0x01,
    SyncError = 0x02,
    FramingError = 0x04,
    Timeout = 0x08,
    BusDominantStuck = 0x10
}

public class LinFrame
{
    public byte Pid { get; init; }
    public byte FrameId { get; init; }
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public byte Checksum { get; init; }
    public LinChecksumType ChecksumType { get; init; } = LinChecksumType.Enhanced;
    public LinFrameType FrameType { get; init; } = LinFrameType.Unconditional;
    public LinDirection Direction { get; init; } = LinDirection.Rx;
    public LinErrorFlags Errors { get; init; } = LinErrorFlags.None;
    public byte Channel { get; init; }
    public ulong TimestampUs { get; init; }
    public string? SourceId { get; init; }

    public bool HasError => Errors != LinErrorFlags.None;

    public LinFrame() { }

    public LinFrame(byte frameId, byte[] data, LinChecksumType checksumType = LinChecksumType.Enhanced, byte channel = 0, ulong timestampUs = 0, string? sourceId = null)
    {
        FrameId = (byte)(frameId & 0x3F);
        Pid = CalculatePid(FrameId);
        Data = data ?? Array.Empty<byte>();
        ChecksumType = checksumType;
        FrameType = FrameId switch
        {
            0x3C => LinFrameType.DiagnosticMasterRequest,
            0x3D => LinFrameType.DiagnosticSlaveResponse,
            _ => LinFrameType.Unconditional
        };
        Checksum = CalculateChecksum(Pid, Data, ChecksumType);
        Channel = channel;
        TimestampUs = timestampUs;
        SourceId = sourceId;
    }

    public static byte CalculatePid(byte frameId)
    {
        byte id = (byte)(frameId & 0x3F);
        int p0 = ((id >> 0) ^ (id >> 1) ^ (id >> 2) ^ (id >> 4)) & 0x01;
        int p1 = ~((id >> 1) ^ (id >> 3) ^ (id >> 4) ^ (id >> 5)) & 0x01;
        return (byte)(id | (p0 << 6) | (p1 << 7));
    }

    public static byte CalculateChecksum(byte pid, byte[] data, LinChecksumType type)
    {
        int sum = (type == LinChecksumType.Enhanced) ? pid : 0;
        foreach (var b in data)
        {
            sum += b;
            if (sum > 0xFF) sum = (sum & 0xFF) + 1; // 1's complement carry addition
        }
        return (byte)(~sum & 0xFF);
    }
}

public class LinCapabilities : IAutomotiveCapabilities
{
    public AutomotiveProtocol Protocol => AutomotiveProtocol.Lin;
    public bool SupportsTransmit { get; init; } = true;
    public bool SupportsHardwareTimestamps { get; init; } = true;
    public int ChannelCount { get; init; } = 1;

    public IReadOnlyList<uint> SupportedBaudRates { get; init; } = new uint[] { 2400, 9600, 19200, 20000 };
    public bool SupportsMasterMode { get; init; } = true;
    public bool SupportsSlaveMode { get; init; } = true;
    public bool SupportsAutoBaud { get; init; }
    public bool SupportsHardwareChecksumCalculation { get; init; } = true;
}
