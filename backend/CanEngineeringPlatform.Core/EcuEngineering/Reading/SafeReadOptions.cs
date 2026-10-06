using System;
using CanEngineeringPlatform.Core.EcuEngineering.Memory;

namespace CanEngineeringPlatform.Core.EcuEngineering.Reading;

/// <summary>
/// Configurable safety and execution parameters for ECU memory reads.
/// </summary>
public class SafeReadOptions
{
    public int ChunkSize { get; set; } = 256;              // Chunk size in bytes (protects ISO-TP PDU limits)
    public int TimeoutMs { get; set; } = 5000;             // Timeout per chunk in milliseconds
    public int InterChunkDelayMs { get; set; } = 10;       // Delay between consecutive chunk requests to prevent ECU buffer overrun
    public int MaxTotalReadBytes { get; set; } = 16777216; // 16 MB maximum single read limit to prevent memory exhaustion
    public int RetryCount { get; set; } = 3;               // Retry count per failed chunk
    public ExecutionEnvironment Environment { get; set; } = ExecutionEnvironment.Simulation;
    public bool RequireUserConfirmation { get; set; } = true;
    public bool UserConfirmed { get; set; } = false;
}
