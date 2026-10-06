using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.Engine;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Core;
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
using CanEngineeringPlatform.Core.EcuEngineering.Identification;
using CanEngineeringPlatform.Core.EcuEngineering.Images;
using CanEngineeringPlatform.Core.EcuEngineering.Memory;
using CanEngineeringPlatform.Core.EcuEngineering.Reading;
using CanEngineeringPlatform.Core.Pipeline;

namespace CanEngineeringPlatform.Tests;

public class DiagnosticEnginePhase3Tests : IDisposable
{
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
    private readonly IDiagnosticEngine _engine;
    private readonly IEcuMemoryImageStore _imageStore;
    private readonly IEcuMemoryReader _memoryReader;

    public DiagnosticEnginePhase3Tests()
    {
        var topology = new CanEngineeringPlatform.Core.Topology.TopologyEngine();
        var logger = new CanEngineeringPlatform.Core.Logging.CanLoggerService();
        _pipeline = new CanPipeline(topology, logger);
        _isoTpRouter = new IsoTpRouter();
        _simulator = new EcuSimulator(_pipeline, _isoTpRouter);
        _sessionService = new DiagnosticSessionService(_pipeline, _isoTpRouter, _simulator);
        _traceService = new DiagnosticTraceService(1000);
        _sessionManager = new DiagnosticSessionManager(_sessionService, _traceService);
        _dtcService = new DtcService(_sessionService, _sessionManager, _traceService);
        _liveDataService = new LiveDataService(_sessionService, _sessionManager, _traceService);
        _plausibilityService = new SensorPlausibilityService(_liveDataService);
        _routineService = new RoutineControlService(_sessionService, _sessionManager, _traceService);

        _imageStore = new EcuMemoryImageStore();
        _memoryReader = new EcuMemoryReader(_sessionService, _imageStore);
        _resetService = new EcuResetService(_sessionService, _sessionManager, _traceService, _memoryReader);

        _healthManager = new EcuHealthManager(_dtcService, _liveDataService, _plausibilityService, _traceService);
        _compatibilityManager = new EcuCompatibilityManager();
        _normalizationService = new OemNormalizationService(_compatibilityManager, _liveDataService, _dtcService, _plausibilityService, _healthManager);

        _engine = new DiagnosticEngine(
            _sessionManager,
            _dtcService,
            _liveDataService,
            _plausibilityService,
            _routineService,
            _resetService,
            _traceService,
            _healthManager,
            _normalizationService);
    }

    public void Dispose()
    {
        _sessionManager.Dispose();
        _liveDataService.Dispose();
    }

    // =========================================================================
    // 1. Session Manager & UDS 0x10 State Machine Tests
    // =========================================================================

    [Fact]
    public void SessionStateMachine_ValidSequentialTransitions_Succeed()
    {
        Assert.Equal(DiagnosticSessionState.Disconnected, _sessionManager.GetStatus().CurrentState);

        Assert.True(_sessionManager.TransitionTo(DiagnosticSessionState.Connected, "Physical link active"));
        Assert.True(_sessionManager.TransitionTo(DiagnosticSessionState.Identified, "ECU ID acquired"));
        Assert.True(_sessionManager.TransitionTo(DiagnosticSessionState.ProtocolConfirmed, "UDS verified"));
        Assert.True(_sessionManager.TransitionTo(DiagnosticSessionState.DefaultSession, "Entered default session 0x01"));
        Assert.True(_sessionManager.TransitionTo(DiagnosticSessionState.ExtendedSession, "Requested 0x03 Extended"));
        Assert.True(_sessionManager.TransitionTo(DiagnosticSessionState.DefaultSession, "Returned to 0x01"));
    }

    [Fact]
    public void SessionStateMachine_IllegalTransition_ThrowsInvalidOperationException()
    {
        Assert.Equal(DiagnosticSessionState.Disconnected, _sessionManager.GetStatus().CurrentState);

        // Disconnected -> ExtendedSession is illegal
        Assert.Throws<InvalidOperationException>(() =>
            _sessionManager.TransitionTo(DiagnosticSessionState.ExtendedSession, "Illegal jump"));

        Assert.Equal(DiagnosticSessionState.Disconnected, _sessionManager.GetStatus().CurrentState);
    }

