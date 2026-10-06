using System;
using CanEngineeringPlatform.Core.EcuEngineering.Core;
using CanEngineeringPlatform.Core.EcuEngineering.Memory;

namespace CanEngineeringPlatform.Core.EcuEngineering.Analysis;

/// <summary>
/// Detected memory block showing mathematical characteristics of a calibration table or map.
/// </summary>
public class PotentialCalibrationRegion
{
    public long StartOffset { get; set; }
    public long EndOffset { get; set; }
    public long LengthBytes => EndOffset >= StartOffset ? (EndOffset - StartOffset + 1) : 0;
    public IdentificationConfidence Confidence { get; set; } = IdentificationConfidence.Detected;
    public string Evidence { get; set; } = string.Empty;
    public double Entropy { get; set; }
    public string PatternDescription { get; set; } = string.Empty;
    public int PotentialAxesCount { get; set; }
    public int PotentialTablesCount { get; set; }
}

/// <summary>
/// Detected checksum block or signature candidate.
/// </summary>
public class ChecksumCandidateRegion
{
    public long Offset { get; set; }
    public int LengthBytes { get; set; } = 4;
    public string AlgorithmName { get; set; } = "Bosch 16-bit complement / CRC32";
    public ChecksumClassification Classification { get; set; } = ChecksumClassification.Candidate;
    public string CurrentValueHex { get; set; } = string.Empty;
    public string CalculatedValueHex { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string Description { get; set; } = string.Empty;
}
