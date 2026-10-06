using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Hardware.Automotive;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Vehicles;

namespace CanEngineeringPlatform.Core.Sessions;

public enum RunType
{
    DynoPull,
    AccelerationTest,
    IdleMonitoring,
    DiagnosticScan,
    SteadyCruise,
    TrackLap,
    Custom
}

public class EngineeringRun
{
    public string RunId { get; set; } = Guid.NewGuid().ToString("N");
    public int RunNumber { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public RunType Type { get; set; } = RunType.DynoPull;
    public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndTimeUtc { get; set; }
    public double DurationSeconds => EndTimeUtc.HasValue
        ? Math.Max(0.0, (EndTimeUtc.Value - StartTimeUtc).TotalSeconds)
        : Math.Max(0.0, (DateTime.UtcNow - StartTimeUtc).TotalSeconds);

    public long StartFrameIndex { get; set; } = 0;
    public long EndFrameIndex { get; set; } = 0;
    public Dictionary<string, double> SummaryMetrics { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Notes { get; set; } = string.Empty;
}

public class EngineeringSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Engineering Session";
    public string Description { get; set; } = string.Empty;
    public string Technician { get; set; } = "Lead Calibrator";

    public string VehicleProfileId { get; set; } = string.Empty;
    public VehicleProfile? VehicleProfileSnapshot { get; set; }

    public string HardwareTransportId { get; set; } = string.Empty;
    public string HardwareAdapterName { get; set; } = string.Empty;
    public AutomotiveProtocol ActiveProtocol { get; set; } = AutomotiveProtocol.Can;

    public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndTimeUtc { get; set; }
    public bool IsActive { get; set; } = true;

    public string? LogSessionId { get; set; }
    public List<EngineeringRun> Runs { get; set; } = new();
    public List<string> Tags { get; set; } = new();

    public EngineeringRun StartNewRun(string name, RunType type, long currentFrameIndex = 0)
    {
        var run = new EngineeringRun
        {
            RunNumber = Runs.Count + 1,
            Name = string.IsNullOrWhiteSpace(name) ? $"Run #{Runs.Count + 1} ({type})" : name,
            Type = type,
            StartTimeUtc = DateTime.UtcNow,
            StartFrameIndex = currentFrameIndex
        };
        Runs.Add(run);
        return run;
    }

    public EngineeringRun? StopActiveRun(string runId, long currentFrameIndex = 0, Dictionary<string, double>? metrics = null)
    {
        var run = Runs.FirstOrDefault(r => r.RunId == runId && !r.EndTimeUtc.HasValue);
        if (run != null)
        {
            run.EndTimeUtc = DateTime.UtcNow;
            run.EndFrameIndex = currentFrameIndex;
            if (metrics != null)
            {
                foreach (var kvp in metrics)
                {
                    run.SummaryMetrics[kvp.Key] = kvp.Value;
                }
            }
        }
        return run;
    }
}

public interface ISessionManagementService
{
    EngineeringSession StartSession(string name, string? description = null, string? technician = null, List<string>? tags = null);
    EngineeringSession? StopSession(string? sessionId = null);
    EngineeringSession? GetActiveSession();
    EngineeringSession? GetSession(string id);
    IReadOnlyList<EngineeringSession> GetAllSessions();

    EngineeringRun? StartRun(string? sessionId, string name, RunType type);
    EngineeringRun? StopRun(string? sessionId, string runId, Dictionary<string, double>? metrics = null);
}

public class SessionManagementService : ISessionManagementService
{
    private readonly IVehicleProfileService _vehicleService;
    private readonly IHardwareManager _hardwareManager;
    private readonly ICanLoggerService _loggerService;

    private readonly ConcurrentDictionary<string, EngineeringSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private EngineeringSession? _activeSession;
    private readonly object _lock = new();

