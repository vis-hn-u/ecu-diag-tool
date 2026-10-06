using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Session;

public record SessionTransitionRecord
{
    public DiagnosticSessionState FromState { get; init; }
    public DiagnosticSessionState ToState { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string Reason { get; init; } = string.Empty;
}

public class DiagnosticSessionStatus
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public string EcuName { get; set; } = "ECM";
    public uint RequestCanId { get; set; } = 0x7E0;
    public uint ResponseCanId { get; set; } = 0x7E8;
    public DiagnosticSessionState CurrentState { get; set; } = DiagnosticSessionState.Disconnected;
    public DiagnosticSessionType? ActiveUdsSession { get; set; }
    public int P2ServerMs { get; set; } = 50;
    public int P2StarServerMs { get; set; } = 5000;
    public bool IsTesterPresentActive { get; set; }
    public int TesterPresentIntervalMs { get; set; } = 2000;
    public bool SuppressTesterPresentPositiveResponse { get; set; }
    public DateTime? LastCommunicationUtc { get; set; }
    public List<SessionTransitionRecord> History { get; set; } = new();
    public string? LastError { get; set; }
}
