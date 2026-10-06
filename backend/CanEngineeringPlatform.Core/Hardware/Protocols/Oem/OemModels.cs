using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.Oem;

public class OemMessage
{
    public string OemIdentifier { get; init; } = string.Empty;
    public string ProtocolName { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
    public byte[] Payload { get; init; } = Array.Empty<byte>();
    public byte Channel { get; init; }
    public ulong TimestampUs { get; init; }
    public string? SourceId { get; init; }

    public OemMessage() { }

    public OemMessage(
        string oemIdentifier,
        string protocolName,
        byte[] payload,
        IReadOnlyDictionary<string, string>? parameters = null,
        byte channel = 0,
        ulong timestampUs = 0,
        string? sourceId = null)
    {
        OemIdentifier = oemIdentifier;
        ProtocolName = protocolName;
        Payload = payload ?? Array.Empty<byte>();
        Parameters = parameters ?? new Dictionary<string, string>();
        Channel = channel;
        TimestampUs = timestampUs;
        SourceId = sourceId;
    }
}

public class OemCapabilities : IAutomotiveCapabilities
{
    public AutomotiveProtocol Protocol => AutomotiveProtocol.OemSpecific;
    public bool SupportsTransmit { get; init; } = true;
    public bool SupportsHardwareTimestamps { get; init; } = true;
    public int ChannelCount { get; init; } = 1;

    public string OemIdentifier { get; init; } = string.Empty;
    public string ProtocolFamily { get; init; } = string.Empty;
    public IReadOnlyList<string> SupportedFeatures { get; init; } = Array.Empty<string>();
}
