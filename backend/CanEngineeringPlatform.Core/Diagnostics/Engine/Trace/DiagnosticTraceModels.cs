using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Diagnostics.Uds;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Trace;

public enum DiagnosticDirection
{
    Tx,
    Rx
}

public enum DiagnosticProtocolType
{
    Uds,
    Obd2,
    Kwp2000,
    J1939,
    RawCan
}

public enum DiagnosticExchangeResult
{
    PositiveResponse,
    NegativeResponse,
    Timeout,
    TransportError,
    Pending
}

/// <summary>
/// Immutable record of a single diagnostic communication exchange on the CAN/ISO-TP bus.
/// </summary>
public record DiagnosticTraceEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string TransportId { get; init; } = string.Empty;
    public uint RequestCanId { get; init; }
    public uint ResponseCanId { get; init; }
    public DiagnosticDirection Direction { get; init; }
    public DiagnosticProtocolType Protocol { get; init; }
    public byte ServiceId { get; init; }
    public byte? SubFunction { get; init; }
    public ushort? DataIdentifier { get; init; }
    public byte[] Payload { get; init; } = Array.Empty<byte>();
    public string FormattedHex => BitConverter.ToString(Payload).Replace("-", " ");
    public DiagnosticExchangeResult Result { get; init; } = DiagnosticExchangeResult.PositiveResponse;
    public byte? NegativeResponseCode { get; init; }
    public string? NrcDescription { get; init; }
    public NrcRetryBehavior RetryClassification { get; init; } = NrcRetryBehavior.Unknown;
    public double LatencyMs { get; init; }
    public DiagnosticSessionState ActiveSession { get; init; }
    public string? Notes { get; init; }
}

public record NrcEvaluation
{
    public byte NrcCode { get; init; }
    public string Meaning { get; init; } = string.Empty;
    public NrcRetryBehavior RetryBehavior { get; init; }
    public string PossibleExplanation { get; init; } = string.Empty;
    public string RecommendedDiagnosticAction { get; init; } = string.Empty;
}

