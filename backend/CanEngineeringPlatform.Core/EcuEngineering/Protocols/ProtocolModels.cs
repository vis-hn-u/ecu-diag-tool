using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.EcuEngineering.Protocols;

public enum DiagnosticProtocolType
{
    UdsOnIsoTp,
    Obd2Iso15765,
    Kwp2000,
    VagTp20,
    J1939,
    CanFdRaw,
    Unknown
}

public enum AddressingSchemeType
{
    Standard11BitUnicast,      // 0x7E0 / 0x7E8 (Physical ECM)
    Standard11BitFunctional,   // 0x7DF (Broadcast)
    Extended29BitUnicast,      // 0x18DAxxF1 (Normal Fixed Addressing)
    Extended29BitFunctional,   // 0x18DB33F1 (Functional Broadcast)
    VagChannel200,             // 0x200 (KWP2000 / TP2.0 dynamically negotiated)
    Unknown
}

public class ServiceCapability
{
    public byte ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public bool IsSupported { get; set; }
    public bool RequiresSecurityAccess { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class EcuProtocolCapabilityMatrix
{
    public string EcuName { get; set; } = "ECM";
    public DiagnosticProtocolType PrimaryProtocol { get; set; } = DiagnosticProtocolType.Unknown;
    public AddressingSchemeType AddressingScheme { get; set; } = AddressingSchemeType.Standard11BitUnicast;
    public uint RequestCanId { get; set; } = 0x7E0;
    public uint ResponseCanId { get; set; } = 0x7E8;

    public bool SupportsIsoTp { get; set; } = true;
    public bool SupportsCanFd { get; set; } = false;
    public int MaxPduSizeBytes { get; set; } = 4095; // ISO-TP Standard maximum

    // Standard Timing Constraints (ISO 14229 / ISO 15765-2)
    public double TimingP2ClientMs { get; set; } = 50.0;    // Default server response timeout
    public double TimingP2StarClientMs { get; set; } = 5000.0; // Enhanced response pending timeout
    public double TimingP3ClientMs { get; set; } = 2000.0;  // Periodic tester present interval

    // Diagnostic Sessions Supported
    public List<string> SupportedDiagnosticSessions { get; set; } = new() { "Default (0x01)", "Extended Diagnostic (0x03)" };
    public bool SecurityAccessRequiredForCalibrations { get; set; } = true;

    // Detailed Service Support Matrix
    public Dictionary<byte, ServiceCapability> Services { get; set; } = new();
    public DateTime DiscoveryTimestampUtc { get; set; } = DateTime.UtcNow;
}
