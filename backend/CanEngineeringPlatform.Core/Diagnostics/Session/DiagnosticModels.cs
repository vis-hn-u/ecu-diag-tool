using System;

namespace CanEngineeringPlatform.Core.Diagnostics.Session;

public class DiagnosticExchange
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? VehicleProfileId { get; set; }
    public string? SessionId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public uint TimestampUs { get; set; }
    public string EcuName { get; set; } = "ECM";
    public uint RequestCanId { get; set; } = 0x7E0;
    public uint ResponseCanId { get; set; } = 0x7E8;
    public string Protocol { get; set; } = "UDS"; // "UDS" or "OBD-II"
    public byte ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public byte? SubFunction { get; set; }
    public ushort? DataIdentifier { get; set; }
    public string RequestDataHex { get; set; } = string.Empty;
    public string? ResponseDataHex { get; set; }
    public bool IsComplete { get; set; }
    public bool IsSuccess { get; set; }
    public string? NrcHex { get; set; }
    public string? NrcDescription { get; set; }
    public string Summary { get; set; } = string.Empty;
    public double ResponseTimeMs { get; set; }
}

public class DiagnosticTargetEcu
{
    public string Name { get; set; } = "ECM";
    public uint RequestCanId { get; set; } = 0x7E0;
    public uint ResponseCanId { get; set; } = 0x7E8;
    public string Protocol { get; set; } = "UDS";
    public string Description { get; set; } = "Engine Control Module";
}
