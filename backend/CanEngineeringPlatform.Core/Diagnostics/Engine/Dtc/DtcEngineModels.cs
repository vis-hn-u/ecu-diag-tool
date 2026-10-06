using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Dtc;

/// <summary>
/// Comprehensive ISO 14229-1 DTC record with 8-bit status flags and evidentiary tracking.
/// </summary>
public record DtcRecord
{
    public string Code { get; init; } = string.Empty;
    public uint RawDtcNumber { get; init; }
    public byte StatusByte { get; init; }
    public string Description { get; init; } = string.Empty;
    public string DescriptionSource { get; init; } = "SAE J2012 Standard Database";
    public string EcuName { get; init; } = "ECM";
    public uint CanId { get; init; }
    public DateTime DetectedUtc { get; init; } = DateTime.UtcNow;
    public DiagnosticSessionState SessionWhenRead { get; init; } = DiagnosticSessionState.DefaultSession;
    public byte[] RawResponseBytes { get; init; } = Array.Empty<byte>();

    // ISO 14229-1 / SAE J1979 8-bit DTC status mask indicators
    public bool TestFailed => (StatusByte & 0x01) != 0;
    public bool TestFailedThisOperationCycle => (StatusByte & 0x02) != 0;
    public bool PendingDtc => (StatusByte & 0x04) != 0;
    public bool ConfirmedDtc => (StatusByte & 0x08) != 0;
    public bool TestNotCompletedSinceLastClear => (StatusByte & 0x10) != 0;
    public bool TestFailedSinceLastClear => (StatusByte & 0x20) != 0;
    public bool TestNotCompletedThisOperationCycle => (StatusByte & 0x40) != 0;
    public bool WarningIndicatorRequested => (StatusByte & 0x80) != 0;

    public int? FaultOccurrenceCounter { get; init; }
    public int? AgingCounter { get; init; }
    public List<DtcSnapshotRecord> Snapshots { get; init; } = new();
    public List<DtcExtendedDataRecord> ExtendedData { get; init; } = new();

    public IReadOnlyList<string> FormattedFlags
    {
        get
        {
            var flags = new List<string>();
            if (TestFailed) flags.Add("TEST_FAILED (0x01)");
            if (TestFailedThisOperationCycle) flags.Add("TEST_FAILED_THIS_CYCLE (0x02)");
            if (PendingDtc) flags.Add("PENDING (0x04)");
            if (ConfirmedDtc) flags.Add("CONFIRMED (0x08)");
            if (TestNotCompletedSinceLastClear) flags.Add("TEST_NOT_COMPLETED_SINCE_CLEAR (0x10)");
            if (TestFailedSinceLastClear) flags.Add("TEST_FAILED_SINCE_CLEAR (0x20)");
            if (TestNotCompletedThisOperationCycle) flags.Add("TEST_NOT_COMPLETED_THIS_CYCLE (0x40)");
            if (WarningIndicatorRequested) flags.Add("WARNING_INDICATOR_REQUESTED (0x80)");
            return flags;
        }
    }
}

public record DtcSnapshotRecord
{
    public byte RecordNumber { get; init; }
    public ushort DataIdentifier { get; init; }
    public string ParameterName { get; init; } = string.Empty;
    public byte[] RawData { get; init; } = Array.Empty<byte>();
    public string FormattedValue { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
}

public record DtcExtendedDataRecord
{
    public byte RecordNumber { get; init; }
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public string Description { get; init; } = string.Empty;
}

public class DtcClearRequest
{
    public string EcuName { get; set; } = "ECM";
    public uint RequestCanId { get; set; } = 0x7E0;
    public uint GroupOfDtc { get; set; } = 0xFFFFFF; // All DTCs
    public bool RequireUserConfirmation { get; set; } = true;
    public bool UserConfirmed { get; set; } = false;
    public string ConfirmationReason { get; set; } = string.Empty;
}

public record DtcClearResult
{
    public string OperationId { get; init; } = Guid.NewGuid().ToString("N");
    public string EcuName { get; init; } = string.Empty;
    public DateTime ClearedUtc { get; init; } = DateTime.UtcNow;
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<DtcRecord> PreClearDtcs { get; init; } = Array.Empty<DtcRecord>();
    public IReadOnlyList<DtcRecord> PostClearDtcs { get; init; } = Array.Empty<DtcRecord>();
    public IReadOnlyList<string> ClearedCodes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ReturningCodes { get; init; } = Array.Empty<string>();
}
