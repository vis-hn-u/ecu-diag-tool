using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using CanEngineeringPlatform.Core.EcuEngineering.Identification;
using CanEngineeringPlatform.Core.EcuEngineering.Memory;

namespace CanEngineeringPlatform.Core.EcuEngineering.Images;

/// <summary>
/// Immutable, verified container of ECU memory bytes extracted via safe diagnostic reads.
/// Acts as the authoritative bridge to Flash Analyzer, Calibration Engine, and Checksum Engine.
/// </summary>
public class EcuMemoryImage
{
    public string ImageId { get; set; } = $"IMG-{Guid.NewGuid():N}";
    public string EcuName { get; set; } = "ECM";
    public string HardwareNumber { get; set; } = "UNKNOWN";
    public string SoftwareNumber { get; set; } = "UNKNOWN";
    public string CalibrationId { get; set; } = "UNKNOWN";
    public string Vin { get; set; } = "UNKNOWN";

    public string RegionName { get; set; } = "Internal Flash";
    public long StartAddress { get; set; }
    public long Length { get; set; }

    [JsonIgnore]
    public byte[] RawBytes { get; set; } = Array.Empty<byte>();

    public string Sha256Hash { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public string Transport { get; set; } = "USB Serial / CAN";
    public string DiagnosticProtocol { get; set; } = "UDS on ISO-TP (ISO 14229)";
    public string ReadMethod { get; set; } = "ReadMemoryByAddress (0x23)";
    public string DiagnosticSession { get; set; } = "Extended Diagnostic Session (0x03)";
    public string AuthorizationState { get; set; } = "User Authorized";
    public ImageClassification Classification { get; set; } = ImageClassification.OriginalRead;
    public ExecutionEnvironment Environment { get; set; } = ExecutionEnvironment.Simulation;
    public string ToolVersion { get; set; } = "v5.2.0 (Phase 2)";

    public List<EvidenceSource> Evidence { get; set; } = new();

    public static string ComputeSha256(byte[] data)
    {
        if (data == null || data.Length == 0) return string.Empty;
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(data);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
