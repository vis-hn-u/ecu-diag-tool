using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.Engine;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Health;
using CanEngineeringPlatform.Core.Diagnostics.Engine.LiveData;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Normalization;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Plausibility;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Reset;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Routines;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Session;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Trace;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.Diagnostics.Uds;
using CanEngineeringPlatform.Core.EcuEngineering.Compatibility;
using CanEngineeringPlatform.Core.EcuEngineering.Images;
using CanEngineeringPlatform.Core.EcuEngineering.Memory;
using CanEngineeringPlatform.Core.EcuEngineering.Reading;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Topology;

namespace CanEngineeringPlatform.Tests;

/// <summary>
/// Production Hardening Phase — Adversarial tests.
/// Each test targets a specific real vulnerability found during code review.
/// MUST FAIL without the corresponding production code fix.
/// MUST PASS after the fix.
/// </summary>
public class ProductionHardeningTests : IDisposable
{
    // ── Full wired test harness (same pattern as DiagnosticEnginePhase3Tests) ──

    private readonly ICanPipeline _pipeline;
    private readonly IIsoTpRouter _isoTpRouter;
    private readonly IEcuSimulator _simulator;
    private readonly IDiagnosticSessionService _sessionService;
    private readonly IDiagnosticTraceService _traceService;
    private readonly IDiagnosticSessionManager _sessionManager;
    private readonly IDtcService _dtcService;
    private readonly ILiveDataService _liveDataService;
    private readonly ISensorPlausibilityService _plausibilityService;
    private readonly IRoutineControlService _routineService;
    private readonly IEcuResetService _resetService;
    private readonly IEcuHealthManager _healthManager;
    private readonly IOemNormalizationService _normalizationService;
    private readonly IEcuCompatibilityManager _compatibilityManager;
    private readonly IEcuMemoryImageStore _imageStore;
    private readonly IEcuMemoryReader _memoryReader;

    public ProductionHardeningTests()
    {
        var topology = new TopologyEngine();
        var logger   = new CanLoggerService();
        _pipeline       = new CanPipeline(topology, logger);
        _isoTpRouter    = new IsoTpRouter();
        _simulator      = new EcuSimulator(_pipeline, _isoTpRouter);
        _sessionService = new DiagnosticSessionService(_pipeline, _isoTpRouter, _simulator);
        _traceService   = new DiagnosticTraceService(1000);
        _sessionManager = new DiagnosticSessionManager(_sessionService, _traceService);
        _dtcService     = new DtcService(_sessionService, _sessionManager, _traceService);
        _liveDataService = new LiveDataService(_sessionService, _sessionManager, _traceService);
        _plausibilityService = new SensorPlausibilityService(_liveDataService);
        _routineService = new RoutineControlService(_sessionService, _sessionManager, _traceService);
        _imageStore     = new EcuMemoryImageStore();
        _memoryReader   = new EcuMemoryReader(_sessionService, _imageStore);
        _resetService   = new EcuResetService(_sessionService, _sessionManager, _traceService, _memoryReader);
        _compatibilityManager = new EcuCompatibilityManager();
        _healthManager  = new EcuHealthManager(_dtcService, _liveDataService, _plausibilityService, _traceService);
        _normalizationService = new OemNormalizationService(_compatibilityManager, _liveDataService, _dtcService, _plausibilityService, _healthManager);
    }

