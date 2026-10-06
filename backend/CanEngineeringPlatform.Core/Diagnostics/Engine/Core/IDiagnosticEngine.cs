using System;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Health;
using CanEngineeringPlatform.Core.Diagnostics.Engine.LiveData;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Normalization;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Plausibility;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Reset;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Routines;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Session;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Trace;

namespace CanEngineeringPlatform.Core.Diagnostics.Engine.Core;

public interface IDiagnosticEngine
{
    IDiagnosticSessionManager SessionManager { get; }
    IDtcService DtcService { get; }
    ILiveDataService LiveDataService { get; }
    ISensorPlausibilityService PlausibilityService { get; }
    IRoutineControlService RoutineService { get; }
    IEcuResetService ResetService { get; }
    IDiagnosticTraceService TraceService { get; }
    IEcuHealthManager HealthManager { get; }
    IOemNormalizationService NormalizationService { get; }
}

public class DiagnosticEngine : IDiagnosticEngine
{
    public IDiagnosticSessionManager SessionManager { get; }
    public IDtcService DtcService { get; }
    public ILiveDataService LiveDataService { get; }
    public ISensorPlausibilityService PlausibilityService { get; }
    public IRoutineControlService RoutineService { get; }
    public IEcuResetService ResetService { get; }
    public IDiagnosticTraceService TraceService { get; }
    public IEcuHealthManager HealthManager { get; }
    public IOemNormalizationService NormalizationService { get; }

    public DiagnosticEngine(
        IDiagnosticSessionManager sessionManager,
        IDtcService dtcService,
        ILiveDataService liveDataService,
        ISensorPlausibilityService plausibilityService,
        IRoutineControlService routineService,
        IEcuResetService resetService,
        IDiagnosticTraceService traceService,
        IEcuHealthManager healthManager,
        IOemNormalizationService normalizationService)
    {
        SessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        DtcService = dtcService ?? throw new ArgumentNullException(nameof(dtcService));
        LiveDataService = liveDataService ?? throw new ArgumentNullException(nameof(liveDataService));
        PlausibilityService = plausibilityService ?? throw new ArgumentNullException(nameof(plausibilityService));
        RoutineService = routineService ?? throw new ArgumentNullException(nameof(routineService));
        ResetService = resetService ?? throw new ArgumentNullException(nameof(resetService));
        TraceService = traceService ?? throw new ArgumentNullException(nameof(traceService));
        HealthManager = healthManager ?? throw new ArgumentNullException(nameof(healthManager));
        NormalizationService = normalizationService ?? throw new ArgumentNullException(nameof(normalizationService));
    }
}
