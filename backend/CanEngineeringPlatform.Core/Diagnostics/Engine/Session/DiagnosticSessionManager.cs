using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Trace;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Uds;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Session;

public interface IDiagnosticSessionManager : IDisposable
{
    DiagnosticSessionStatus GetStatus();
    bool TransitionTo(DiagnosticSessionState targetState, string reason);
    Task<bool> SwitchSessionAsync(uint requestCanId, DiagnosticSessionType sessionType, string ecuName = "ECM", CancellationToken cancellationToken = default);
    void StartTesterPresent(uint requestCanId, string ecuName = "ECM", int intervalMs = 2000, bool suppressResponse = false);
    void StopTesterPresent();
    void SetConnectedEcu(string ecuName, uint requestCanId, uint responseCanId);
    event Action<DiagnosticSessionStatus>? OnSessionStatusChanged;
}

public class DiagnosticSessionManager : IDiagnosticSessionManager
{
    private readonly IDiagnosticSessionService _diagnosticSessionService;
    private readonly IDiagnosticTraceService _traceService;
    private readonly DiagnosticSessionStatus _status = new();
    private readonly object _stateLock = new();

    private CancellationTokenSource? _testerPresentCts;
    private Task? _testerPresentTask;
    private bool _disposed;

    public event Action<DiagnosticSessionStatus>? OnSessionStatusChanged;

    public DiagnosticSessionManager(
        IDiagnosticSessionService diagnosticSessionService,
        IDiagnosticTraceService traceService)
    {
        _diagnosticSessionService = diagnosticSessionService ?? throw new ArgumentNullException(nameof(diagnosticSessionService));
        _traceService = traceService ?? throw new ArgumentNullException(nameof(traceService));
    }

    public DiagnosticSessionStatus GetStatus()
    {
        lock (_stateLock)
        {
            return new DiagnosticSessionStatus
            {
                SessionId = _status.SessionId,
                EcuName = _status.EcuName,
                RequestCanId = _status.RequestCanId,
                ResponseCanId = _status.ResponseCanId,
                CurrentState = _status.CurrentState,
                ActiveUdsSession = _status.ActiveUdsSession,
                P2ServerMs = _status.P2ServerMs,
                P2StarServerMs = _status.P2StarServerMs,
                IsTesterPresentActive = _status.IsTesterPresentActive,
                TesterPresentIntervalMs = _status.TesterPresentIntervalMs,
                SuppressTesterPresentPositiveResponse = _status.SuppressTesterPresentPositiveResponse,
                LastCommunicationUtc = _status.LastCommunicationUtc,
                LastError = _status.LastError,
                History = new List<SessionTransitionRecord>(_status.History)
            };
        }
    }

    public void SetConnectedEcu(string ecuName, uint requestCanId, uint responseCanId)
    {
        lock (_stateLock)
        {
            _status.EcuName = ecuName;
            _status.RequestCanId = requestCanId;
            _status.ResponseCanId = responseCanId;
        }
    }

    public bool TransitionTo(DiagnosticSessionState targetState, string reason)
    {
        lock (_stateLock)
        {
            if (_status.CurrentState == targetState) return true;

            if (!IsValidTransition(_status.CurrentState, targetState))
            {
                throw new InvalidOperationException($"Invalid diagnostic session state transition: Cannot transition from {_status.CurrentState} to {targetState}. Reason: {reason}");
            }

            var record = new SessionTransitionRecord
            {
                FromState = _status.CurrentState,
                ToState = targetState,
                TimestampUtc = DateTime.UtcNow,
                Reason = reason
            };

            _status.CurrentState = targetState;
            _status.History.Add(record);

            if (targetState == DiagnosticSessionState.Disconnected ||
                targetState == DiagnosticSessionState.DefaultSession ||
                targetState == DiagnosticSessionState.Terminated ||
                targetState == DiagnosticSessionState.Faulted)
            {
                if (targetState != DiagnosticSessionState.ExtendedSession && targetState != DiagnosticSessionState.ProgrammingSession)
                {
                    StopTesterPresentInternal();
                }
            }

            OnSessionStatusChanged?.Invoke(GetStatus());
            return true;
        }
    }

