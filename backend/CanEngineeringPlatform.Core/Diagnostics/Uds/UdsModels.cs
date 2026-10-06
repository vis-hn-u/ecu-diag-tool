using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Diagnostics.Uds;

public enum UdsServiceId : byte
{
    DiagnosticSessionControl = 0x10,
    EcuReset = 0x11,
    ClearDiagnosticInformation = 0x14,
    ReadDTCInformation = 0x19,
    ReadDataByIdentifier = 0x22,
    ReadMemoryByAddress = 0x23,
    SecurityAccess = 0x27,
    WriteDataByIdentifier = 0x2E,
    RoutineControl = 0x31,
    TesterPresent = 0x3E,
    ControlDTCSetting = 0x85,
    NegativeResponse = 0x7F
}

public enum NegativeResponseCode : byte
{
    GeneralReject = 0x10,
    ServiceNotSupported = 0x11,
    SubFunctionNotSupported = 0x12,
    IncorrectMessageLengthOrInvalidFormat = 0x13,
    ResponseTooLong = 0x14,
    BusyRepeatRequest = 0x21,
    ConditionsNotCorrect = 0x22,
    RequestSequenceError = 0x24,
    NoResponseFromSubnetComponent = 0x25,
    FailurePreventsExecutionOfRequestedAction = 0x26,
    RequestOutOfRange = 0x31,
    SecurityAccessDenied = 0x33,
    InvalidKey = 0x35,
    ExceedNumberOfAttempts = 0x36,
    RequiredTimeDelayNotExpired = 0x37,
    UploadDownloadNotAccepted = 0x70,
    TransferDataSuspended = 0x71,
    GeneralProgrammingFailure = 0x72,
    WrongBlockSequenceCounter = 0x73,
    RequestCorrectlyReceivedResponsePending = 0x78,
    SubFunctionNotSupportedInActiveSession = 0x7E,
    ServiceNotSupportedInActiveSession = 0x7F,
    RpmTooHigh = 0x81,
    RpmTooLow = 0x82,
    EngineIsRunning = 0x83,
    EngineIsNotRunning = 0x84,
    EngineRunTimeTooLow = 0x85,
    TemperatureTooHigh = 0x86,
    TemperatureTooLow = 0x87,
    VehicleSpeedTooHigh = 0x88,
    VehicleSpeedTooLow = 0x89,
    ThrottlePedalTooHigh = 0x8A,
    ThrottlePedalTooLow = 0x8B,
    TransmissionRangeNotInNeutral = 0x8C,
    TransmissionRangeNotInGear = 0x8D,
    BrakeSwitchNotClosed = 0x8F,
    ShifterLeverNotInPark = 0x90,
    TorqueConverterClutchLocked = 0x91,
    VoltageTooHigh = 0x92,
    VoltageTooLow = 0x93
}

public static class NrcHelper
{
    public static string GetDescription(NegativeResponseCode nrc) => nrc switch
    {
        NegativeResponseCode.GeneralReject => "General Reject",
        NegativeResponseCode.ServiceNotSupported => "Service Not Supported",
        NegativeResponseCode.SubFunctionNotSupported => "Sub-Function Not Supported",
        NegativeResponseCode.IncorrectMessageLengthOrInvalidFormat => "Incorrect Message Length Or Invalid Format",
        NegativeResponseCode.ResponseTooLong => "Response Too Long",
        NegativeResponseCode.BusyRepeatRequest => "Busy - Repeat Request",
        NegativeResponseCode.ConditionsNotCorrect => "Conditions Not Correct",
        NegativeResponseCode.RequestSequenceError => "Request Sequence Error",
        NegativeResponseCode.RequestOutOfRange => "Request Out Of Range",
        NegativeResponseCode.SecurityAccessDenied => "Security Access Denied",
        NegativeResponseCode.InvalidKey => "Invalid Key",
        NegativeResponseCode.ExceedNumberOfAttempts => "Exceed Number Of Attempts",
        NegativeResponseCode.RequiredTimeDelayNotExpired => "Required Time Delay Not Expired",
        NegativeResponseCode.GeneralProgrammingFailure => "General Programming Failure",
        NegativeResponseCode.RequestCorrectlyReceivedResponsePending => "Response Pending (0x78)",
        NegativeResponseCode.SubFunctionNotSupportedInActiveSession => "Sub-Function Not Supported In Active Session",
        NegativeResponseCode.ServiceNotSupportedInActiveSession => "Service Not Supported In Active Session",
        NegativeResponseCode.RpmTooHigh => "Engine RPM Too High",
        NegativeResponseCode.VoltageTooHigh => "Battery Voltage Too High",
        NegativeResponseCode.VoltageTooLow => "Battery Voltage Too Low",
        _ => $"Unknown NRC (0x{(byte)nrc:X2})"
    };
}

public class UdsRequest
{
    public UdsServiceId ServiceId { get; set; }
    public byte? SubFunction { get; set; }
    public ushort? DataIdentifier { get; set; }
    public byte[] Payload { get; set; } = Array.Empty<byte>();
    public string ServiceName => ServiceId.ToString();
}

public class UdsResponse
{
    public bool IsPositive { get; set; }
    public UdsServiceId RequestedServiceId { get; set; }
    public byte ResponseSid { get; set; }
    public NegativeResponseCode? Nrc { get; set; }
    public string? NrcDescription => Nrc.HasValue ? NrcHelper.GetDescription(Nrc.Value) : null;
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public ushort? DataIdentifier { get; set; }
    public string? DecodedSummary { get; set; }
}
