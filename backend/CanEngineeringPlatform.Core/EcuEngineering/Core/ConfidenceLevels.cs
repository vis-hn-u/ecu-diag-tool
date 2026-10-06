using System;

namespace CanEngineeringPlatform.Core.EcuEngineering.Core;

/// <summary>
/// Strict evidence-backed confidence state for all ECU identification and engineering fields.
/// Never fabricate or assume information.
/// </summary>
public enum IdentificationConfidence
{
    /// <summary>
    /// Confirmed directly via official diagnostic service or verified read (e.g. UDS DID, OBD-II Mode 9).
    /// </summary>
    Confirmed,

    /// <summary>
    /// Detected from network communication, broadcast frames, or unambiguous bus patterns.
    /// </summary>
    Detected,

    /// <summary>
    /// Inferred using OEM database correlation, hardware part number heuristics, or architectural signatures.
    /// </summary>
    Inferred,

    /// <summary>
    /// Unknown or unsupported; no evidence exists to determine this field.
    /// </summary>
    Unknown
}

/// <summary>
/// Calibration and memory validation status.
/// </summary>
public enum ValidationStatus
{
    NotValidated,
    StructurallyValid,
    EngineeringallyValidated,
    Failed
}

/// <summary>
/// Diagnostic and programming authorization states.
/// </summary>
public enum AuthorizationStatus
{
    Authorized,
    RequiresAuthorization,
    Denied
}
