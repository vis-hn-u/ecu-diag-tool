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
using CanEngineeringPlatform.Core.EcuEngineering.Memory;
using CanEngineeringPlatform.Core.EcuEngineering.Reading;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Reset;

public interface IEcuResetService
{
    Task<EcuResetResult> ExecuteResetAsync(EcuResetRequest request, CancellationToken cancellationToken = default);
    IReadOnlyList<EcuResetResult> GetResetHistory();
    event Action<EcuResetResult>? OnEcuResetExecuted;
}

public class EcuResetService : IEcuResetService
{
    private readonly IDiagnosticSessionService _sessionService;
    private readonly IDiagnosticSessionManager _sessionManager;
    private readonly IDiagnosticTraceService _traceService;
    private readonly IEcuMemoryReader? _memoryReader;

    private readonly List<EcuResetResult> _history = new();
    private readonly object _historyLock = new();

    public event Action<EcuResetResult>? OnEcuResetExecuted;

    public EcuResetService(
        IDiagnosticSessionService sessionService,
        IDiagnosticSessionManager sessionManager,
        IDiagnosticTraceService traceService,
        IEcuMemoryReader? memoryReader = null)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _traceService = traceService ?? throw new ArgumentNullException(nameof(traceService));
        _memoryReader = memoryReader;
    }

    public async Task<EcuResetResult> ExecuteResetAsync(EcuResetRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        if (request.RequireUserConfirmation && !request.UserConfirmed)
        {
            throw new UnauthorizedAccessException("Operator confirmation is required before commanding an ECU reset.");
        }

        // Safety Invariant: Check if active memory read/flash operation is ongoing
        if (_memoryReader != null)
        {
            var activeReads = _memoryReader.GetAllSessions();
            if (activeReads.Any(s => s.State == ReadSessionState.Reading || s.State == ReadSessionState.Verifying))
            {
                throw new InvalidOperationException("CRITICAL SAFETY GUARD: Cannot execute ECU reset while memory read/extraction operation is in progress.");
            }
        }

        byte subFunc = (byte)request.ResetType;
        byte[] payload = new byte[] { (byte)UdsServiceId.EcuReset, subFunc };

        var sw = Stopwatch.StartNew();
        _traceService.RecordTrace(new DiagnosticTraceEntry
        {
            TransportId = "iso-tp",
            RequestCanId = request.RequestCanId,
            ResponseCanId = request.RequestCanId + 8,
            Direction = DiagnosticDirection.Tx,
            Protocol = DiagnosticProtocolType.Uds,
            ServiceId = (byte)UdsServiceId.EcuReset,
            SubFunction = subFunc,
            Payload = payload,
            Result = DiagnosticExchangeResult.Pending,
            ActiveSession = _sessionManager.GetStatus().CurrentState,
            Notes = $"ECU Reset ({request.ResetType}) requested."
        });

        var exchange = await _sessionService.SendRequestAsync(request.RequestCanId, payload, request.EcuName, isSimulation: true);

        int waited = 0;
        while (!exchange.IsComplete && waited < 800)
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

        int powerDownTime = 0;
        if (respBytes.Length >= 3 && respBytes[0] == 0x51)
        {
            powerDownTime = respBytes[2]; // Power down time in seconds
        }

        _traceService.RecordTrace(new DiagnosticTraceEntry
        {
            TransportId = "iso-tp",
            RequestCanId = request.RequestCanId,
            ResponseCanId = request.RequestCanId + 8,
            Direction = DiagnosticDirection.Rx,
            Protocol = DiagnosticProtocolType.Uds,
            ServiceId = (byte)UdsServiceId.EcuReset,
            SubFunction = subFunc,
            Payload = respBytes,
            Result = success ? DiagnosticExchangeResult.PositiveResponse : DiagnosticExchangeResult.NegativeResponse,
            LatencyMs = sw.Elapsed.TotalMilliseconds,
            ActiveSession = _sessionManager.GetStatus().CurrentState,
            Notes = success ? $"ECU Reset positive response (0x51). Power down time: {powerDownTime}s." : exchange.Summary
        });

        bool commRestored = false;
        DateTime? restoredUtc = null;

        if (success)
        {
            // Transition Session State Machine to Disconnected during reboot, then back to DefaultSession
            _sessionManager.TransitionTo(DiagnosticSessionState.Disconnected, $"ECU rebooting due to {request.ResetType}.");

            // Wait recovery latency
            await Task.Delay(300, cancellationToken);

            // Re-establish communication
            _sessionManager.TransitionTo(DiagnosticSessionState.Connected, "Reconnected after ECU reset.");
            _sessionManager.TransitionTo(DiagnosticSessionState.Identified, "ECU re-identified after reset.");
            _sessionManager.TransitionTo(DiagnosticSessionState.ProtocolConfirmed, "Protocol confirmed post-reset.");
            _sessionManager.TransitionTo(DiagnosticSessionState.DefaultSession, "Restored default diagnostic session.");

            commRestored = true;
            restoredUtc = DateTime.UtcNow;
        }

        var result = new EcuResetResult
        {
            EcuName = request.EcuName,
            ResetType = request.ResetType,
            Success = success,
            ErrorMessage = success ? null : exchange.Summary,
            PostResetPowerDownDelaySeconds = powerDownTime,
            CommunicationRestored = commRestored,
            InitiatedUtc = DateTime.UtcNow,
            RestoredUtc = restoredUtc
        };

        lock (_historyLock)
        {
            _history.Add(result);
        }

        OnEcuResetExecuted?.Invoke(result);
        return result;
    }

    public IReadOnlyList<EcuResetResult> GetResetHistory()
    {
        lock (_historyLock)
        {
            return _history.OrderByDescending(h => h.InitiatedUtc).ToList();
        }
    }
}
