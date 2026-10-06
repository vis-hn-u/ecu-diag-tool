using System;
using System.Collections.Generic;
using System.IO;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.DoIp;

public enum DoIpPayloadType : ushort
{
    GenericHeaderNack = 0x0001,
    VehicleIdentificationRequest = 0x0002,
    VehicleIdentificationRequestWithEid = 0x0003,
    VehicleIdentificationRequestWithVin = 0x0004,
    RoutingActivationRequest = 0x0005,
    RoutingActivationResponse = 0x0006,
    AliveCheckRequest = 0x0007,
    AliveCheckResponse = 0x0008,
    EntityStatusRequest = 0x4001,
    EntityStatusResponse = 0x4002,
    DiagnosticMessage = 0x8001,
    DiagnosticMessageAck = 0x8002,
    DiagnosticMessageNack = 0x8003
}

public class DoIpMessage
{
    public byte ProtocolVersion { get; init; } = 0x02; // ISO 13400-2:2012
    public byte InverseProtocolVersion => (byte)(~ProtocolVersion & 0xFF);
    public DoIpPayloadType PayloadType { get; init; } = DoIpPayloadType.DiagnosticMessage;
    public uint PayloadLength { get; init; }
    public ushort? SourceLogicalAddress { get; init; }
    public ushort? TargetLogicalAddress { get; init; }
    public byte[] DiagnosticPayload { get; init; } = Array.Empty<byte>();
    public ulong TimestampUs { get; init; }
    public string? SourceId { get; init; }

    public DoIpMessage() { }

    public static DoIpMessage CreateDiagnosticMessage(ushort sourceAddress, ushort targetAddress, byte[] udsPayload, byte protocolVersion = 0x02, ulong timestampUs = 0, string? sourceId = null)
    {
        byte[] payload = udsPayload ?? Array.Empty<byte>();
        uint len = 4 + (uint)payload.Length; // 2 bytes source + 2 bytes target + UDS data

        return new DoIpMessage
        {
            ProtocolVersion = protocolVersion,
            PayloadType = DoIpPayloadType.DiagnosticMessage,
            PayloadLength = len,
            SourceLogicalAddress = sourceAddress,
            TargetLogicalAddress = targetAddress,
            DiagnosticPayload = payload,
            TimestampUs = timestampUs,
            SourceId = sourceId
        };
    }

    public static DoIpMessage CreateRoutingActivationRequest(ushort sourceAddress, byte activationType = 0x00, byte protocolVersion = 0x02)
    {
        return new DoIpMessage
        {
            ProtocolVersion = protocolVersion,
            PayloadType = DoIpPayloadType.RoutingActivationRequest,
            PayloadLength = 7, // 2 source + 1 type + 4 reserved
            SourceLogicalAddress = sourceAddress,
            DiagnosticPayload = new byte[] { activationType, 0x00, 0x00, 0x00, 0x00 }
        };
    }

    public byte[] ToRawBytes()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(ProtocolVersion);
        writer.Write(InverseProtocolVersion);
        writer.Write((byte)(((ushort)PayloadType >> 8) & 0xFF));
        writer.Write((byte)((ushort)PayloadType & 0xFF));

        // Calculate actual payload
        using var payloadMs = new MemoryStream();
        using var payloadWriter = new BinaryWriter(payloadMs);
        if (SourceLogicalAddress.HasValue && TargetLogicalAddress.HasValue)
        {
            payloadWriter.Write((byte)((SourceLogicalAddress.Value >> 8) & 0xFF));
            payloadWriter.Write((byte)(SourceLogicalAddress.Value & 0xFF));
            payloadWriter.Write((byte)((TargetLogicalAddress.Value >> 8) & 0xFF));
            payloadWriter.Write((byte)(TargetLogicalAddress.Value & 0xFF));
        }
        else if (SourceLogicalAddress.HasValue)
        {
            payloadWriter.Write((byte)((SourceLogicalAddress.Value >> 8) & 0xFF));
            payloadWriter.Write((byte)(SourceLogicalAddress.Value & 0xFF));
        }

        if (DiagnosticPayload.Length > 0)
        {
            payloadWriter.Write(DiagnosticPayload);
        }

        byte[] payloadBytes = payloadMs.ToArray();
        uint len = (uint)payloadBytes.Length;

        // Write 4-byte big-endian length
        writer.Write((byte)((len >> 24) & 0xFF));
        writer.Write((byte)((len >> 16) & 0xFF));
        writer.Write((byte)((len >> 8) & 0xFF));
        writer.Write((byte)(len & 0xFF));

        writer.Write(payloadBytes);
        return ms.ToArray();
    }
}

public class DoIpCapabilities : IAutomotiveCapabilities
{
    public AutomotiveProtocol Protocol => AutomotiveProtocol.DoIp;
    public bool SupportsTransmit { get; init; } = true;
    public bool SupportsHardwareTimestamps { get; init; } = true;
    public int ChannelCount { get; init; } = 1;

    public IReadOnlyList<byte> SupportedProtocolVersions { get; init; } = new byte[] { 0x02, 0x03 };
    public bool SupportsTls { get; init; }
    public uint MaxDiagnosticPayloadBytes { get; init; } = 65535;
    public int DefaultTcpPort { get; init; } = 13400;
    public int DefaultUdpPort { get; init; } = 13400;
}