    [Fact]
    public async Task SessionManager_SwitchSessionToExtended_ReceivesPositiveResponseAndSetsP2Timings()
    {
        // Setup state to DefaultSession
        _sessionManager.TransitionTo(DiagnosticSessionState.Connected, "Conn");
        _sessionManager.TransitionTo(DiagnosticSessionState.Identified, "ID");
        _sessionManager.TransitionTo(DiagnosticSessionState.ProtocolConfirmed, "Proto");
        _sessionManager.TransitionTo(DiagnosticSessionState.DefaultSession, "Default");

        bool success = await _sessionManager.SwitchSessionAsync(0x7E0, DiagnosticSessionType.ExtendedDiagnosticSession, "ECM");

        Assert.True(success);
        var status = _sessionManager.GetStatus();
        Assert.Equal(DiagnosticSessionState.ExtendedSession, status.CurrentState);
        Assert.Equal(DiagnosticSessionType.ExtendedDiagnosticSession, status.ActiveUdsSession);
        Assert.True(status.P2ServerMs > 0);
        Assert.True(status.P2StarServerMs > 0);
        Assert.True(status.IsTesterPresentActive); // TesterPresent should automatically start for extended session
    }

    [Fact]
    public void SessionManager_TesterPresent_StartsAndStopsCleanly()
    {
        _sessionManager.StartTesterPresent(0x7E0, "ECM", intervalMs: 100);
        var status = _sessionManager.GetStatus();
        Assert.True(status.IsTesterPresentActive);

        _sessionManager.StopTesterPresent();
        status = _sessionManager.GetStatus();
        Assert.False(status.IsTesterPresentActive);
    }

    // =========================================================================
    // 2. DTC Engine & 0x14 Safe Clearing Tests
    // =========================================================================

    [Fact]
    public async Task DtcService_ReadDtcsByStatusMask_ReturnsStructuredDtcsWith8BitFlags()
    {
        var dtcs = await _dtcService.ReadDtcsByStatusMaskAsync(0x7E0, 0xFF, "ECM");

        Assert.NotNull(dtcs);
        Assert.NotEmpty(dtcs);

        var p0300 = dtcs.FirstOrDefault(d => d.Code.StartsWith("P0300"));
        Assert.NotNull(p0300);
        Assert.True(p0300.TestFailed);
        Assert.True(p0300.PendingDtc);
        Assert.True(p0300.ConfirmedDtc);
        Assert.Contains("CONFIRMED (0x08)", p0300.FormattedFlags);
        Assert.Equal("SAE J2012 / OEM Diagnostic Catalog", p0300.DescriptionSource);
    }

    [Fact]
    public async Task DtcService_ClearWithoutUserConfirmation_ThrowsUnauthorizedAccessException()
    {
        var request = new DtcClearRequest
        {
            EcuName = "ECM",
            RequestCanId = 0x7E0,
            RequireUserConfirmation = true,
            UserConfirmed = false // Confirmation omitted
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _dtcService.ClearDtcsWithSafetyAsync(request));
    }

    [Fact]
    public async Task DtcService_ClearWithConfirmation_ClearsDtcsAndPreservesAuditHistory()
    {
        // Confirm pre-clear DTCs exist
        var preDtcs = await _dtcService.ReadDtcsByStatusMaskAsync(0x7E0, 0xFF, "ECM");
        Assert.NotEmpty(preDtcs);

        var request = new DtcClearRequest
        {
            EcuName = "ECM",
            RequestCanId = 0x7E0,
            GroupOfDtc = 0xFFFFFF,
            RequireUserConfirmation = true,
            UserConfirmed = true,
            ConfirmationReason = "Verified cylinder 1 spark plug replacement completed"
        };

        var result = await _dtcService.ClearDtcsWithSafetyAsync(request);

        Assert.True(result.Success);
        Assert.NotEmpty(result.PreClearDtcs);
        Assert.NotEmpty(result.ClearedCodes);

        // Verify history is permanently preserved
        var clearHistory = _dtcService.GetClearHistory("ECM");
        Assert.NotEmpty(clearHistory);
        Assert.Equal(result.OperationId, clearHistory[0].OperationId);

        var historicalDtcs = _dtcService.GetHistoricalDtcs("ECM");
        Assert.NotEmpty(historicalDtcs);
    }

    // =========================================================================
    // 3. Live Data Engine & Data Quality Tests
    // =========================================================================

    [Fact]
    public void LiveDataService_DefaultDefinitions_AreRegistered()
    {
        var defs = _liveDataService.GetSupportedDefinitions();

        Assert.Contains(defs, d => d.Id == "ENGINE_RPM");
        Assert.Contains(defs, d => d.Id == "COOLANT_TEMP");
        Assert.Contains(defs, d => d.Id == "MODULE_VOLTAGE");
        Assert.Contains(defs, d => d.Id == "THROTTLE_POS");
        Assert.Contains(defs, d => d.Id == "MASS_AIR_FLOW");
        Assert.Contains(defs, d => d.Id == "LAMBDA");
    }

