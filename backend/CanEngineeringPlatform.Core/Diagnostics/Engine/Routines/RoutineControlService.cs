using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Session;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Trace;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Uds;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Routines;

public interface IRoutineControlService
{
    IReadOnlyList<RoutineDescriptor> GetSupportedRoutines();
    RoutineDescriptor? GetRoutine(ushort routineId);
    Task<RoutineExecutionResult> ExecuteRoutineAsync(RoutineExecutionRequest request, CancellationToken cancellationToken = default);
    IReadOnlyList<RoutineExecutionResult> GetExecutionHistory();
    event Action<RoutineExecutionResult>? OnRoutineExecuted;
}

public class RoutineControlService : IRoutineControlService
{
    private readonly IDiagnosticSessionService _sessionService;
    private readonly IDiagnosticSessionManager _sessionManager;
    private readonly IDiagnosticTraceService _traceService;

    private readonly ConcurrentDictionary<ushort, RoutineDescriptor> _routines = new();
    private readonly List<RoutineExecutionResult> _history = new();
    private readonly object _historyLock = new();

    public event Action<RoutineExecutionResult>? OnRoutineExecuted;

    public RoutineControlService(
        IDiagnosticSessionService sessionService,
        IDiagnosticSessionManager sessionManager,
        IDiagnosticTraceService traceService)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _traceService = traceService ?? throw new ArgumentNullException(nameof(traceService));

