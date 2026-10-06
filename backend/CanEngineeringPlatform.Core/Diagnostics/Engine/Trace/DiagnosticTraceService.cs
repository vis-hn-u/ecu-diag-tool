using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Trace;

public interface IDiagnosticTraceService
{
    void RecordTrace(DiagnosticTraceEntry entry);
    IReadOnlyList<DiagnosticTraceEntry> GetRecentTraces(int maxCount = 500);
    IReadOnlyList<DiagnosticTraceEntry> SearchTraces(
        uint? canId = null,
        byte? serviceId = null,
        DiagnosticExchangeResult? result = null,
        DateTime? since = null,
        int maxCount = 500);
    void ClearTraces();
    event Action<DiagnosticTraceEntry>? OnTraceRecorded;
}

public class DiagnosticTraceService : IDiagnosticTraceService
{
    private readonly ConcurrentQueue<DiagnosticTraceEntry> _traces = new();
    private readonly int _maxCapacity;
    private readonly object _lock = new();

    public event Action<DiagnosticTraceEntry>? OnTraceRecorded;

    public DiagnosticTraceService(int maxCapacity = 10000)
    {
        _maxCapacity = maxCapacity;
    }

    public void RecordTrace(DiagnosticTraceEntry entry)
    {
        if (entry == null) return;

        _traces.Enqueue(entry);

        while (_traces.Count > _maxCapacity && _traces.TryDequeue(out _))
        {
            // Evict oldest
        }

        OnTraceRecorded?.Invoke(entry);
    }

    public IReadOnlyList<DiagnosticTraceEntry> GetRecentTraces(int maxCount = 500)
    {
        int count = Math.Min(maxCount, _traces.Count);
        return _traces.Reverse().Take(count).ToList();
    }

    public IReadOnlyList<DiagnosticTraceEntry> SearchTraces(
        uint? canId = null,
        byte? serviceId = null,
        DiagnosticExchangeResult? result = null,
        DateTime? since = null,
        int maxCount = 500)
    {
        var query = _traces.AsEnumerable();

        if (canId.HasValue)
        {
            query = query.Where(t => t.RequestCanId == canId.Value || t.ResponseCanId == canId.Value);
        }

        if (serviceId.HasValue)
        {
            query = query.Where(t => t.ServiceId == serviceId.Value);
        }

        if (result.HasValue)
        {
            query = query.Where(t => t.Result == result.Value);
        }

        if (since.HasValue)
        {
            query = query.Where(t => t.TimestampUtc >= since.Value);
        }

        return query.Reverse().Take(maxCount).ToList();
    }

    public void ClearTraces()
    {
        while (_traces.TryDequeue(out _)) { }
    }
}