    [Fact]
    public void LiveDataService_OutOfRangeValue_TaggedWithOutOfRangeQuality()
    {
        var sample = new LiveDataSample
        {
            ParameterId = "MODULE_VOLTAGE",
            Name = "Battery / System Voltage",
            Category = "Electrical",
            ScaledValue = 24.5, // Impossibly high for 12V automotive bus (max expected 16.5V)
            Unit = "V",
            TimestampUtc = DateTime.UtcNow
        };

        _liveDataService.IngestSample(sample);

        var retrieved = _liveDataService.GetSample("MODULE_VOLTAGE");
        Assert.NotNull(retrieved);
        Assert.Equal(DataQuality.OutOfRange, retrieved.Quality);
        Assert.Contains("outside physical bounds", retrieved.QualityReason);
    }

    [Fact]
    public async Task LiveDataService_PollParameter_ReadsFromSimulatedEcuSuccessfully()
    {
        var rpmSample = await _liveDataService.PollParameterAsync(0x7E0, "ENGINE_RPM", "ECM");

        Assert.NotNull(rpmSample);
        Assert.Equal("ENGINE_RPM", rpmSample.ParameterId);
        Assert.Equal(DataQuality.Valid, rpmSample.Quality);
        Assert.Equal(2000.0, rpmSample.ScaledValue);
        Assert.Equal("RPM", rpmSample.Unit);
    }

    // =========================================================================
    // 4. Sensor Plausibility Engine Tests
    // =========================================================================