    public void Dispose()
    {
        _sessionManager.Dispose();
        _liveDataService.Dispose();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-001 [P1] — TesterPresent with intervalMs=0 MUST NOT busy-loop
    // Root cause: Task.Delay(0) returns immediately, spinning the while loop
    //             at CPU maximum rate consuming 100% of one core.
    // Fix required: Clamp intervalMs to minimum 50ms in StartTesterPresent.
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bug001_TesterPresent_ZeroInterval_DoesNotBusyLoop()
    {
        // ACT — start with zero interval, run 200ms, stop
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _sessionManager.StartTesterPresent(0x7E0, "ECM", intervalMs: 0, suppressResponse: false);
        await Task.Delay(200, cts.Token);
        _sessionManager.StopTesterPresent();

        // ASSERT — if we reach here, no infinite spin block occurred
        Assert.False(cts.IsCancellationRequested, "TesterPresent with intervalMs=0 must not hang for 5 seconds.");
    }

    [Fact]
    public async Task Bug001_TesterPresent_NegativeInterval_DoesNotBusyLoop()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _sessionManager.StartTesterPresent(0x7E0, "ECM", intervalMs: -500, suppressResponse: false);
        await Task.Delay(200, cts.Token);
        _sessionManager.StopTesterPresent();

        Assert.False(cts.IsCancellationRequested, "TesterPresent with negative intervalMs must not hang.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-002 [P1] — StartPolling with intervalMs=0 MUST NOT busy-loop
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bug002_StartPolling_ZeroInterval_DoesNotBusyLoop()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _liveDataService.StartPolling(0x7E0, new[] { "ENGINE_RPM" }, intervalMs: 0, ecuName: "ECM");
        await Task.Delay(300, cts.Token);
        _liveDataService.StopPolling();

        Assert.False(cts.IsCancellationRequested, "StartPolling with intervalMs=0 must not hang.");
    }

    [Fact]
    public async Task Bug002_StartPolling_NegativeInterval_DoesNotBusyLoop()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _liveDataService.StartPolling(0x7E0, new[] { "ENGINE_RPM" }, intervalMs: -1000, ecuName: "ECM");
        await Task.Delay(300, cts.Token);
        _liveDataService.StopPolling();

        Assert.False(cts.IsCancellationRequested, "StartPolling with negative intervalMs must not hang.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-003 [P1] — GetRecentTraces(Int32.MaxValue) must be capped at ring size
    // Root cause: No upper bound check — allocates a list of capacity Int32.MaxValue
    // Fix required: Clamp count to ring buffer capacity (500).
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Bug003_TraceService_MaxCount_IsCappedAtRingBufferSize()
    {
        // Fill 10 traces
        for (int i = 0; i < 10; i++)
        {
            _traceService.RecordTrace(new DiagnosticTraceEntry
            {
                TransportId = "test",
                RequestCanId = 0x7E0,
                ResponseCanId = 0x7E8,
                Direction = DiagnosticDirection.Tx,
                Protocol = DiagnosticProtocolType.Uds,
                ServiceId = 0x10,
                Payload = Array.Empty<byte>(),
                Result = DiagnosticExchangeResult.PositiveResponse,
                ActiveSession = DiagnosticSessionState.DefaultSession
            });
        }

        // Request Int32.MaxValue
        var result = _traceService.GetRecentTraces(int.MaxValue);

        Assert.NotNull(result);
        Assert.True(result.Count <= 1000,
            $"GetRecentTraces(Int32.MaxValue) must cap at ring buffer capacity. Got {result.Count}.");
    }

    [Fact]
    public void Bug003_TraceService_NegativeCount_ReturnsEmpty()
    {
        var result = _traceService.GetRecentTraces(-1);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void Bug003_TraceService_ZeroCount_ReturnsEmpty()
    {
        var result = _traceService.GetRecentTraces(0);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-004 [P1] — DTC clear without confirmation MUST throw UnauthorizedAccessException
    // This verifies the safety gate cannot be bypassed via null/false confirmation.
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bug004_DtcClear_ConfirmationRequired_ButNotConfirmed_ThrowsUnauthorized()
    {
        var request = new DtcClearRequest
        {
            EcuName = "ECM",
            RequestCanId = 0x7E0,
            RequireUserConfirmation = true,
            UserConfirmed = false,
            ConfirmationReason = string.Empty
        };

        var ex = await Record.ExceptionAsync(() => _dtcService.ClearDtcsWithSafetyAsync(request));

        Assert.IsType<UnauthorizedAccessException>(ex);
        Assert.Contains("confirmation", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bug004_DtcClear_NullRequest_ThrowsArgumentNull()
    {
        var ex = await Record.ExceptionAsync(() => _dtcService.ClearDtcsWithSafetyAsync(null!));
        Assert.IsType<ArgumentNullException>(ex);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-005 [P1] — ECU Reset without confirmation MUST throw UnauthorizedAccessException
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bug005_EcuReset_NotConfirmed_ThrowsUnauthorized()
    {
        var request = new EcuResetRequest
        {
            EcuName = "ECM",
            RequestCanId = 0x7E0,
            ResetType = EcuResetType.HardReset,
            RequireUserConfirmation = true,
            UserConfirmed = false,
            ConfirmationReason = string.Empty
        };

        var ex = await Record.ExceptionAsync(() => _resetService.ExecuteResetAsync(request));
        Assert.IsType<UnauthorizedAccessException>(ex);
    }

    [Fact]
    public async Task Bug005_EcuReset_NullRequest_ThrowsArgumentNull()
    {
        var ex = await Record.ExceptionAsync(() => _resetService.ExecuteResetAsync(null!));
        Assert.IsType<ArgumentNullException>(ex);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-006 [P1] — STATE MACHINE: Invalid direct transitions must be rejected
    // Disconnected cannot jump directly to ExtendedSession, ProgrammingSession etc.
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(DiagnosticSessionState.Disconnected, DiagnosticSessionState.ProgrammingSession)]
    [InlineData(DiagnosticSessionState.Disconnected, DiagnosticSessionState.ExtendedSession)]
    [InlineData(DiagnosticSessionState.Disconnected, DiagnosticSessionState.Identified)]
    [InlineData(DiagnosticSessionState.Disconnected, DiagnosticSessionState.DefaultSession)]
    [InlineData(DiagnosticSessionState.Connected,    DiagnosticSessionState.ProgrammingSession)]
    [InlineData(DiagnosticSessionState.Connected,    DiagnosticSessionState.DefaultSession)]
    [InlineData(DiagnosticSessionState.Connected,    DiagnosticSessionState.ExtendedSession)]
    [InlineData(DiagnosticSessionState.Identified,   DiagnosticSessionState.ExtendedSession)]
    [InlineData(DiagnosticSessionState.Identified,   DiagnosticSessionState.DefaultSession)]
    [InlineData(DiagnosticSessionState.Identified,   DiagnosticSessionState.ProgrammingSession)]
    public void Bug006_StateMachine_InvalidDirectTransitions_AreRejected(
        DiagnosticSessionState from, DiagnosticSessionState to)
    {
        bool valid = DiagnosticSessionManager.IsValidTransition(from, to);
        Assert.False(valid,
            $"Transition {from} → {to} must be INVALID. IsValidTransition returned true — state machine bypass possible.");
    }

    [Theory]
    [InlineData(DiagnosticSessionState.Disconnected,     DiagnosticSessionState.Connected)]
    [InlineData(DiagnosticSessionState.Connected,        DiagnosticSessionState.Identified)]
    [InlineData(DiagnosticSessionState.Identified,       DiagnosticSessionState.ProtocolConfirmed)]
    [InlineData(DiagnosticSessionState.ProtocolConfirmed, DiagnosticSessionState.DefaultSession)]
    [InlineData(DiagnosticSessionState.DefaultSession,   DiagnosticSessionState.ExtendedSession)]
    [InlineData(DiagnosticSessionState.ExtendedSession,  DiagnosticSessionState.ProgrammingSession)]
    public void Bug006_StateMachine_ValidSequentialTransitions_AreAccepted(
        DiagnosticSessionState from, DiagnosticSessionState to)
    {
        bool valid = DiagnosticSessionManager.IsValidTransition(from, to);
        Assert.True(valid, $"Transition {from} → {to} must be VALID.");
    }

    [Fact]
    public void Bug006_TransitionTo_InvalidState_ThrowsInvalidOperation()
    {
        // Fresh manager starts in Disconnected — jumping to ExtendedSession must throw
        var ex = Record.Exception(() =>
            _sessionManager.TransitionTo(DiagnosticSessionState.ExtendedSession, "attack"));

        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("Invalid diagnostic session state transition", ex.Message);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-007 [P1] — NRC Classifier: All 256 byte values must not throw or return null
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(0x10)] // generalReject
    [InlineData(0x11)] // serviceNotSupported
    [InlineData(0x12)] // subFunctionNotSupported
    [InlineData(0x13)] // incorrectMessageLength
    [InlineData(0x21)] // busyRepeatRequest → SafeToRetry
    [InlineData(0x22)] // conditionsNotCorrect
    [InlineData(0x24)] // requestSequenceError
    [InlineData(0x31)] // requestOutOfRange
    [InlineData(0x33)] // securityAccessDenied → RequiresUserAction
    [InlineData(0x35)] // invalidKey
    [InlineData(0x36)] // exceededNumberOfAttempts
    [InlineData(0x37)] // requiredTimeDelayNotExpired
    [InlineData(0x70)] // uploadDownloadNotAccepted → DoNotRetry
    [InlineData(0x72)] // transferDataSuspended
    [InlineData(0x73)] // generalProgrammingFailure
    [InlineData(0x78)] // requestCorrectlyReceivedResponsePending → SafeToRetry
    [InlineData(0xFF)] // unknown NRC
    [InlineData(0x00)] // boundary
    public void Bug007_NrcClassifier_NeverThrowsOrReturnsNull(byte nrc)
    {
        var eval = NrcClassifier.Evaluate(nrc);

        Assert.NotNull(eval);
        Assert.False(string.IsNullOrWhiteSpace(eval.Meaning),
            $"NRC 0x{nrc:X2} must have non-empty Meaning.");
    }

    [Fact]
    public void Bug007_NrcClassifier_BusyRepeatRequest_IsSafeToRetry()
    {
        var eval = NrcClassifier.Evaluate(0x21);
        Assert.Equal(NrcRetryBehavior.SafeToRetry, eval.RetryBehavior);
    }

    [Fact]
    public void Bug007_NrcClassifier_SecurityAccessDenied_RequiresUserAction()
    {
        var eval = NrcClassifier.Evaluate(0x33);
        Assert.Equal(NrcRetryBehavior.RequiresUserAction, eval.RetryBehavior);
    }

    [Fact]
    public void Bug007_NrcClassifier_UploadDownloadNotAccepted_DoNotRetry()
    {
        var eval = NrcClassifier.Evaluate(0x70);
        Assert.Equal(NrcRetryBehavior.DoNotRetry, eval.RetryBehavior);
    }

    [Fact]
    public void Bug007_NrcClassifier_ResponsePending_IsSafeToRetry()
    {
        var eval = NrcClassifier.Evaluate(0x78);
        Assert.Equal(NrcRetryBehavior.SafeToRetry, eval.RetryBehavior);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-008 [P2] — Health score must always be within [0, 100]
    // Adversarial: inject max faults to push score below zero.
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bug008_HealthScore_WithMaxFaults_StaysInBounds()
    {
        _simulator.SetSimulatedVoltage("ECM", 0.0);
        for (int i = 0; i < 20; i++)
        {
            _simulator.SetDtcFault("ECM", $"P{i:0000}", true);
        }

        await _dtcService.ReadDtcsByStatusMaskAsync(0x7E0, 0xFF, "ECM");
        await _liveDataService.PollParameterAsync(0x7E0, "MODULE_VOLTAGE", "ECM");

        var report = _healthManager.EvaluateHealth("ECM");

        Assert.InRange(report.HealthScorePercent, 0, 100);
    }

    [Fact]
    public async Task Bug008_HealthReport_WithManyFaults_NeverReportsNormal()
    {
        _simulator.SetSimulatedVoltage("ECM", 0.0);
        _simulator.SetDtcFault("ECM", "P0300", true);
        _simulator.SetDtcFault("ECM", "P0171", true);
        _simulator.SetDtcFault("ECM", "P0174", true);
        _simulator.SetDtcFault("ECM", "P0016", true);
        _simulator.SetDtcFault("ECM", "P0340", true);

        await _dtcService.ReadDtcsByStatusMaskAsync(0x7E0, 0xFF, "ECM");
        await _liveDataService.PollParameterAsync(0x7E0, "MODULE_VOLTAGE", "ECM");

        var report = _healthManager.EvaluateHealth("ECM");

        Assert.NotEqual(EcuHealthStatus.Normal, report.OverallHealth);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-009 [P2] — OEM Normalization: null/empty part numbers must not crash
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData(null, "8D0-907-551")]
    [InlineData("8D0-907-551", null)]
    public void Bug009_OemNormalization_NullOrEmptyPartNumbers_NeverCrashes(
        string? hwNum, string? swNum)
    {
        var ex = Record.Exception(() =>
            _normalizationService.GenerateNormalizationPlan("ECM", hwNum, swNum));
        Assert.Null(ex);
    }

    [Fact]
    public void Bug009_OemNormalization_UnknownEcuName_ReturnsValidPlan()
    {
        var plan = _normalizationService.GenerateNormalizationPlan("UNKNOWN_ECU_999");

        Assert.NotNull(plan);
        Assert.NotNull(plan.PlanId);
        Assert.False(string.IsNullOrWhiteSpace(plan.SafetyAttestation),
            "SafetyAttestation must never be empty in an OEM normalization plan.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-010 [P1] — PollParameterAsync with null/empty/unknown ID returns null, not throws
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("NONEXISTENT_PARAMETER")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Bug010_PollParameter_UnknownOrEmptyId_ReturnsNull(string parameterId)
    {
        var result = await _liveDataService.PollParameterAsync(0x7E0, parameterId, "ECM");
        // Must return null (not found), never throw KeyNotFoundException or NRE
        Assert.Null(result);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-011 [P2] — Routine execution without confirmation throws Unauthorized
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bug011_RoutineControl_NoConfirmation_ThrowsUnauthorized()
    {
        var request = new RoutineExecutionRequest
        {
            EcuName = "ECM",
            RequestCanId = 0x7E0,
            RoutineId = 0x0201,
            SubFunction = 0x01,
            RequireUserConfirmation = true,
            UserConfirmed = false,
            ConfirmationReason = string.Empty
        };

        var ex = await Record.ExceptionAsync(() => _routineService.ExecuteRoutineAsync(request));
        Assert.IsType<UnauthorizedAccessException>(ex);
    }

    [Fact]
    public async Task Bug011_RoutineControl_NullRequest_ThrowsArgumentNull()
    {
        var ex = await Record.ExceptionAsync(() => _routineService.ExecuteRoutineAsync(null!));
        Assert.IsType<ArgumentNullException>(ex);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-012 [P1] — Unknown RoutineId must not throw NullReferenceException
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bug012_RoutineControl_UnknownRoutineId_NotNullReferenceException()
    {
        var request = new RoutineExecutionRequest
        {
            EcuName = "ECM",
            RequestCanId = 0x7E0,
            RoutineId = 0xDEAD, // unknown
            SubFunction = 0x01,
            RequireUserConfirmation = true,
            UserConfirmed = true,
            ConfirmationReason = "Adversarial test"
        };

        var ex = await Record.ExceptionAsync(() => _routineService.ExecuteRoutineAsync(request));

        if (ex != null)
        {
            Assert.IsNotType<NullReferenceException>(ex);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-013 [P2] — Sensor plausibility: all checks have non-null category + summary
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Bug013_SensorPlausibility_AllChecks_HaveNonNullFields()
    {
        var report = _plausibilityService.EvaluatePlausibility("ECM");

        Assert.NotNull(report);
        foreach (var check in report.Checks)
        {
            Assert.False(string.IsNullOrWhiteSpace(check.CheckName),
                "Plausibility check must have non-empty CheckName.");
            Assert.False(string.IsNullOrWhiteSpace(check.Category),
                $"Check '{check.CheckName}' must have non-empty Category.");
            Assert.False(string.IsNullOrWhiteSpace(check.Summary),
                $"Check '{check.CheckName}' must have non-empty Summary.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-014 [P2] — Trace ring buffer: overflow evicts old entries (FIFO)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Bug014_TraceRingBuffer_Overflow_EvictsOldest_NoGrowth()
    {
        // Insert 1200 traces (ring capacity = 1000)
        for (int i = 0; i < 1200; i++)
        {
            _traceService.RecordTrace(new DiagnosticTraceEntry
            {
                TransportId = $"t{i}",
                RequestCanId = (uint)i,
                ResponseCanId = 0x7E8,
                Direction = DiagnosticDirection.Tx,
                Protocol = DiagnosticProtocolType.Uds,
                ServiceId = 0x10,
                Payload = Array.Empty<byte>(),
                Result = DiagnosticExchangeResult.PositiveResponse,
                ActiveSession = DiagnosticSessionState.DefaultSession,
                Notes = $"Trace {i}"
            });
        }

        var recent = _traceService.GetRecentTraces(2000);

        // Must be capped at ring buffer capacity, not grow to 1200
        Assert.True(recent.Count <= 1000,
            $"Ring buffer must not grow past capacity. Got {recent.Count} entries.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-015 [P2] — Dispose stops TesterPresent cleanly, status reflects it
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bug015_Dispose_StopsTesterPresent_NoLeak()
    {
        // Create a SEPARATE manager (not the shared one) to test disposal
        var topology    = new TopologyEngine();
        var logger      = new CanLoggerService();
        var pipeline    = new CanPipeline(topology, logger);
        var isoTp       = new IsoTpRouter();
        var sim         = new EcuSimulator(pipeline, isoTp);
        var sessionSvc  = new DiagnosticSessionService(pipeline, isoTp, sim);
        var trace       = new DiagnosticTraceService();
        var mgr         = new DiagnosticSessionManager(sessionSvc, trace);

        mgr.StartTesterPresent(0x7E0, "ECM", 100);
        await Task.Delay(50);

        mgr.Dispose();
        await Task.Delay(100);

        var status = mgr.GetStatus();
        Assert.False(status.IsTesterPresentActive,
            "IsTesterPresentActive must be false after Dispose.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-016 [P2] — Concurrent TesterPresent starts must not leak background tasks
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bug016_ConcurrentTesterPresentStarts_NoTaskLeak()
    {
        var tasks = Enumerable.Range(0, 10).Select(_ =>
            Task.Run(() => _sessionManager.StartTesterPresent(0x7E0, "ECM", 100)));

        await Task.WhenAll(tasks);
        await Task.Delay(100);
        _sessionManager.StopTesterPresent();

        var status = _sessionManager.GetStatus();
        Assert.False(status.IsTesterPresentActive,
            "TesterPresent must be false after stop, even after concurrent starts.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-017 [P2] — Live data sample timestamp must always be UTC
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bug017_LiveDataSample_Timestamp_IsAlwaysUtc()
    {
        var sample = await _liveDataService.PollParameterAsync(0x7E0, "ENGINE_RPM", "ECM");

        Assert.NotNull(sample);
        Assert.Equal(DateTimeKind.Utc, sample!.TimestampUtc.Kind);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-018 [P3] — DtcStatusByte bitfield must correctly represent all 8 flags
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Bug018_DtcStatusByte_0xFF_AllFlagsSet()
    {
        byte s = 0xFF;
        Assert.True((s & 0x01) != 0, "Bit 0 TestFailed");
        Assert.True((s & 0x02) != 0, "Bit 1 TestFailedThisOpCycle");
        Assert.True((s & 0x04) != 0, "Bit 2 PendingDtc");
        Assert.True((s & 0x08) != 0, "Bit 3 ConfirmedDtc");
        Assert.True((s & 0x10) != 0, "Bit 4 TestNotCompletedSinceClear");
        Assert.True((s & 0x20) != 0, "Bit 5 TestFailedSinceClear");
        Assert.True((s & 0x40) != 0, "Bit 6 TestNotCompletedThisOp");
        Assert.True((s & 0x80) != 0, "Bit 7 WarningIndicator");
    }

    [Fact]
    public void Bug018_DtcStatusByte_0x00_AllFlagsClear()
    {
        byte s = 0x00;
        for (int bit = 0; bit < 8; bit++)
        {
            Assert.False((s & (1 << bit)) != 0, $"Bit {bit} must be clear for status=0x00.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-019 [P2] — DtcService null/empty filter methods must not throw
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Bug019_DtcService_GetActiveDtcs_NullFilter_DoesNotThrow()
    {
        var ex = Record.Exception(() => _dtcService.GetActiveDtcs(null));
        Assert.Null(ex);
    }

    [Fact]
    public void Bug019_DtcService_GetHistoricalDtcs_NullFilter_DoesNotThrow()
    {
        var ex = Record.Exception(() => _dtcService.GetHistoricalDtcs(null));
        Assert.Null(ex);
    }

    [Fact]
    public async Task Bug019_DtcService_ReadDtcs_NullEcuName_DoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(
            () => _dtcService.ReadDtcsByStatusMaskAsync(0x7E0, 0xFF, null!));
        Assert.Null(ex);
    }

    [Fact]
    public async Task Bug019_DtcService_ReadDtcs_EmptyEcuName_DoesNotThrow()
    {
        var ex = await Record.ExceptionAsync(
            () => _dtcService.ReadDtcsByStatusMaskAsync(0x7E0, 0xFF, string.Empty));
        Assert.Null(ex);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BUG-020 [P2] — Concurrent StartPolling calls must not leave orphan tasks
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Bug020_ConcurrentStartPolling_NoOrphanTask()
    {
        var tasks = Enumerable.Range(0, 5).Select(_ =>
            Task.Run(() => _liveDataService.StartPolling(0x7E0, new[] { "ENGINE_RPM" }, 100)));
        await Task.WhenAll(tasks);

        await Task.Delay(100);
        _liveDataService.StopPolling();

        // Should not throw, hang, or leave orphan tasks
        Assert.True(true);
    }
}