    public static bool IsValidTransition(DiagnosticSessionState current, DiagnosticSessionState target)
    {
        // Terminal or fault states can be entered from any state
        if (target == DiagnosticSessionState.Terminated || target == DiagnosticSessionState.Faulted)
            return true;

        return current switch
        {
            DiagnosticSessionState.Disconnected => target == DiagnosticSessionState.Connected,
            DiagnosticSessionState.Connected => target == DiagnosticSessionState.Disconnected || target == DiagnosticSessionState.Identified,
            DiagnosticSessionState.Identified => target == DiagnosticSessionState.Disconnected || target == DiagnosticSessionState.ProtocolConfirmed,
            DiagnosticSessionState.ProtocolConfirmed => target == DiagnosticSessionState.Disconnected || target == DiagnosticSessionState.DefaultSession,
            DiagnosticSessionState.DefaultSession => target == DiagnosticSessionState.Disconnected || target == DiagnosticSessionState.ExtendedSession || target == DiagnosticSessionState.ProgrammingSession,
            DiagnosticSessionState.ExtendedSession => target == DiagnosticSessionState.Disconnected || target == DiagnosticSessionState.DefaultSession || target == DiagnosticSessionState.ProgrammingSession,
            DiagnosticSessionState.ProgrammingSession => target == DiagnosticSessionState.Disconnected || target == DiagnosticSessionState.DefaultSession || target == DiagnosticSessionState.ExtendedSession,
            DiagnosticSessionState.Terminated => target == DiagnosticSessionState.Disconnected,
            DiagnosticSessionState.Faulted => target == DiagnosticSessionState.Disconnected || target == DiagnosticSessionState.DefaultSession,
            _ => false
        };
    }

    public async Task<bool> SwitchSessionAsync(uint requestCanId, DiagnosticSessionType sessionType, string ecuName = "ECM", CancellationToken cancellationToken = default)
    {
        byte subFunc = (byte)sessionType;
        byte[] requestPayload = new byte[] { (byte)UdsServiceId.DiagnosticSessionControl, subFunc };

        var sw = Stopwatch.StartNew();
        _traceService.RecordTrace(new DiagnosticTraceEntry
        {
            TransportId = "iso-tp",
            RequestCanId = requestCanId,
            ResponseCanId = requestCanId + 8,
            Direction = DiagnosticDirection.Tx,
            Protocol = DiagnosticProtocolType.Uds,
            ServiceId = (byte)UdsServiceId.DiagnosticSessionControl,
            SubFunction = subFunc,
            Payload = requestPayload,
            Result = DiagnosticExchangeResult.Pending,
            ActiveSession = _status.CurrentState
        });

        var exchange = await _diagnosticSessionService.SendRequestAsync(requestCanId, requestPayload, ecuName, isSimulation: true);

        // Wait up to P2 timeout (or 500ms default)
        int waitTimeout = 500;
        int elapsed = 0;
        while (!exchange.IsComplete && elapsed < waitTimeout)
        {
            await Task.Delay(20, cancellationToken);
            elapsed += 20;
        }
        sw.Stop();

        if (!exchange.IsComplete)
        {
            _traceService.RecordTrace(new DiagnosticTraceEntry
            {
                TransportId = "iso-tp",
                RequestCanId = requestCanId,
                ResponseCanId = requestCanId + 8,
                Direction = DiagnosticDirection.Rx,
                Protocol = DiagnosticProtocolType.Uds,
                ServiceId = (byte)UdsServiceId.DiagnosticSessionControl,
                SubFunction = subFunc,
                Payload = Array.Empty<byte>(),
                Result = DiagnosticExchangeResult.Timeout,
                LatencyMs = sw.Elapsed.TotalMilliseconds,
                ActiveSession = _status.CurrentState,
                Notes = "UDS 0x10 DiagnosticSessionControl response timed out."
            });

            lock (_stateLock)
            {
                _status.LastError = "Session switch timed out.";
            }
            return false;
        }

        if (!exchange.IsSuccess)
        {
            byte nrcCode = 0x10;
            if (!string.IsNullOrEmpty(exchange.NrcHex) && exchange.NrcHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                byte.TryParse(exchange.NrcHex[2..], System.Globalization.NumberStyles.HexNumber, null, out nrcCode);
            }

            var eval = NrcClassifier.Evaluate(nrcCode);

            _traceService.RecordTrace(new DiagnosticTraceEntry
            {
                TransportId = "iso-tp",
                RequestCanId = requestCanId,
                ResponseCanId = requestCanId + 8,
                Direction = DiagnosticDirection.Rx,
                Protocol = DiagnosticProtocolType.Uds,
                ServiceId = (byte)UdsServiceId.DiagnosticSessionControl,
                SubFunction = subFunc,
                Payload = new byte[] { 0x7F, (byte)UdsServiceId.DiagnosticSessionControl, nrcCode },
                Result = DiagnosticExchangeResult.NegativeResponse,
                NegativeResponseCode = nrcCode,
                NrcDescription = eval.Meaning,
                RetryClassification = eval.RetryBehavior,
                LatencyMs = sw.Elapsed.TotalMilliseconds,
                ActiveSession = _status.CurrentState,
                Notes = eval.PossibleExplanation
            });

            lock (_stateLock)
            {
                _status.LastError = $"Session switch rejected: {eval.Meaning} ({exchange.NrcHex})";
            }
            return false;
        }

        // Positive Response (0x50 <subfunc> [P2_ms_high P2_ms_low P2*_ms_high P2*_ms_low])
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

        int p2ServerMs = 50;
        int p2StarServerMs = 5000;
        if (respBytes.Length >= 6 && respBytes[0] == 0x50)
        {
            p2ServerMs = (respBytes[2] << 8) | respBytes[3];
            p2StarServerMs = ((respBytes[4] << 8) | respBytes[5]) * 10;
            if (p2ServerMs <= 0) p2ServerMs = 50;
            if (p2StarServerMs <= 0) p2StarServerMs = 5000;
        }

        _traceService.RecordTrace(new DiagnosticTraceEntry
        {
            TransportId = "iso-tp",
            RequestCanId = requestCanId,
            ResponseCanId = requestCanId + 8,
            Direction = DiagnosticDirection.Rx,
            Protocol = DiagnosticProtocolType.Uds,
            ServiceId = (byte)UdsServiceId.DiagnosticSessionControl,
            SubFunction = subFunc,
            Payload = respBytes,
            Result = DiagnosticExchangeResult.PositiveResponse,
            LatencyMs = sw.Elapsed.TotalMilliseconds,
            ActiveSession = _status.CurrentState
        });

        lock (_stateLock)
        {
            _status.P2ServerMs = p2ServerMs;
            _status.P2StarServerMs = p2StarServerMs;
            _status.ActiveUdsSession = sessionType;
            _status.LastCommunicationUtc = DateTime.UtcNow;
            _status.LastError = null;
        }

        // Map target state based on session type
        DiagnosticSessionState targetState = sessionType switch
        {
            DiagnosticSessionType.DefaultSession => DiagnosticSessionState.DefaultSession,
            DiagnosticSessionType.ExtendedDiagnosticSession => DiagnosticSessionState.ExtendedSession,
            DiagnosticSessionType.ProgrammingSession => DiagnosticSessionState.ProgrammingSession,
            _ => DiagnosticSessionState.ExtendedSession
        };

        if (_status.CurrentState == DiagnosticSessionState.Disconnected)
        {
            TransitionTo(DiagnosticSessionState.Connected, "Link established via UDS request.");
            TransitionTo(DiagnosticSessionState.Identified, "ECU identified.");
            TransitionTo(DiagnosticSessionState.ProtocolConfirmed, "Protocol confirmed.");
            TransitionTo(DiagnosticSessionState.DefaultSession, "Entered default session.");
        }

        TransitionTo(targetState, $"UDS 0x10 DiagnosticSessionControl positive response received for 0x{subFunc:X2}.");

        // If non-default session, automatically start TesterPresent to keep alive
        if (targetState == DiagnosticSessionState.ExtendedSession || targetState == DiagnosticSessionState.ProgrammingSession)
        {
            StartTesterPresent(requestCanId, ecuName, 2000, suppressResponse: false);
        }

        return true;
    }

