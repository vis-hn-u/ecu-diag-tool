using System;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine;

/// <summary>
/// Diagnostic session types defined by ISO 14229-1 (UDS Service 0x10).
/// </summary>
public enum DiagnosticSessionType : byte
{
    DefaultSession = 0x01,
    ProgrammingSession = 0x02,
    ExtendedDiagnosticSession = 0x03,
    SafetySystemDiagnosticSession = 0x04
}

/// <summary>
/// Strict diagnostic session state machine states.
/// </summary>
public enum DiagnosticSessionState
{
    Disconnected,
    Connected,
    Identified,
    ProtocolConfirmed,
    DefaultSession,
    ExtendedSession,
    ProgrammingSession,
    Terminated,
    Faulted
}

/// <summary>
/// Support capability for specific diagnostic sessions or services.
/// </summary>
public enum SessionCapability
{
    Supported,
    NotSupported,
    Unknown,
    RequiresAuthorization
}

/// <summary>
/// Quality classification for live data parameters.
/// </summary>
public enum DataQuality
{
    Valid,
    Invalid,
    Stale,
    OutOfRange,
    Unavailable,
    Unknown
}

/// <summary>
/// Evidence-based sensor plausibility state.
/// </summary>
public enum SensorPlausibilityState
{
    Plausible,
    Questionable,
    OutOfRange,
    Inconsistent,
    InsufficientData,
    Unknown
}

/// <summary>
/// Evaluated ECU health state.
/// </summary>
public enum EcuHealthStatus
{
    Normal,
    Warning,
    Fault,
    Unknown
}

/// <summary>
/// Confidence levels for diagnostic root causes and evidence correlation.
/// </summary>
public enum EvidenceConfidence
{
    Confirmed,
    Likely,
    Possible,
    InsufficientData,
    Unknown
}

/// <summary>
/// Evidentiary provenance for OEM targets and specifications.
/// </summary>
public enum OemTargetSource
{
    OemDocumented,
    Measured,
    Calculated,
    Inferred,
    AiSuggestion
}

/// <summary>
/// Retry behavior classification for Negative Response Codes (NRCs).
/// </summary>
public enum NrcRetryBehavior
{
    SafeToRetry,
    RequiresUserAction,
    DoNotRetry,
    Unknown
}

/// <summary>
/// Execution lifecycle state for UDS RoutineControl (0x31).
/// </summary>
public enum RoutineExecutionState
{
    Ready,
    Running,
    Completed,
    Aborted,
    Failed
}

/// <summary>
/// Supported ECU reset types defined by ISO 14229-1 (UDS Service 0x11).
/// </summary>
public enum EcuResetType : byte
{
    HardReset = 0x01,
    KeyOffOnReset = 0x02,
    SoftReset = 0x03
}
