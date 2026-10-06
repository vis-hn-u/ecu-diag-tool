using System;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Reset;

public class EcuResetRequest
{
    public string EcuName { get; set; } = "ECM";
    public uint RequestCanId { get; set; } = 0x7E0;
    public EcuResetType ResetType { get; set; } = EcuResetType.SoftReset;
    public bool RequireUserConfirmation { get; set; } = true;
    public bool UserConfirmed { get; set; } = false;
    public string ConfirmationReason { get; set; } = string.Empty;
}

public record EcuResetResult
{
    public string ResetId { get; init; } = Guid.NewGuid().ToString("N");
    public string EcuName { get; init; } = "ECM";
    public EcuResetType ResetType { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int PostResetPowerDownDelaySeconds { get; init; }
    public bool CommunicationRestored { get; init; }
    public DateTime InitiatedUtc { get; init; } = DateTime.UtcNow;
    public DateTime? RestoredUtc { get; init; }
}