    [Fact]
    public void SensorPlausibility_NominalValues_EvaluatesToPlausible()
    {
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "ENGINE_RPM", ScaledValue = 800, TimestampUtc = DateTime.UtcNow });
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "MODULE_VOLTAGE", ScaledValue = 14.1, TimestampUtc = DateTime.UtcNow });
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "COOLANT_TEMP", ScaledValue = 89, TimestampUtc = DateTime.UtcNow });
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "INTAKE_AIR_TEMP", ScaledValue = 24, TimestampUtc = DateTime.UtcNow });
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "THROTTLE_POS", ScaledValue = 2.0, TimestampUtc = DateTime.UtcNow });
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "MASS_AIR_FLOW", ScaledValue = 3.5, TimestampUtc = DateTime.UtcNow });
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "LAMBDA", ScaledValue = 1.00, TimestampUtc = DateTime.UtcNow });
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "STFT", ScaledValue = 1.5, TimestampUtc = DateTime.UtcNow });
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "LTFT", ScaledValue = -2.0, TimestampUtc = DateTime.UtcNow });

        var report = _plausibilityService.EvaluatePlausibility("ECM");

        Assert.Equal(SensorPlausibilityState.Plausible, report.OverallState);
        Assert.Equal(0, report.AnomalyCount);
        Assert.True(report.PlausibleCount >= 4);
    }

    [Fact]
    public void SensorPlausibility_EngineRunningWithLowVoltage_DetectsInconsistentCharging()
    {
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "ENGINE_RPM", ScaledValue = 1800, TimestampUtc = DateTime.UtcNow });
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "MODULE_VOLTAGE", ScaledValue = 11.2, TimestampUtc = DateTime.UtcNow }); // Alternator not charging

        var report = _plausibilityService.EvaluatePlausibility("ECM");

        var chargingCheck = report.Checks.FirstOrDefault(c => c.Category == "Electrical");
        Assert.NotNull(chargingCheck);
        Assert.Equal(SensorPlausibilityState.Inconsistent, chargingCheck.State);
        Assert.Contains("Alternator not charging", chargingCheck.Summary);
    }

    [Fact]
    public void SensorPlausibility_OverheatingCoolant_DetectsOutOfRange()
    {
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "COOLANT_TEMP", ScaledValue = 122.0, TimestampUtc = DateTime.UtcNow }); // Dangerously hot

        var report = _plausibilityService.EvaluatePlausibility("ECM");

        var thermalCheck = report.Checks.FirstOrDefault(c => c.Category == "Thermal");
        Assert.NotNull(thermalCheck);
        Assert.Equal(SensorPlausibilityState.OutOfRange, thermalCheck.State);
        Assert.Contains("overheating", thermalCheck.Summary);
    }

    // =========================================================================
    // 5. Routine Control Tests (UDS 0x31)
    // =========================================================================

    [Fact]
    public void RoutineControl_SupportedRoutines_ContainDocumentedActuators()
    {
        var routines = _routineService.GetSupportedRoutines();

        Assert.Contains(routines, r => r.RoutineId == 0x0201 && r.Name.Contains("EVAP"));
        Assert.Contains(routines, r => r.RoutineId == 0x0202 && r.Name.Contains("Fuel Pump"));
        Assert.Contains(routines, r => r.RoutineId == 0x0203 && r.Name.Contains("Cooling Fan"));
    }

    [Fact]
    public async Task RoutineControl_UnconfirmedExecution_ThrowsUnauthorizedAccessException()
    {
        var request = new RoutineExecutionRequest
        {
            RoutineId = 0x0202,
            RequireUserConfirmation = true,
            UserConfirmed = false
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _routineService.ExecuteRoutineAsync(request));
    }

    [Fact]
    public async Task RoutineControl_ExecuteDocumentedRoutine_AwaitsPositiveResponse()
    {
        var request = new RoutineExecutionRequest
        {
            EcuName = "ECM",
            RequestCanId = 0x7E0,
            RoutineId = 0x0201, // EVAP Purge
            SubFunction = 0x01,  // StartRoutine
            RequireUserConfirmation = true,
            UserConfirmed = true,
            ConfirmationReason = "Verify purge solenoid clicking on bench"
        };

        var result = await _routineService.ExecuteRoutineAsync(request);

        Assert.True(result.Success);
        Assert.Equal(RoutineExecutionState.Completed, result.State);
        Assert.NotEmpty(result.ResponsePayload);
        Assert.Equal(0x71, result.ResponsePayload[0]); // UDS 0x71 positive response
    }

    // =========================================================================
    // 6. ECU Reset Tests (UDS 0x11)
    // =========================================================================

    [Fact]
    public async Task EcuReset_UnconfirmedRequest_ThrowsUnauthorizedAccessException()
    {
        var req = new EcuResetRequest
        {
            EcuName = "ECM",
            RequestCanId = 0x7E0,
            ResetType = EcuResetType.SoftReset,
            RequireUserConfirmation = true,
            UserConfirmed = false
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _resetService.ExecuteResetAsync(req));
    }

    [Fact]
    public async Task EcuReset_WithConfirmation_ExecutesResetAndRestoresDefaultSession()
    {
        var req = new EcuResetRequest
        {
            EcuName = "ECM",
            RequestCanId = 0x7E0,
            ResetType = EcuResetType.SoftReset,
            RequireUserConfirmation = true,
            UserConfirmed = true,
            ConfirmationReason = "Post-coding module reboot"
        };

        var result = await _resetService.ExecuteResetAsync(req);

        Assert.True(result.Success);
        Assert.True(result.CommunicationRestored);
        Assert.Equal(DiagnosticSessionState.DefaultSession, _sessionManager.GetStatus().CurrentState);

        var history = _resetService.GetResetHistory();
        Assert.NotEmpty(history);
        Assert.Equal(result.ResetId, history[0].ResetId);
    }

    // =========================================================================
    // 7. Diagnostic Trace & NRC Classification Tests
    // =========================================================================

    [Fact]
    public void DiagnosticTrace_RecordsAndFiltersEntries()
    {
        _traceService.ClearTraces();

        _traceService.RecordTrace(new DiagnosticTraceEntry
        {
            TransportId = "can-fd",
            RequestCanId = 0x7E0,
            ResponseCanId = 0x7E8,
            Direction = DiagnosticDirection.Tx,
            Protocol = DiagnosticProtocolType.Uds,
            ServiceId = 0x22,
            Payload = new byte[] { 0x22, 0xF1, 0x90 },
            Result = DiagnosticExchangeResult.PositiveResponse
        });

        _traceService.RecordTrace(new DiagnosticTraceEntry
        {
            TransportId = "can-fd",
            RequestCanId = 0x7E0,
            ResponseCanId = 0x7E8,
            Direction = DiagnosticDirection.Rx,
            Protocol = DiagnosticProtocolType.Uds,
            ServiceId = 0x22,
            Payload = new byte[] { 0x7F, 0x22, 0x31 },
            Result = DiagnosticExchangeResult.NegativeResponse,
            NegativeResponseCode = 0x31
        });

        var traces = _traceService.GetRecentTraces();
        Assert.Equal(2, traces.Count);

        var negTraces = _traceService.SearchTraces(result: DiagnosticExchangeResult.NegativeResponse);
        Assert.Single(negTraces);
        Assert.Equal((byte)0x31, negTraces[0].NegativeResponseCode);
    }

    [Theory]
    [InlineData(0x11, NrcRetryBehavior.DoNotRetry)]
    [InlineData(0x21, NrcRetryBehavior.SafeToRetry)]
    [InlineData(0x22, NrcRetryBehavior.RequiresUserAction)]
    [InlineData(0x33, NrcRetryBehavior.RequiresUserAction)]
    [InlineData(0x78, NrcRetryBehavior.SafeToRetry)]
    public void NrcClassifier_EvaluatesRetryBehaviorAccurately(byte nrc, NrcRetryBehavior expected)
    {
        var eval = NrcClassifier.Evaluate(nrc);
        Assert.Equal(expected, eval.RetryBehavior);
        Assert.False(string.IsNullOrWhiteSpace(eval.Meaning));
        Assert.False(string.IsNullOrWhiteSpace(eval.RecommendedDiagnosticAction));
    }

    // =========================================================================
    // 8. ECU Health & Root-Cause Assistant Tests
    // =========================================================================

    [Fact]
    public async Task EcuHealth_WithActiveFaultsAndAnomalies_IdentifiesRootCauseCandidates()
    {
        // Seed active misfire DTC in simulator
        _simulator.SetDtcFault("ECM", "P0300", true);
        await _dtcService.ReadDtcsByStatusMaskAsync(0x7E0, 0xFF, "ECM");

        // Ingest low voltage while engine running
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "ENGINE_RPM", ScaledValue = 1800, TimestampUtc = DateTime.UtcNow });
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "MODULE_VOLTAGE", ScaledValue = 11.4, TimestampUtc = DateTime.UtcNow });

        var health = _healthManager.EvaluateHealth("ECM");

        Assert.Equal(EcuHealthStatus.Fault, health.OverallHealth);
        Assert.True(health.HealthScorePercent < 80.0);
        Assert.NotEmpty(health.HealthObservations);
        Assert.NotEmpty(health.RootCauseCandidates);

        var misfireCandidate = health.RootCauseCandidates.FirstOrDefault(c => c.SuspectComponent.Contains("Ignition"));
        Assert.NotNull(misfireCandidate);
        Assert.Equal(EvidenceConfidence.Confirmed, misfireCandidate.Confidence);
        Assert.NotEmpty(misfireCandidate.RecommendedPhysicalChecks);
    }

    // =========================================================================
    // 9. OEM Normalization Workflow Tests
    // =========================================================================

    [Fact]
    public void OemNormalization_GeneratesStructuredPlanWithSafetyAttestation()
    {
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "MODULE_VOLTAGE", ScaledValue = 11.2, TimestampUtc = DateTime.UtcNow }); // Discrepancy
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "COOLANT_TEMP", ScaledValue = 88.0, TimestampUtc = DateTime.UtcNow }); // In spec
        _liveDataService.IngestSample(new LiveDataSample { ParameterId = "ENGINE_RPM", ScaledValue = 750.0, TimestampUtc = DateTime.UtcNow }); // In spec

        var plan = _normalizationService.GenerateNormalizationPlan("ECM", "HW-ECM-8920A", "v4.2.1-prod");

        Assert.NotNull(plan);
        Assert.NotEmpty(plan.EvaluatedParameters);
        Assert.NotEmpty(plan.Discrepancies);
        Assert.False(plan.MeetsAllOemTargets);
        Assert.NotEmpty(plan.RecommendedServiceActions);
        Assert.Contains("preserves original calibration", plan.SafetyAttestation);
    }

    // =========================================================================
    // 10. Security & Boundary Guard Tests
    // =========================================================================

    [Fact]
    public void Security_SimulatorNrcOverride_ReturnsExactOverriddenNrcWithoutCrash()
    {
        _simulator.SetNrcOverride(0x7E0, (byte)UdsServiceId.ReadDataByIdentifier, 0x33); // Override with SecurityAccessDenied (0x33)

        var resp = _simulator.ProcessDiagnosticRequest(0x7E0, new byte[] { (byte)UdsServiceId.ReadDataByIdentifier, 0xF1, 0x90 });

        Assert.NotNull(resp);
        Assert.Equal(3, resp.Length);
        Assert.Equal(0x7F, resp[0]);
        Assert.Equal((byte)UdsServiceId.ReadDataByIdentifier, resp[1]);
        Assert.Equal(0x33, resp[2]); // SecurityAccessDenied

        _simulator.ClearNrcOverrides();
    }
}
