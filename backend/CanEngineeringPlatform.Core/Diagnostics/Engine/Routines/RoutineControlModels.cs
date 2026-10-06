using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Routines;

public record RoutineDescriptor
{
    public ushort RoutineId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DiagnosticSessionType RequiredSession { get; init; } = DiagnosticSessionType.ExtendedDiagnosticSession;
    public bool RequiresSecurityAccess { get; init; } = false;
    public List<string> SafetyPreconditions { get; init; } = new();
    public int ExpectedDurationSeconds { get; init; } = 10;
    public string RecoveryBehavior { get; init; } = "Automatic stop upon routine completion or tester abort.";
    public bool SupportsStop { get; init; } = true;
    public bool SupportsResultsRequest { get; init; } = true;
}

public class RoutineExecutionRequest
{
    public string EcuName { get; set; } = "ECM";
    public uint RequestCanId { get; set; } = 0x7E0;
    public ushort RoutineId { get; set; }
    public byte SubFunction { get; set; } = 0x01; // 0x01: StartRoutine, 0x02: StopRoutine, 0x03: RequestResults
    public byte[] OptionRecord { get; set; } = Array.Empty<byte>();
    public bool RequireUserConfirmation { get; set; } = true;
    public bool UserConfirmed { get; set; } = false;
    public string ConfirmationReason { get; set; } = string.Empty;
}

public record RoutineExecutionResult
{
    public string ExecutionId { get; init; } = Guid.NewGuid().ToString("N");
    public ushort RoutineId { get; init; }
    public string RoutineName { get; init; } = string.Empty;
    public byte SubFunction { get; init; }
    public bool Success { get; init; }
    public RoutineExecutionState State { get; init; } = RoutineExecutionState.Ready;
    public byte[] ResponsePayload { get; init; } = Array.Empty<byte>();
    public byte RoutineStatusRecord { get; init; }
    public string? ErrorMessage { get; init; }
    public double ExecutionLatencyMs { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}
