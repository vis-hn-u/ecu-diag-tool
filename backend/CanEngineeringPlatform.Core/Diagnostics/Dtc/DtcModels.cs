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

    public List<string> ActiveStatusFlags
    {
        get
        {
            var flags = new List<string>();
            if (IsActive) flags.Add("Test Failed");
            if (IsPending) flags.Add("Pending");
            if (IsConfirmed) flags.Add("Confirmed");
            if (IsMilRequested) flags.Add("MIL Warning");
            if ((StatusByte & 0x20) != 0) flags.Add("Failed Since Clear");
            return flags;
        }
    }
}