public static class NrcClassifier
{
    public static NrcEvaluation Evaluate(byte nrc)
    {
        var code = (NegativeResponseCode)nrc;
        return code switch
        {
            NegativeResponseCode.GeneralReject => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "General Reject",
                RetryBehavior = NrcRetryBehavior.DoNotRetry,
                PossibleExplanation = "The ECU rejected the request without providing a specific reason.",
                RecommendedDiagnosticAction = "Check vehicle ignition state, battery voltage, and verify service prerequisites."
            },
            NegativeResponseCode.ServiceNotSupported => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Service Not Supported",
                RetryBehavior = NrcRetryBehavior.DoNotRetry,
                PossibleExplanation = "The requested UDS service is not implemented in this ECU's software.",
                RecommendedDiagnosticAction = "Consult ECU documentation; do not repeatedly invoke unsupported services."
            },
            NegativeResponseCode.SubFunctionNotSupported => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Sub-Function Not Supported",
                RetryBehavior = NrcRetryBehavior.DoNotRetry,
                PossibleExplanation = "The requested sub-function is invalid for this service.",
                RecommendedDiagnosticAction = "Verify sub-function parameter with ECU specifications."
            },
            NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Incorrect Message Length Or Invalid Format",
                RetryBehavior = NrcRetryBehavior.DoNotRetry,
                PossibleExplanation = "Payload byte count does not match ISO 14229 definition for this service.",
                RecommendedDiagnosticAction = "Review request framing and length parameters."
            },
            NegativeResponseCode.BusyRepeatRequest => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Busy - Repeat Request",
                RetryBehavior = NrcRetryBehavior.SafeToRetry,
                PossibleExplanation = "The ECU is executing a high-priority background task (e.g. flash erase, NVRAM write).",
                RecommendedDiagnosticAction = "Wait P2* timeout period and re-attempt the request."
            },
            NegativeResponseCode.ConditionsNotCorrect => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Conditions Not Correct",
                RetryBehavior = NrcRetryBehavior.RequiresUserAction,
                PossibleExplanation = "Environmental pre-conditions not met (e.g. engine running during actuator test, RPM > 0, gear not in park).",
                RecommendedDiagnosticAction = "Ensure vehicle meets test criteria (e.g. Key ON Engine OFF, parking brake set)."
            },
            NegativeResponseCode.RequestSequenceError => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Request Sequence Error",
                RetryBehavior = NrcRetryBehavior.RequiresUserAction,
                PossibleExplanation = "A prerequisite step in the diagnostic sequence was skipped.",
                RecommendedDiagnosticAction = "Re-establish diagnostic session or perform initial security unlock before retrying."
            },
            NegativeResponseCode.RequestOutOfRange => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Request Out Of Range",
                RetryBehavior = NrcRetryBehavior.DoNotRetry,
                PossibleExplanation = "Target Data Identifier (DID), memory address, or routine parameter is outside valid bounds.",
                RecommendedDiagnosticAction = "Validate address boundaries against discovered ECU memory map."
            },
            NegativeResponseCode.SecurityAccessDenied => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Security Access Denied",
                RetryBehavior = NrcRetryBehavior.RequiresUserAction,
                PossibleExplanation = "The requested service/DID requires an authorized security session (UDS 0x27).",
                RecommendedDiagnosticAction = "Obtain legitimate authorization credentials. Brute force cracking is strictly prohibited."
            },
            NegativeResponseCode.InvalidKey => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Invalid Key",
                RetryBehavior = NrcRetryBehavior.DoNotRetry,
                PossibleExplanation = "Calculated security key rejected by ECU security algorithm.",
                RecommendedDiagnosticAction = "Verify security algorithm version and credentials."
            },
            NegativeResponseCode.ExceedNumberOfAttempts => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Exceed Number Of Attempts",
                RetryBehavior = NrcRetryBehavior.RequiresUserAction,
                PossibleExplanation = "Maximum failed security attempts reached. Security module is temporarily locked.",
                RecommendedDiagnosticAction = "Wait for timeout delay period (typically 10 to 60 minutes) with ignition active."
            },
            NegativeResponseCode.RequiredTimeDelayNotExpired => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Required Time Delay Not Expired",
                RetryBehavior = NrcRetryBehavior.RequiresUserAction,
                PossibleExplanation = "Security lockout timer has not elapsed.",
                RecommendedDiagnosticAction = "Keep ECU powered until lockout timer expires."
            },
            NegativeResponseCode.UploadDownloadNotAccepted => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Upload/Download Not Accepted (0x70)",
                RetryBehavior = NrcRetryBehavior.DoNotRetry,
                PossibleExplanation = "The ECU rejected memory transfer operation.",
                RecommendedDiagnosticAction = "Check memory block address, length, and programming session prerequisites."
            },
            NegativeResponseCode.TransferDataSuspended => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Transfer Data Suspended (0x71)",
                RetryBehavior = NrcRetryBehavior.RequiresUserAction,
                PossibleExplanation = "Data transfer was interrupted.",
                RecommendedDiagnosticAction = "Re-initiate data transfer sequence."
            },
            NegativeResponseCode.GeneralProgrammingFailure => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "General Programming Failure (0x72)",
                RetryBehavior = NrcRetryBehavior.DoNotRetry,
                PossibleExplanation = "Flash memory write/erase failure.",
                RecommendedDiagnosticAction = "Check ECU supply voltage and memory integrity."
            },
            NegativeResponseCode.WrongBlockSequenceCounter => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Wrong Block Sequence Counter (0x73)",
                RetryBehavior = NrcRetryBehavior.RequiresUserAction,
                PossibleExplanation = "Block transfer sequence counter mismatch.",
                RecommendedDiagnosticAction = "Resend the block with the expected block counter."
            },
            NegativeResponseCode.RequestCorrectlyReceivedResponsePending => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Request Correctly Received - Response Pending (0x78)",
                RetryBehavior = NrcRetryBehavior.SafeToRetry,
                PossibleExplanation = "ECU is processing a long-running operation; extended P2* timer started.",
                RecommendedDiagnosticAction = "Continue waiting for the final response. Reset P2* watchdog timer."
            },
            NegativeResponseCode.SubFunctionNotSupportedInActiveSession => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Sub-Function Not Supported In Active Session",
                RetryBehavior = NrcRetryBehavior.RequiresUserAction,
                PossibleExplanation = "Active diagnostic session does not permit this sub-function.",
                RecommendedDiagnosticAction = "Transition to Extended Diagnostic Session (0x03) before retrying."
            },
            NegativeResponseCode.ServiceNotSupportedInActiveSession => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Service Not Supported In Active Session",
                RetryBehavior = NrcRetryBehavior.RequiresUserAction,
                PossibleExplanation = "Active diagnostic session does not permit this service.",
                RecommendedDiagnosticAction = "Transition to Extended Diagnostic Session (0x03) or Programming Session (0x02)."
            },
            NegativeResponseCode.VoltageTooHigh => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Battery Voltage Too High",
                RetryBehavior = NrcRetryBehavior.RequiresUserAction,
                PossibleExplanation = "Bus voltage exceeds safe operating thresholds (> 16V).",
                RecommendedDiagnosticAction = "Disconnect high-voltage battery charger or check alternator voltage regulator."
            },
            NegativeResponseCode.VoltageTooLow => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = "Battery Voltage Too Low",
                RetryBehavior = NrcRetryBehavior.RequiresUserAction,
                PossibleExplanation = "Bus voltage dropped below minimum threshold (< 11.5V).",
                RecommendedDiagnosticAction = "Connect regulated 13.8V clean power supply before proceeding with diagnostic operations."
            },
            _ => new NrcEvaluation
            {
                NrcCode = nrc,
                Meaning = $"Unknown NRC (0x{nrc:X2})",
                RetryBehavior = NrcRetryBehavior.Unknown,
                PossibleExplanation = "Unrecognized negative response code reported by ECU.",
                RecommendedDiagnosticAction = "Verify ECU manufacturer documentation."
            }
        };
    }
}
