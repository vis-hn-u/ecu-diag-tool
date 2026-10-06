using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.EcuEngineering.Core;

namespace CanEngineeringPlatform.Core.EcuEngineering.Abstractions;

public interface IMemoryManager
{
    Task<byte[]> ReadMemoryAddressAsync(long address, int length);
    Task<bool> WriteMemoryAddressAsync(long address, byte[] data);
}

public interface IFlashImageManager
{
    Task<string> CalculateSha256Async(byte[] imageBytes);
    Task<bool> CreateBackupAsync(string ecuIdentifier, byte[] imageBytes);
}

public interface ICalibrationManager
{
    ValidationStatus ValidateMonotonicity(double[] axisValues);
    ValidationStatus ValidateBoundaries(double value, double min, double max);
}

public interface IChecksumManager
{
    bool VerifyChecksum(byte[] imageBytes, string algorithm, out string calculated);
    byte[] RecalculateChecksum(byte[] imageBytes, string algorithm);
}

public enum ProgrammingState
{
    NotStarted,
    BackupComplete,
    Preparing,
    Erase,
    Programming,
    Verifying,
    Complete,
    Failed,
    RecoveryRequired
}

public interface IProgrammingManager
{
    ProgrammingState CurrentState { get; }
    Task<bool> PerformPreProgrammingSafetyCheckAsync();
}

public interface IVerificationManager
{
    Task<bool> VerifyTargetFlashAsync(byte[] expectedHash);
}

public interface IRecoveryManager
{
    bool IsRecoveryRequired { get; }
    IReadOnlyList<string> GetAvailableRecoveryProcedures(string ecuFamily);
}

public interface IEcuHealthManager
{
    Task<Dictionary<string, string>> EvaluateHealthAsync(string ecuName);
}
