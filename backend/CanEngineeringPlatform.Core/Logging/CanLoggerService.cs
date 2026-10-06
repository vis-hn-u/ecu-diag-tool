using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Logging;

public class LogSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndTimeUtc { get; set; }
    public bool IsActive { get; set; }
    public bool IsPaused { get; set; }
    public List<CanFrame> Frames { get; set; } = new();
    public long FrameCount => Frames.Count;
}

public interface ICanLoggerService
{
    LogSession StartLogging(string name);
    void PauseLogging();
    void ResumeLogging();
    LogSession? StopLogging();
    LogSession? GetCurrentSession();
    List<LogSession> GetSessions();
    LogSession? GetSession(string id);
    string ExportCsv(string sessionId);
    void ProcessFrame(in CanFrame frame);
}

public class CanLoggerService : ICanLoggerService
{
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, LogSession> _sessions = new();
    private LogSession? _currentSession;
    private const int MaxFramesPerSession = 500000;

    public LogSession StartLogging(string name)
    {
        lock (_lock)
        {
            if (_currentSession != null && _currentSession.IsActive)
            {
                StopLogging();
            }

            var session = new LogSession
            {
                Name = string.IsNullOrWhiteSpace(name) ? $"CAN_Log_{DateTime.UtcNow:yyyyMMdd_HHmmss}" : name,
                StartTimeUtc = DateTime.UtcNow,
                IsActive = true,
                IsPaused = false
            };

            _currentSession = session;
            _sessions[session.Id] = session;
            return session;
        }
    }

    public void PauseLogging()
    {
        lock (_lock)
        {
            if (_currentSession != null && _currentSession.IsActive)
            {
                _currentSession.IsPaused = true;
            }
        }
    }

    public void ResumeLogging()
    {
        lock (_lock)
        {
            if (_currentSession != null && _currentSession.IsActive)
            {
                _currentSession.IsPaused = false;
            }
        }
    }

    public LogSession? StopLogging()
    {
        lock (_lock)
        {
            if (_currentSession != null)
            {
                _currentSession.IsActive = false;
                _currentSession.IsPaused = false;
                _currentSession.EndTimeUtc = DateTime.UtcNow;
                var finished = _currentSession;
                _currentSession = null;
                return finished;
            }
            return null;
        }
    }

    public LogSession? GetCurrentSession()
    {
        lock (_lock)
        {
            return _currentSession;
        }
    }

    public List<LogSession> GetSessions()
    {
        return _sessions.Values.OrderByDescending(s => s.StartTimeUtc).ToList();
    }

    public LogSession? GetSession(string id)
    {
        _sessions.TryGetValue(id, out var session);
        return session;
    }

    public void ProcessFrame(in CanFrame frame)
    {
        lock (_lock)
        {
            if (_currentSession == null || !_currentSession.IsActive || _currentSession.IsPaused)
            {
                return;
            }

            if (_currentSession.Frames.Count < MaxFramesPerSession)
            {
                _currentSession.Frames.Add(frame);
            }
        }
    }

    public string ExportCsv(string sessionId)
    {
        var session = GetSession(sessionId);
        if (session == null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("TimestampUs,IdHex,IdDec,Type,RTR,DLC,DataHex,Channel,Protocol,BRS,ESI,SourceId");

        lock (_lock)
        {
            foreach (var f in session.Frames)
            {
                string idHex = f.IsExtended ? $"0x{f.Id:X8}" : $"0x{f.Id:X3}";
                string type = f.IsExtended ? "Extended" : "Standard";
                string rtr = f.IsRtr ? "1" : "0";
                string dataHex = f.ToHexString();
                string protocol = f.IsCanFd ? "CAN FD" : "Classic CAN";
                string brs = f.BitRateSwitch ? "1" : "0";
                string esi = f.ErrorStateIndicator ? "1" : "0";

                sb.AppendLine($"{f.TimestampUs},{idHex},{f.Id},{type},{rtr},{f.Dlc},{dataHex},{f.Channel},{protocol},{brs},{esi},{f.SourceId}");
            }
        }

        return sb.ToString();
    }
}
