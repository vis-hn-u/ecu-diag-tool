using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.EcuEngineering.Core;

namespace CanEngineeringPlatform.Core.EcuEngineering.Identification;

/// <summary>
/// Detailed citation tracking the exact origin of a decoded or inferred field.
/// </summary>
public class EvidenceSource
{
    public string SourceType { get; set; } = "DiagnosticService"; // "UdsDid", "ObdMode9", "Kwp2000", "CanFrame", "OemDatabase", "FlashHeader"
    public string ReferenceCode { get; set; } = string.Empty;     // e.g. "0xF190", "Mode 0x09 PID 0x02", "0x7E8"
    public string? RawBytesHex { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public static EvidenceSource Create(string sourceType, string referenceCode, string description, string? rawBytesHex = null)
    {
        return new EvidenceSource
        {
            SourceType = sourceType,
            ReferenceCode = referenceCode,
            Description = description,
            RawBytesHex = rawBytesHex,
            TimestampUtc = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Generic container binding a strongly-typed value to its confidence classification and supporting evidence citations.
/// </summary>
/// <typeparam name="T">Field data type</typeparam>
public class FieldWithConfidence<T>
{
    public T? Value { get; set; }
    public IdentificationConfidence Confidence { get; set; } = IdentificationConfidence.Unknown;
    public List<EvidenceSource> Citations { get; set; } = new();

    public FieldWithConfidence() { }

    public FieldWithConfidence(T? value, IdentificationConfidence confidence, EvidenceSource? primaryEvidence = null)
    {
        Value = value;
        Confidence = confidence;
        if (primaryEvidence != null)
        {
            Citations.Add(primaryEvidence);
        }
    }

    public static FieldWithConfidence<T> Confirmed(T value, EvidenceSource evidence) =>
        new(value, IdentificationConfidence.Confirmed, evidence);

    public static FieldWithConfidence<T> Detected(T value, EvidenceSource evidence) =>
        new(value, IdentificationConfidence.Detected, evidence);

    public static FieldWithConfidence<T> Inferred(T value, EvidenceSource evidence) =>
        new(value, IdentificationConfidence.Inferred, evidence);

    public static FieldWithConfidence<T> Unknown(string reason = "No data received") =>
        new(default, IdentificationConfidence.Unknown, EvidenceSource.Create("NotAvailable", "N/A", reason));
}
