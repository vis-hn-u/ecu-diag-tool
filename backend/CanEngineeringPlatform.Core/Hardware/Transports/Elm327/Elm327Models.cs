using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.Hardware.Automotive;

namespace CanEngineeringPlatform.Core.Hardware.Transports.Elm327;

/// <summary>
/// Supported ELM327 standard and extended protocol identifiers.
/// </summary>
public enum Elm327Protocol
{
    Auto = 0,                       // 0: Automatic protocol search
    SaeJ1850Pwm = 1,                // 1: SAE J1850 PWM (41.6 kbaud)
    SaeJ1850Vpw = 2,                // 2: SAE J1850 VPW (10.4 kbaud)
    Iso9141_2 = 3,                  // 3: ISO 9141-2 (5 baud init, 10.4 kbaud)
    Iso14230_4_Kwp_5Baud = 4,       // 4: ISO 14230-4 KWP (5 baud init, 10.4 kbaud)
    Iso14230_4_Kwp_Fast = 5,        // 5: ISO 14230-4 KWP (fast init, 10.4 kbaud)
    Iso15765_4_Can_11Bit_500k = 6,  // 6: ISO 15765-4 CAN (11 bit ID, 500 kbaud)
    Iso15765_4_Can_29Bit_500k = 7,  // 7: ISO 15765-4 CAN (29 bit ID, 500 kbaud)
    Iso15765_4_Can_11Bit_250k = 8,  // 8: ISO 15765-4 CAN (11 bit ID, 250 kbaud)
    Iso15765_4_Can_29Bit_250k = 9,  // 9: ISO 15765-4 CAN (29 bit ID, 250 kbaud)
    SaeJ1939_Can_29Bit_250k = 10,   // A: SAE J1939 CAN (29 bit ID, 250 kbaud)
    User1_Can = 11,                 // B: User 1 CAN (11 or 29 bit ID)
    User2_Can = 12,                 // C: User 2 CAN (11 or 29 bit ID)
    Unknown = 99
}

public static class Elm327ProtocolExtensions
{
    public static string ToProtocolCode(this Elm327Protocol protocol) => protocol switch
    {
        Elm327Protocol.Auto => "0",
        Elm327Protocol.SaeJ1850Pwm => "1",
        Elm327Protocol.SaeJ1850Vpw => "2",
        Elm327Protocol.Iso9141_2 => "3",
        Elm327Protocol.Iso14230_4_Kwp_5Baud => "4",
        Elm327Protocol.Iso14230_4_Kwp_Fast => "5",
        Elm327Protocol.Iso15765_4_Can_11Bit_500k => "6",
        Elm327Protocol.Iso15765_4_Can_29Bit_500k => "7",
        Elm327Protocol.Iso15765_4_Can_11Bit_250k => "8",
        Elm327Protocol.Iso15765_4_Can_29Bit_250k => "9",
        Elm327Protocol.SaeJ1939_Can_29Bit_250k => "A",
        Elm327Protocol.User1_Can => "B",
        Elm327Protocol.User2_Can => "C",
        _ => "0"
    };

    public static Elm327Protocol FromProtocolCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return Elm327Protocol.Unknown;
        string clean = code.Trim().ToUpperInvariant().TrimStart('A', 'T', 'P', 'S', ' ');
        if (clean.Length > 1) clean = clean.Substring(clean.Length - 1);

        return clean switch
        {
            "0" => Elm327Protocol.Auto,
            "1" => Elm327Protocol.SaeJ1850Pwm,
            "2" => Elm327Protocol.SaeJ1850Vpw,
            "3" => Elm327Protocol.Iso9141_2,
            "4" => Elm327Protocol.Iso14230_4_Kwp_5Baud,
            "5" => Elm327Protocol.Iso14230_4_Kwp_Fast,
            "6" => Elm327Protocol.Iso15765_4_Can_11Bit_500k,
            "7" => Elm327Protocol.Iso15765_4_Can_29Bit_500k,
            "8" => Elm327Protocol.Iso15765_4_Can_11Bit_250k,
            "9" => Elm327Protocol.Iso15765_4_Can_29Bit_250k,
            "A" => Elm327Protocol.SaeJ1939_Can_29Bit_250k,
            "B" => Elm327Protocol.User1_Can,
            "C" => Elm327Protocol.User2_Can,
            _ => Elm327Protocol.Unknown
        };
    }

    public static string GetDescription(this Elm327Protocol protocol) => protocol switch
    {
        Elm327Protocol.Auto => "Automatic Protocol Search",
        Elm327Protocol.SaeJ1850Pwm => "SAE J1850 PWM (41.6 kbaud)",
        Elm327Protocol.SaeJ1850Vpw => "SAE J1850 VPW (10.4 kbaud)",
        Elm327Protocol.Iso9141_2 => "ISO 9141-2 (5 baud init, 10.4 kbaud)",
        Elm327Protocol.Iso14230_4_Kwp_5Baud => "ISO 14230-4 KWP (5 baud init)",
        Elm327Protocol.Iso14230_4_Kwp_Fast => "ISO 14230-4 KWP (Fast init)",
        Elm327Protocol.Iso15765_4_Can_11Bit_500k => "ISO 15765-4 CAN (11-bit ID, 500 kbaud)",
        Elm327Protocol.Iso15765_4_Can_29Bit_500k => "ISO 15765-4 CAN (29-bit ID, 500 kbaud)",
        Elm327Protocol.Iso15765_4_Can_11Bit_250k => "ISO 15765-4 CAN (11-bit ID, 250 kbaud)",
        Elm327Protocol.Iso15765_4_Can_29Bit_250k => "ISO 15765-4 CAN (29-bit ID, 250 kbaud)",
        Elm327Protocol.SaeJ1939_Can_29Bit_250k => "SAE J1939 CAN (29-bit ID, 250 kbaud)",
        Elm327Protocol.User1_Can => "User 1 CAN",
        Elm327Protocol.User2_Can => "User 2 CAN",
        _ => "Unknown / Undetected Protocol"
    };

    public static bool IsCanProtocol(this Elm327Protocol protocol) => protocol switch
    {
        Elm327Protocol.Iso15765_4_Can_11Bit_500k => true,
        Elm327Protocol.Iso15765_4_Can_29Bit_500k => true,
        Elm327Protocol.Iso15765_4_Can_11Bit_250k => true,
        Elm327Protocol.Iso15765_4_Can_29Bit_250k => true,
        Elm327Protocol.SaeJ1939_Can_29Bit_250k => true,
        Elm327Protocol.User1_Can => true,
        Elm327Protocol.User2_Can => true,
        _ => false
    };

    public static AutomotiveProtocol ToAutomotiveProtocol(this Elm327Protocol protocol) => protocol switch
    {
        Elm327Protocol.SaeJ1850Pwm => AutomotiveProtocol.J1850Pwm,
        Elm327Protocol.SaeJ1850Vpw => AutomotiveProtocol.J1850Vpw,
        Elm327Protocol.Iso9141_2 => AutomotiveProtocol.KLine,
        Elm327Protocol.Iso14230_4_Kwp_5Baud => AutomotiveProtocol.Kwp2000,
        Elm327Protocol.Iso14230_4_Kwp_Fast => AutomotiveProtocol.Kwp2000,
        Elm327Protocol.SaeJ1939_Can_29Bit_250k => AutomotiveProtocol.J1939,
        _ => AutomotiveProtocol.Can
    };
}

