using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Protocols.Ethernet;

public enum EthernetPhyType
{
    BaseT1_100,    // 100BASE-T1 (IEEE 802.3bw / BroadR-Reach, 100 Mbps, single unshielded twisted pair)
    BaseT1_1000,   // 1000BASE-T1 (IEEE 802.3bp, 1 Gbps single pair)
    BaseTx_100,    // Standard 100BASE-TX (diagnostics connector / bench testing)
    BaseT_1000     // Standard 1000BASE-T
}

public class AutomotiveEthernetFrame
{
    public byte[] DestinationMac { get; init; } = new byte[6];
    public byte[] SourceMac { get; init; } = new byte[6];
    public ushort EtherType { get; init; } = 0x0800; // Default IPv4
    public ushort? VlanId { get; init; }            // IEEE 802.1Q VLAN Tag (0 - 4095)
    public byte? VlanPriority { get; init; }        // 802.1p PCP (0 - 7)
    public byte[] Payload { get; init; } = Array.Empty<byte>();
    public EthernetPhyType PhyType { get; init; } = EthernetPhyType.BaseT1_100;
    public int PortIndex { get; init; } = 0;
    public ulong TimestampUs { get; init; }
    public string? SourceId { get; init; }

    public AutomotiveEthernetFrame() { }

    public AutomotiveEthernetFrame(
        byte[] destinationMac,
        byte[] sourceMac,
        ushort etherType,
        byte[] payload,
        ushort? vlanId = null,
        byte? vlanPriority = null,
        EthernetPhyType phyType = EthernetPhyType.BaseT1_100,
        int portIndex = 0,
        ulong timestampUs = 0,
        string? sourceId = null)
    {
        DestinationMac = destinationMac ?? new byte[6];
        SourceMac = sourceMac ?? new byte[6];
        EtherType = etherType;
        Payload = payload ?? Array.Empty<byte>();
        VlanId = vlanId;
        VlanPriority = vlanPriority;
        PhyType = phyType;
        PortIndex = portIndex;
        TimestampUs = timestampUs;
        SourceId = sourceId;
    }
}

public class AutomotiveEthernetCapabilities : IAutomotiveCapabilities
{
    public AutomotiveProtocol Protocol => AutomotiveProtocol.AutomotiveEthernet;
    public bool SupportsTransmit { get; init; } = true;
    public bool SupportsHardwareTimestamps { get; init; } = true;
    public int ChannelCount { get; init; } = 1;

    public uint LinkSpeedMbps { get; init; } = 100;
    public bool SupportsVlanTagging { get; init; } = true;
    public bool SupportsPromiscuousMode { get; init; } = true;
    public IReadOnlyList<EthernetPhyType> SupportedPhyTypes { get; init; } = new[]
    {
        EthernetPhyType.BaseT1_100,
        EthernetPhyType.BaseT1_1000,
        EthernetPhyType.BaseTx_100
    };
}