    public SessionManagementService(
        IVehicleProfileService vehicleService,
        IHardwareManager hardwareManager,
        ICanLoggerService loggerService)
    {
        _vehicleService = vehicleService ?? throw new ArgumentNullException(nameof(vehicleService));
        _hardwareManager = hardwareManager ?? throw new ArgumentNullException(nameof(hardwareManager));
        _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
    }

    public EngineeringSession StartSession(string name, string? description = null, string? technician = null, List<string>? tags = null)
    {
        lock (_lock)
        {
            if (_activeSession != null && _activeSession.IsActive)
            {
                StopSession(_activeSession.SessionId);
            }

            var vehicle = _vehicleService.GetActiveProfile();
            var hwTransport = _hardwareManager.GetActiveTransport();
            var hwStatus = hwTransport?.GetStatus();

            // Also start a backing CAN frame log session
            var logSession = _loggerService.StartLogging(string.IsNullOrWhiteSpace(name) ? "Engineering_Log" : name);

            var session = new EngineeringSession
            {
                Name = string.IsNullOrWhiteSpace(name) ? $"Session_{DateTime.UtcNow:yyyyMMdd_HHmmss}" : name,
                Description = description ?? string.Empty,
                Technician = technician ?? "Lead Engineer",
                VehicleProfileId = vehicle.Id,
                VehicleProfileSnapshot = vehicle,
                HardwareTransportId = hwTransport?.TransportId ?? "unassigned",
                HardwareAdapterName = hwStatus?.Name ?? "No Hardware",
                ActiveProtocol = (hwTransport as IAutomotiveTransport)?.Protocol ?? AutomotiveProtocol.Can,
                StartTimeUtc = DateTime.UtcNow,
                IsActive = true,
                LogSessionId = logSession.Id,
                Tags = tags ?? new List<string>()
            };

            _sessions[session.SessionId] = session;
            _activeSession = session;
            return session;
        }
    }

    public EngineeringSession? StopSession(string? sessionId = null)
    {
        lock (_lock)
        {
            var target = !string.IsNullOrWhiteSpace(sessionId)
                ? GetSession(sessionId)
                : _activeSession;

            if (target != null && target.IsActive)
            {
                target.IsActive = false;
                target.EndTimeUtc = DateTime.UtcNow;

                // Stop any ongoing run
                var ongoingRun = target.Runs.FirstOrDefault(r => !r.EndTimeUtc.HasValue);
                if (ongoingRun != null)
                {
                    ongoingRun.EndTimeUtc = DateTime.UtcNow;
                }

                _loggerService.StopLogging();

                if (ReferenceEquals(_activeSession, target))
                {
                    _activeSession = null;
                }
            }

            return target;
        }
    }

    public EngineeringSession? GetActiveSession()
    {
        lock (_lock)
        {
            return _activeSession;
        }
    }

    public EngineeringSession? GetSession(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        _sessions.TryGetValue(id, out var session);
        return session;
    }

    public IReadOnlyList<EngineeringSession> GetAllSessions() => _sessions.Values.ToList();

    public EngineeringRun? StartRun(string? sessionId, string name, RunType type)
    {
        lock (_lock)
        {
            var session = !string.IsNullOrWhiteSpace(sessionId) ? GetSession(sessionId) : _activeSession;
            if (session == null || !session.IsActive) return null;

            long currentFrames = _loggerService.GetCurrentSession()?.FrameCount ?? 0;
            return session.StartNewRun(name, type, currentFrames);
        }
    }

    public EngineeringRun? StopRun(string? sessionId, string runId, Dictionary<string, double>? metrics = null)
    {
        lock (_lock)
        {
            var session = !string.IsNullOrWhiteSpace(sessionId) ? GetSession(sessionId) : _activeSession;
            if (session == null) return null;

            long currentFrames = _loggerService.GetCurrentSession()?.FrameCount ?? 0;
            return session.StopActiveRun(runId, currentFrames, metrics);
        }
    }
}