/// <summary>
/// Connection state of an ELM327 Bluetooth adapter.
/// </summary>
public enum Elm327ConnectionState
{
    Disconnected,
    Connecting,
    Handshaking,
    Connected,
    Reconnecting,
    Error
}

/// <summary>
/// Identification metadata gathered from an ELM327 adapter during handshake.
/// </summary>
public class Elm327AdapterInfo
{
    public string DeviceName { get; set; } = "ELM327 Adapter";
    public string FirmwareVersion { get; set; } = "Unknown";
    public string Description { get; set; } = "ELM327 Compatible Interface";
    public string Identifier { get; set; } = string.Empty;
    public double? BatteryVoltage { get; set; }
    public Elm327Protocol ActiveProtocol { get; set; } = Elm327Protocol.Auto;
    public string ActiveProtocolName { get; set; } = "Auto";
    public string PortName { get; set; } = "Loopback";
    public int BaudRate { get; set; } = 115200;
    public string ConnectionType { get; set; } = "Bluetooth RFCOMM";
    public int ReconnectAttempts { get; set; }
    public DateTime? ConnectedAtUtc { get; set; }
}

/// <summary>
/// Hardware and protocol capabilities reported or detected for the ELM327 device.
/// </summary>
public class Elm327Capabilities : IAutomotiveCapabilities
{
    public AutomotiveProtocol Protocol { get; set; } = AutomotiveProtocol.Can;
    public bool SupportsTransmit => true;
    public bool SupportsHardwareTimestamps => false;
    public int ChannelCount => 1;
    public int MaxPayloadBytes => 8;
    public bool SupportsCanFd => false;
    public bool SupportsBitRateSwitch => false;
    public bool SupportsClassicCan { get; set; } = true;

    // ELM327 Specific Capability Flags
    public bool SupportsCan { get; set; } = true;
    public bool SupportsIso15765Can { get; set; } = true;
    public bool SupportsIso9141 { get; set; } = true;
    public bool SupportsKwp2000 { get; set; } = true;
    public bool SupportsJ1850Vpw { get; set; } = true;
    public bool SupportsJ1850Pwm { get; set; } = true;
    public bool SupportsVoltageMeasurement { get; set; } = true;
    public bool SupportsAdaptiveTiming { get; set; } = true;
    public bool SupportsCustomHeaders { get; set; } = true;
    public bool SupportsCanMonitoring { get; set; } = true;

    public IReadOnlyList<Elm327Protocol> SupportedProtocols { get; set; } = new[]
    {
        Elm327Protocol.Iso15765_4_Can_11Bit_500k,
        Elm327Protocol.Iso15765_4_Can_29Bit_500k,
        Elm327Protocol.Iso15765_4_Can_11Bit_250k,
        Elm327Protocol.Iso15765_4_Can_29Bit_250k,
        Elm327Protocol.Iso9141_2,
        Elm327Protocol.Iso14230_4_Kwp_Fast,
        Elm327Protocol.Iso14230_4_Kwp_5Baud,
        Elm327Protocol.SaeJ1850Vpw,
        Elm327Protocol.SaeJ1850Pwm
    };
}

/// <summary>
/// Classification of ELM327 responses.
/// </summary>
public enum Elm327ResponseType
{
    Ok,
    Data,
    NoData,
    CanError,
    BusBusy,
    BufferFull,
    DataError,
    UnknownCommand,
    Stopped,
    Timeout,
    Error
}

/// <summary>
/// Structured result of an ELM327 command exchange.
/// </summary>
public class Elm327Response
{
    public bool Success { get; set; }
    public string RawResponse { get; set; } = string.Empty;
    public IReadOnlyList<string> Lines { get; set; } = Array.Empty<string>();
    public Elm327ResponseType Type { get; set; } = Elm327ResponseType.Ok;
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
}
