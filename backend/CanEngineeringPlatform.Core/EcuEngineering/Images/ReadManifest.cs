using System;

namespace CanEngineeringPlatform.Core.EcuEngineering.Images;

/// <summary>
/// Machine-readable manifest serializable to JSON conforming to Section 16 specification.
/// </summary>
public class ReadManifest
{
    public string Ecu { get; set; } = "UNKNOWN";
    public string HardwareNumber { get; set; } = "UNKNOWN";
    public string SoftwareNumber { get; set; } = "UNKNOWN";
    public string CalibrationId { get; set; } = "UNKNOWN";
    public string Protocol { get; set; } = "UNKNOWN";
    public string Addressing { get; set; } = "UNKNOWN";
    public string MemoryRegion { get; set; } = "UNKNOWN";
    public string StartAddress { get; set; } = "0x00000000";
    public long Length { get; set; }
    public string ReadMethod { get; set; } = "UNKNOWN";
    public string AuthorizationStatus { get; set; } = "UNKNOWN";
    public string Sha256 { get; set; } = string.Empty;
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");
    public string ToolVersion { get; set; } = "v5.2.0 (Phase 2)";

    public static ReadManifest FromImage(EcuMemoryImage image)
    {
        return new ReadManifest
        {
            Ecu = image.EcuName,
            HardwareNumber = image.HardwareNumber,
            SoftwareNumber = image.SoftwareNumber,
            CalibrationId = image.CalibrationId,
            Protocol = image.DiagnosticProtocol,
            Addressing = "11-bit Physical Unicast",
            MemoryRegion = image.RegionName,
            StartAddress = $"0x{image.StartAddress:X8}",
            Length = image.Length,
            ReadMethod = image.ReadMethod,
            AuthorizationStatus = image.AuthorizationState,
            Sha256 = image.Sha256Hash,
            Timestamp = image.TimestampUtc.ToString("o"),
            ToolVersion = image.ToolVersion
        };
    }
}
