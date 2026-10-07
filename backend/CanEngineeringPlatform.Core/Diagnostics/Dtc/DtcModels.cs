using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Diagnostics.Dtc;

[Flags]
public enum DtcStatusMask : byte
{
    None = 0x00,
    TestFailed = 0x01,
    TestFailedThisOperationCycle = 0x02,
    PendingDTC = 0x04,
    ConfirmedDTC = 0x08,
    TestNotCompletedSinceLastClear = 0x10,
    TestFailedSinceLastClear = 0x20,
    TestNotCompletedThisOperationCycle = 0x40,
    WarningIndicatorRequested = 0x80
}

public class DiagnosticTroubleCode
{
    public string Code { get; set; } = string.Empty;
    public uint RawDtcNumber { get; set; }
    public byte StatusByte { get; set; }
    public DtcStatusMask StatusMask => (DtcStatusMask)StatusByte;
    public string Description { get; set; } = string.Empty;
    public string EcuName { get; set; } = string.Empty;
    public uint CanId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    // Status helpers
    public bool IsConfirmed => (StatusByte & 0x08) != 0;
    public bool IsPending => (StatusByte & 0x04) != 0;
    public bool IsActive => (StatusByte & 0x01) != 0;
    public bool IsMilRequested => (StatusByte & 0x80) != 0;
    public bool IsFailedThisCycle => (StatusByte & 0x02) != 0;
    public bool IsFailedSinceClear => (StatusByte & 0x20) != 0;
    public bool IsTestNotCompleted => (StatusByte & 0x10) != 0 || (StatusByte & 0x40) != 0;

    public string RawEvidenceHex => $"DTC:0x{RawDtcNumber:X6} Status:0x{StatusByte:X2} (CAN:0x{CanId:X3})";

    public List<string> ActiveStatusFlags
    {
        get
        {
            var flags = new List<string>();
            if (IsActive) flags.Add("Active / Test Failed");
            if (IsFailedThisCycle) flags.Add("Failed This Cycle");
            if (IsPending) flags.Add("Pending");
            if (IsConfirmed) flags.Add("Confirmed");
            if (IsFailedSinceClear) flags.Add("Failed Since Clear");
            if (IsTestNotCompleted) flags.Add("Test Not Completed");
            if (IsMilRequested) flags.Add("MIL Warning Requested");
            return flags;
        }
    }
}

public enum DtcClearStatus
{
    Cleared,
    StillPresent,
    Permanent,
    Refused,
    SessionRequired,
    SecurityRequired,
    ConditionsNotCorrect,
    Timeout,
    NotSupported
}

public class DtcClearVerificationResult
{
    public string EcuName { get; set; } = "ECM";
    public uint RequestCanId { get; set; } = 0x7E0;
    public DtcClearStatus Status { get; set; } = DtcClearStatus.Cleared;
    public string Message { get; set; } = string.Empty;
    public int PreClearDtcCount { get; set; }
    public int PostClearDtcCount { get; set; }
    public List<string> PreClearCodes { get; set; } = new();
    public List<string> PostClearRemainingCodes { get; set; } = new();
    public string? RawClearResponseHex { get; set; }
    public bool IsVerifiedCleared => Status == DtcClearStatus.Cleared && PostClearDtcCount == 0;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
