using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.FlashAnalysis;

public enum ObjectCategory
{
    Torque,
    AirTurbo,
    Fuel,
    SmokeLambda,
    RpmSpeed,
    Thermal,
    Egr,
    Dpf,
    ScrAdBlue,
    IntakeActuators,
    StartIdle,
    Sensors,
    General
}

public enum ObjectProvenance
{
    Confirmed,
    ProjectDefined,
    UserDefined,
    Heuristic,
    Unknown
}

public class CalibrationObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ObjectCategory Category { get; set; } = ObjectCategory.General;
    public string ObjectType { get; set; } = "2D Map"; // "1D Curve", "2D Map", "3D Map", "Scalar", "Limiter"
    public long FileOffset { get; set; }
    public long? LogicalAddress { get; set; }
    public int SizeBytes { get; set; }
    public string DataType { get; set; } = "uint16_le";
    public double ScalingFactor { get; set; } = 1.0;
    public double Offset { get; set; } = 0.0;
    public string Unit { get; set; } = string.Empty;
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double[] XBreakpoints { get; set; } = Array.Empty<double>();
    public double[]? YBreakpoints { get; set; }
    public double[][] Values { get; set; } = Array.Empty<double[]>();
    public ObjectProvenance Provenance { get; set; } = ObjectProvenance.Confirmed;
}

public class EcuBinaryInfo
{
    public string FileId { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public string Md5Hash { get; set; } = string.Empty;
    public string DetectedEcuFamily { get; set; } = "Unknown ECU";
    public string SoftwareNumber { get; set; } = string.Empty;
    public string HardwareNumber { get; set; } = string.Empty;
    public DateTime ImportedUtc { get; set; } = DateTime.UtcNow;
    public List<CalibrationObject> Objects { get; set; } = new();
    public List<ChecksumInfo> Checksums { get; set; } = new();
}

public class BinaryDiffChunk
{
    public long Offset { get; set; }
    public int Length { get; set; }
    public string HexOriginal { get; set; } = string.Empty;
    public string HexModified { get; set; } = string.Empty;
}

public class BinaryDiffResult
{
    public string OriginalFileName { get; set; } = string.Empty;
    public string ModifiedFileName { get; set; } = string.Empty;
    public long OriginalSizeBytes { get; set; }
    public long ModifiedSizeBytes { get; set; }
    public int TotalDifferentBytes { get; set; }
    public double DifferencePercentage { get; set; }
    public List<BinaryDiffChunk> Chunks { get; set; } = new();
}

public class ChecksumInfo
{
    public string RegionName { get; set; } = "Main Flash Block";
    public string Algorithm { get; set; } = "CRC32";
    public long StartOffset { get; set; }
    public long EndOffset { get; set; }
    public string ExpectedChecksumHex { get; set; } = string.Empty;
    public string CalculatedChecksumHex { get; set; } = string.Empty;
    public bool IsValid { get; set; } = true;
}

public class LiveFlashCorrelation
{
    public string Id { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public string CalibrationCategory { get; set; } = string.Empty;
    public double CalibratedLimit { get; set; }
    public string LiveSignalName { get; set; } = string.Empty;
    public double LiveObservedValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double MarginDelta => Math.Round(CalibratedLimit - LiveObservedValue, 2);
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
