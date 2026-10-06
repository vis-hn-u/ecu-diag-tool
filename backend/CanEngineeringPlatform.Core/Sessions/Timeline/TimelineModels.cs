using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.Sessions.Timeline;

public enum TimelineCategory
{
    CanFrame,
    CanFdFrame,
    DtcEvent,
    UdsExchange,
    ObdEvent,
    SignalChange,
    EcuStateChange,
    UserAction,
    Annotation,
    SimulationEvent,
    FlashOperation,
    CalibrationChange,
    TestResult,
    SecurityAlert,
    HardwareEvent
}

public enum TimelineSeverity
{
    Trace,
    Debug,
    Info,
    Notice,
    Warning,
    Error,
    Critical
}

public class TimelineEntry
{
    public long SequenceId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public TimelineCategory Category { get; set; }
    public TimelineSeverity Severity { get; set; } = TimelineSeverity.Info;
    public string SourceId { get; set; } = "SYSTEM";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? RawHex { get; set; }
    public string? CorrelationId { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public interface IUnifiedTimelineService
{
    void Record(TimelineCategory category, string title, string description, TimelineSeverity severity = TimelineSeverity.Info, string source = "SYSTEM", string? rawHex = null, string? correlationId = null, Dictionary<string, string>? metadata = null);
    IReadOnlyList<TimelineEntry> GetEntries(int maxCount = 500, TimelineCategory? filterCategory = null, TimelineSeverity? minSeverity = null);
    IReadOnlyList<TimelineEntry> Search(string query, int maxCount = 200);
    void Clear();
    long GetTotalCount();
    event Action<TimelineEntry>? OnEntryRecorded;
}

public class UnifiedTimelineService : IUnifiedTimelineService
{
    private readonly ConcurrentQueue<TimelineEntry> _entries = new();
    private long _seqCounter = 0;
    private const int MaxEntries = 20000;

    public event Action<TimelineEntry>? OnEntryRecorded;

    public void Record(
        TimelineCategory category,
        string title,
        string description,
        TimelineSeverity severity = TimelineSeverity.Info,
        string source = "SYSTEM",
        string? rawHex = null,
        string? correlationId = null,
        Dictionary<string, string>? metadata = null)
    {
        var entry = new TimelineEntry
        {
            SequenceId = System.Threading.Interlocked.Increment(ref _seqCounter),
            TimestampUtc = DateTime.UtcNow,
            Category = category,
            Severity = severity,
            SourceId = source ?? "SYSTEM",
            Title = title ?? string.Empty,
            Description = description ?? string.Empty,
            RawHex = rawHex,
            CorrelationId = correlationId,
            Metadata = metadata ?? new()
        };

        _entries.Enqueue(entry);

        while (_entries.Count > MaxEntries && _entries.TryDequeue(out _))
        {
            // Keep memory bounded
        }

        OnEntryRecorded?.Invoke(entry);
    }

    public IReadOnlyList<TimelineEntry> GetEntries(int maxCount = 500, TimelineCategory? filterCategory = null, TimelineSeverity? minSeverity = null)
    {
        var query = _entries.AsEnumerable();

        if (filterCategory.HasValue)
        {
            query = query.Where(e => e.Category == filterCategory.Value);
        }

        if (minSeverity.HasValue)
        {
            query = query.Where(e => e.Severity >= minSeverity.Value);
        }

        int count = Math.Clamp(maxCount, 1, 5000);
        return query.Reverse().Take(count).ToList();
    }

    public IReadOnlyList<TimelineEntry> Search(string query, int maxCount = 200)
    {
        if (string.IsNullOrWhiteSpace(query)) return GetEntries(maxCount);

        var q = query.Trim();
        return _entries
            .Where(e => e.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        e.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        (e.RawHex != null && e.RawHex.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                        (e.CorrelationId != null && e.CorrelationId.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .Reverse()
            .Take(Math.Clamp(maxCount, 1, 1000))
            .ToList();
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
    }

    public long GetTotalCount() => System.Threading.Interlocked.Read(ref _seqCounter);
}
