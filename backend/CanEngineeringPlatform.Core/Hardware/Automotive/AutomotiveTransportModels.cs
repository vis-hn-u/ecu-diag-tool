using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Hardware.Automotive;

/// <summary>
/// Supported automotive physical bus and higher-layer communication protocols.
/// </summary>
public enum AutomotiveProtocol
{
    Can,                // Controller Area Network & CAN FD (ISO 11898-1/2)
    Lin,                // Local Interconnect Network (ISO 17987)
    KLine,              // ISO 9141-2 / ISO 14230 K-Line
    Kwp2000,            // Keyword Protocol 2000 (ISO 14230)
    J1850Vpw,           // SAE J1850 VPW (10.4 kbps, Variable Pulse Width)
    J1850Pwm,           // SAE J1850 PWM (41.6 kbps, Pulse Width Modulation)
    AutomotiveEthernet, // Automotive Ethernet 100BASE-T1 / 1000BASE-T1 (IEEE 802.3bw/bp)
    DoIp,               // Diagnostic communication over Internet Protocol (ISO 13400)
    FlexRay,            // FlexRay (ISO 17458)
    J1939,              // Heavy Duty Commercial Vehicle Protocol (SAE J1939 / J1939-22)
    OemSpecific         // Proprietary OEM transport extensions
}

/// <summary>
/// Operational state of an automotive transport adapter.
/// </summary>
public enum TransportState
{
    Disconnected,
    Connecting,
    Connected,
    Suspended,
    Error
}

/// <summary>
/// Base capability interface for any automotive physical or protocol transport adapter.
/// </summary>
public interface IAutomotiveCapabilities
{
    AutomotiveProtocol Protocol { get; }
    bool SupportsTransmit { get; }
    bool SupportsHardwareTimestamps { get; }
    int ChannelCount { get; }
}

/// <summary>
/// Protocol-agnostic telemetry and status report for automotive transports.
/// </summary>
public class AutomotiveTransportStatus
{
    public string TransportId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AutomotiveProtocol Protocol { get; set; }
    public TransportState State { get; set; } = TransportState.Disconnected;
    public bool IsConnected => State == TransportState.Connected;
    public string ConnectionDetails { get; set; } = string.Empty;
    public long TotalPacketsReceived { get; set; }
    public long TotalPacketsTransmitted { get; set; }
    public double RxPacketRate { get; set; }
    public double TxPacketRate { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public string? LastError { get; set; }
    public IAutomotiveCapabilities? Capabilities { get; set; }
    public object? ExtendedTelemetry { get; set; }
}
