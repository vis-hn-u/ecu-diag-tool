using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.EcuEngineering.Memory;

namespace CanEngineeringPlatform.Core.EcuEngineering.Reading;

public record StateTransitionRecord(ReadSessionState State, DateTime TimestampUtc, string Message);

/// <summary>
/// Real-time tracking and audit log of a safe ECU memory read session.
/// </summary>
public class ReadSessionStatus
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public string EcuName { get; set; } = "ECM";
    public uint RequestCanId { get; set; } = 0x7E0;
    public uint ResponseCanId { get; set; } = 0x7E8;
    public string RegionName { get; set; } = string.Empty;

    public long StartAddress { get; set; }
    public long TotalBytesRequested { get; set; }
    public long BytesReadSoFar { get; set; }
    public double ProgressPercent => TotalBytesRequested > 0 ? Math.Round((double)BytesReadSoFar / TotalBytesRequested * 100.0, 1) : 0.0;

    public int CurrentChunk { get; set; }
    public int TotalChunks { get; set; }

    public ReadSessionState State { get; set; } = ReadSessionState.Disconnected;
    public List<StateTransitionRecord> StateTransitions { get; set; } = new();

    public string? LastError { get; set; }
    public string? Sha256Result { get; set; }
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }

    public void TransitionTo(ReadSessionState nextState, string message)
    {
        // Strict state machine validation: prevent invalid transitions
        if (!IsValidTransition(State, nextState))
        {
            throw new InvalidOperationException($"Invalid state machine transition from {State} to {nextState}: {message}");
        }

        State = nextState;
        StateTransitions.Add(new StateTransitionRecord(nextState, DateTime.UtcNow, message));
    }

    public static bool IsValidTransition(ReadSessionState from, ReadSessionState to)
    {
        // Always allow transition to Failed
        if (to == ReadSessionState.Failed) return true;

        return from switch
        {
            ReadSessionState.Disconnected => to == ReadSessionState.Connected,
            ReadSessionState.Connected => to == ReadSessionState.Identified,
            ReadSessionState.Identified => to == ReadSessionState.ProtocolConfirmed,
            ReadSessionState.ProtocolConfirmed => to == ReadSessionState.DiagnosticSessionActive,
            ReadSessionState.DiagnosticSessionActive => to == ReadSessionState.ReadAuthorized,
            ReadSessionState.ReadAuthorized => to == ReadSessionState.Reading,
            ReadSessionState.Reading => to == ReadSessionState.Verifying || to == ReadSessionState.Reading, // chunk iterations
            ReadSessionState.Verifying => to == ReadSessionState.Complete,
            ReadSessionState.Complete => to == ReadSessionState.Disconnected,
            ReadSessionState.Failed => to == ReadSessionState.Disconnected,
            _ => false
        };
    }
}