        RegisterDefaultRoutines();
    }

    private void RegisterDefaultRoutines()
    {
        // 0x0201: EVAP Purge Valve Test
        RegisterRoutine(new RoutineDescriptor
        {
            RoutineId = 0x0201,
            Name = "EVAP Purge Valve Actuation",
            Description = "Pulses the evaporative emission canister purge solenoid to verify mechanical clicking and flow integrity.",
            RequiredSession = DiagnosticSessionType.ExtendedDiagnosticSession,
            RequiresSecurityAccess = false,
            SafetyPreconditions = new() { "Engine OFF", "Ignition ON", "Battery Voltage > 11.8V" },
            ExpectedDurationSeconds = 15,
            RecoveryBehavior = "Valve automatically closes upon routine completion or tester disconnection."
        });

        // 0x0202: Electric Fuel Pump Test
        RegisterRoutine(new RoutineDescriptor
        {
            RoutineId = 0x0202,
            Name = "Electric Fuel Pump Prime Override",
            Description = "Commands in-tank fuel lift pump to run at 100% duty cycle for fuel rail pressurization and priming.",
            RequiredSession = DiagnosticSessionType.ExtendedDiagnosticSession,
            RequiresSecurityAccess = false,
            SafetyPreconditions = new() { "Engine OFF", "Ignition ON", "No active fuel rail leaks" },
            ExpectedDurationSeconds = 10,
            RecoveryBehavior = "Fuel pump relay opens immediately after elapsed test interval."
        });

        // 0x0203: Radiator Cooling Fan Test
        RegisterRoutine(new RoutineDescriptor
        {
            RoutineId = 0x0203,
            Name = "Radiator Cooling Fan Test",
            Description = "Sequences primary and secondary electric radiator fans from 30% to 100% PWM speed.",
            RequiredSession = DiagnosticSessionType.ExtendedDiagnosticSession,
            RequiresSecurityAccess = false,
            SafetyPreconditions = new() { "Engine OFF", "Engine bay clear of foreign objects", "Hand tools clear of fan blades" },
            ExpectedDurationSeconds = 20,
            RecoveryBehavior = "PWM drive returns to ECU automatic thermostatic duty cycle."
        });
    }

    public void RegisterRoutine(RoutineDescriptor routine)
    {
        _routines[routine.RoutineId] = routine;
    }

    public IReadOnlyList<RoutineDescriptor> GetSupportedRoutines()
    {
        return _routines.Values.OrderBy(r => r.RoutineId).ToList();
    }

    public RoutineDescriptor? GetRoutine(ushort routineId)
    {
        _routines.TryGetValue(routineId, out var r);
        return r;
    }

    public async Task<RoutineExecutionResult> ExecuteRoutineAsync(RoutineExecutionRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (!_routines.TryGetValue(request.RoutineId, out var descriptor))
        {
            throw new ArgumentException($"Unknown or unsupported diagnostic routine: 0x{request.RoutineId:X4}. Unsupported routines are never guessed.");
        }

        if (request.RequireUserConfirmation && !request.UserConfirmed)
        {
            throw new UnauthorizedAccessException($"Operator confirmation required before actuating routine 0x{request.RoutineId:X4} ({descriptor.Name}).");
        }

        // Validate session requirements
        var sessionStatus = _sessionManager.GetStatus();
        if (descriptor.RequiredSession == DiagnosticSessionType.ExtendedDiagnosticSession &&
            sessionStatus.CurrentState != DiagnosticSessionState.ExtendedSession)
        {
            // Auto switch to extended session
            bool switched = await _sessionManager.SwitchSessionAsync(request.RequestCanId, DiagnosticSessionType.ExtendedDiagnosticSession, request.EcuName, cancellationToken);
            if (!switched)
            {
                throw new InvalidOperationException($"Routine 0x{request.RoutineId:X4} requires Extended Diagnostic Session (0x03), but the ECU rejected the session change.");
            }
        }

        // Build UDS 0x31 RoutineControl payload
        // 0x31 <subFunction: 0x01/0x02/0x03> <routineId_H> <routineId_L> [optionRecord...]
        byte rH = (byte)((request.RoutineId >> 8) & 0xFF);
        byte rL = (byte)(request.RoutineId & 0xFF);

        var payloadList = new List<byte> { (byte)UdsServiceId.RoutineControl, request.SubFunction, rH, rL };
        if (request.OptionRecord != null && request.OptionRecord.Length > 0)
        {
            payloadList.AddRange(request.OptionRecord);
        }
        byte[] payload = payloadList.ToArray();

        var sw = Stopwatch.StartNew();
        _traceService.RecordTrace(new DiagnosticTraceEntry
        {
            TransportId = "iso-tp",
            RequestCanId = request.RequestCanId,
            ResponseCanId = request.RequestCanId + 8,
            Direction = DiagnosticDirection.Tx,
            Protocol = DiagnosticProtocolType.Uds,
            ServiceId = (byte)UdsServiceId.RoutineControl,
            SubFunction = request.SubFunction,
            Payload = payload,
            Result = DiagnosticExchangeResult.Pending,
            ActiveSession = _sessionManager.GetStatus().CurrentState,
            Notes = $"Executing routine 0x{request.RoutineId:X4} ({descriptor.Name})"
        });

        var exchange = await _sessionService.SendRequestAsync(request.RequestCanId, payload, request.EcuName, isSimulation: true);

        int waited = 0;
        while (!exchange.IsComplete && waited < 1000)
        {
            await Task.Delay(25, cancellationToken);
            waited += 25;
        }
        sw.Stop();

        bool success = exchange.IsComplete && exchange.IsSuccess;
        byte[] respBytes = Array.Empty<byte>();

        if (!string.IsNullOrEmpty(exchange.ResponseDataHex))
        {
            string[] hexParts = exchange.ResponseDataHex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            respBytes = new byte[hexParts.Length];
            for (int i = 0; i < hexParts.Length; i++)
            {
                byte.TryParse(hexParts[i], System.Globalization.NumberStyles.HexNumber, null, out respBytes[i]);
            }
        }

        byte routineStatus = 0x00;
        if (respBytes.Length >= 5 && respBytes[0] == 0x71)
        {
            routineStatus = respBytes[4];
        }

        _traceService.RecordTrace(new DiagnosticTraceEntry
        {
            TransportId = "iso-tp",
            RequestCanId = request.RequestCanId,
            ResponseCanId = request.RequestCanId + 8,
            Direction = DiagnosticDirection.Rx,
            Protocol = DiagnosticProtocolType.Uds,
            ServiceId = (byte)UdsServiceId.RoutineControl,
            SubFunction = request.SubFunction,
            Payload = respBytes,
            Result = success ? DiagnosticExchangeResult.PositiveResponse : DiagnosticExchangeResult.NegativeResponse,
            LatencyMs = sw.Elapsed.TotalMilliseconds,
            ActiveSession = _sessionManager.GetStatus().CurrentState,
            Notes = success ? $"Routine 0x{request.RoutineId:X4} acknowledged." : exchange.Summary
        });

        var result = new RoutineExecutionResult
        {
            RoutineId = request.RoutineId,
            RoutineName = descriptor.Name,
            SubFunction = request.SubFunction,
            Success = success,
            State = success ? RoutineExecutionState.Completed : RoutineExecutionState.Failed,
            ResponsePayload = respBytes,
            RoutineStatusRecord = routineStatus,
            ErrorMessage = success ? null : exchange.Summary,
            ExecutionLatencyMs = sw.Elapsed.TotalMilliseconds,
            TimestampUtc = DateTime.UtcNow
        };

        lock (_historyLock)
        {
            _history.Add(result);
        }

        OnRoutineExecuted?.Invoke(result);
        return result;
    }

    public IReadOnlyList<RoutineExecutionResult> GetExecutionHistory()
    {
        lock (_historyLock)
        {
            return _history.OrderByDescending(h => h.TimestampUtc).ToList();
        }
    }
}
