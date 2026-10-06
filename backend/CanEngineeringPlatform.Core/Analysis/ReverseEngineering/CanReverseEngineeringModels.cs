using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Analysis.ReverseEngineering;

public enum SignalConfidenceLevel
{
    Candidate,
    Probable,
    Verified
}

public class CanIdDeepMetrics
{
    public uint CanId { get; set; }
    public string FormattedCanId => $"0x{CanId:X3}";
    public long MessageCount { get; set; }
    public double FrequencyHz { get; set; }
    public double PeriodMs { get; set; }
    public double MinIntervalMs { get; set; }
    public double MaxIntervalMs { get; set; }
    public double AverageIntervalMs { get; set; }
    public double JitterMs { get; set; }
    public byte Dlc { get; set; }
    public double PayloadEntropy { get; set; }
    public int[] ByteChangeCounts { get; set; } = new int[8];
    public ulong BitChangeMask { get; set; }
    public bool HasBurstPatterns { get; set; }
    public int MissingMessageCount { get; set; }
    public List<CounterCandidate> CounterCandidates { get; set; } = new();
    public List<ChecksumCandidate> ChecksumCandidates { get; set; } = new();
    public List<DiscoveredSignalCandidate> DiscoveredSignals { get; set; } = new();
    public byte[] LastPayload { get; set; } = Array.Empty<byte>();
    public DateTime LastSeenUtc { get; set; }
}

public class CounterCandidate
{
    public int StartBit { get; set; }
    public int BitLength { get; set; }
    public int Modulo { get; set; }
    public double MonotonicityConfidence { get; set; }
}

public class ChecksumCandidate
{
    public int ByteIndex { get; set; }
    public string Algorithm { get; set; } = "Sum8"; // Sum8, Xor8, Crc8
    public double MatchRatio { get; set; }
}

public class DiscoveredSignalCandidate
{
    public string Name { get; set; } = string.Empty;
    public string PhysicalCategory { get; set; } = "Unknown"; // RPM, Speed, Temp, Pressure, State
    public int StartBit { get; set; }
    public int BitLength { get; set; }
    public bool IsLittleEndian { get; set; } = true;
    public double MinObservedRaw { get; set; }
    public double MaxObservedRaw { get; set; }
    public double PlausibleFactor { get; set; } = 1.0;
    public double PlausibleOffset { get; set; } = 0.0;
    public string Unit { get; set; } = string.Empty;
    public SignalConfidenceLevel Confidence { get; set; } = SignalConfidenceLevel.Candidate;
    public string Rationale { get; set; } = string.Empty;
}

public class ReverseEngineeringEventComparison
{
    public string EventName { get; set; } = string.Empty; // Throttle, Brake, Steering, Gear, etc.
    public DateTime BaselineStartedUtc { get; set; }
    public DateTime EventTriggeredUtc { get; set; }
    public List<CorrelatedCanChange> CorrelatedChanges { get; set; } = new();
    public string AnalysisSummary { get; set; } = string.Empty;
}

public class CorrelatedCanChange
{
    public uint CanId { get; set; }
    public double BaselineMeanEntropy { get; set; }
    public double EventMeanEntropy { get; set; }
    public int ChangedByteIndex { get; set; }
    public double CorrelationScore { get; set; } // 0.0 - 1.0
    public string SuspectedFunction { get; set; } = string.Empty;
    public SignalConfidenceLevel Confidence { get; set; } = SignalConfidenceLevel.Candidate;
}