    public void StartTesterPresent(uint requestCanId, string ecuName = "ECM", int intervalMs = 2000, bool suppressResponse = false)
    {
        lock (_stateLock)
        {
            StopTesterPresentInternal();

            _testerPresentCts = new CancellationTokenSource();
            _status.IsTesterPresentActive = true;
            _status.TesterPresentIntervalMs = intervalMs;
            _status.SuppressTesterPresentPositiveResponse = suppressResponse;

            var token = _testerPresentCts.Token;
            _testerPresentTask = Task.Run(async () =>
            {
                byte subFunc = (byte)(suppressResponse ? 0x80 : 0x00);
                byte[] tpPayload = new byte[] { (byte)UdsServiceId.TesterPresent, subFunc };

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(intervalMs, token);
                        if (token.IsCancellationRequested) break;

                        // Only send if in non-default session
                        lock (_stateLock)
                        {
                            if (_status.CurrentState != DiagnosticSessionState.ExtendedSession &&
                                _status.CurrentState != DiagnosticSessionState.ProgrammingSession)
                            {
                                break;
                            }
                        }

                        var ex = await _diagnosticSessionService.SendRequestAsync(requestCanId, tpPayload, ecuName, isSimulation: true);
                        int waited = 0;
                        while (!ex.IsComplete && waited < 200)
                        {
                            await Task.Delay(20, token);
                            waited += 20;
                        }

                        lock (_stateLock)
                        {
                            _status.LastCommunicationUtc = DateTime.UtcNow;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        lock (_stateLock)
                        {
                            _status.LastError = $"TesterPresent error: {ex.Message}";
                        }
                    }
                }

                lock (_stateLock)
                {
                    _status.IsTesterPresentActive = false;
                }
            }, token);
        }
    }

    public void StopTesterPresent()
    {
        lock (_stateLock)
        {
            StopTesterPresentInternal();
        }
    }

    private void StopTesterPresentInternal()
    {
        if (_testerPresentCts != null)
        {
            _testerPresentCts.Cancel();
            _testerPresentCts.Dispose();
            _testerPresentCts = null;
        }
        _testerPresentTask = null;
        _status.IsTesterPresentActive = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopTesterPresent();
    }
}
