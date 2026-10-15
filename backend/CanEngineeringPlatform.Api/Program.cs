using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using CanEngineeringPlatform.Api.Hubs;
using CanEngineeringPlatform.Api.Services;
using CanEngineeringPlatform.Core.Calibration;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.Dyno;
using CanEngineeringPlatform.Core.Engine;
using CanEngineeringPlatform.Core.FlashAnalysis;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Hardware.Transports;
using CanEngineeringPlatform.Core.LiveEcuData;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Replay;
using CanEngineeringPlatform.Core.Sessions;
using CanEngineeringPlatform.Core.Signals;
using CanEngineeringPlatform.Core.Signals.Calculated;
using CanEngineeringPlatform.Core.Topology;
using CanEngineeringPlatform.Core.Analysis.Comparison;
using CanEngineeringPlatform.Core.Analysis.Import;
using CanEngineeringPlatform.Core.Analysis.OperatingEnvelope;
using CanEngineeringPlatform.Core.Analysis.Processing;
using CanEngineeringPlatform.Core.Analysis.Validation;
using CanEngineeringPlatform.Core.Vehicles;
using CanEngineeringPlatform.Core.Diagnostics.FreezeFrame;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.Manufacturer;
using CanEngineeringPlatform.Core.Diagnostics.Vag;
using CanEngineeringPlatform.Core.Dashboard;
using CanEngineeringPlatform.Core.Reporting;
using CanEngineeringPlatform.Core.EcuEngineering.Compatibility;
using CanEngineeringPlatform.Core.EcuEngineering.Core;
using CanEngineeringPlatform.Core.EcuEngineering.Identification;
using CanEngineeringPlatform.Core.EcuEngineering.Protocols;
using CanEngineeringPlatform.Core.EcuEngineering.Memory;
using CanEngineeringPlatform.Core.EcuEngineering.Images;
using CanEngineeringPlatform.Core.EcuEngineering.Reading;
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
using CanEngineeringPlatform.Core.Projects;
using CanEngineeringPlatform.Core.Sessions.Timeline;
using CanEngineeringPlatform.Core.Analysis.ReverseEngineering;
using CanEngineeringPlatform.Core.Simulation.DigitalTwin;
using CanEngineeringPlatform.Core.Hardware.Hil;
using CanEngineeringPlatform.Core.Protocols.Asam;
using CanEngineeringPlatform.Core.EcuEngineering.Programming;
using CanEngineeringPlatform.Core.Research;
using CanEngineeringPlatform.Core.Ai;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Register Core CAN Services (Singletons)
builder.Services.AddSingleton<IUnifiedSignalRegistry, UnifiedSignalRegistry>();
builder.Services.AddSingleton<ILiveOperatingGridService, LiveOperatingGridService>();
builder.Services.AddSingleton<ITopologyEngine, TopologyEngine>();
builder.Services.AddSingleton<ICanLoggerService, CanLoggerService>();
builder.Services.AddSingleton<ICanPipeline, CanPipeline>();
builder.Services.AddSingleton<ICanReplayService, CanReplayService>();

// Register Hardware Transports and Manager
builder.Services.AddSingleton<Esp8266Transport>();
builder.Services.AddSingleton<WaveshareCanFdTransport>();
builder.Services.AddSingleton<SimulationTransport>();
builder.Services.AddSingleton<Elm327Transport>();
builder.Services.AddSingleton<Esp8266UsbSerialTransport>();
builder.Services.AddSingleton<GvretSerialTransport>();
builder.Services.AddSingleton<IHardwareManager>(sp =>
{
    var pipeline = sp.GetRequiredService<ICanPipeline>();
    var esp = sp.GetRequiredService<Esp8266Transport>();
    var ws = sp.GetRequiredService<WaveshareCanFdTransport>();
    var sim = sp.GetRequiredService<SimulationTransport>();
    var elm = sp.GetRequiredService<Elm327Transport>();
    var usbEsp = sp.GetRequiredService<Esp8266UsbSerialTransport>();
    var gvret = sp.GetRequiredService<GvretSerialTransport>();
    return new HardwareManager(pipeline, new ICanTransport[] { gvret, usbEsp, ws, sim, elm, esp });
});

// Register Diagnostic Services (Phase 3)
builder.Services.AddSingleton<IIsoTpRouter, IsoTpRouter>();
builder.Services.AddSingleton<IEcuSimulator, EcuSimulator>();
builder.Services.AddSingleton<IDiagnosticSessionService, DiagnosticSessionService>();

// Register Engine, Dyno & Calibration Services (Phase 4)
builder.Services.AddSingleton<IEngineSimulator, EngineSimulator>();
builder.Services.AddSingleton<IDynoAnalysisService, DynoAnalysisService>();
builder.Services.AddSingleton<ICalibrationService, CalibrationService>();
builder.Services.AddSingleton<IEngineCanBridgeService, EngineCanBridgeService>();

// Register Live ECU Data & Read-Only Flash Analyzer Services (Phase 5)
builder.Services.AddSingleton<ILiveEcuDataService, LiveEcuDataService>();
builder.Services.AddSingleton<IFlashAnalyzerService, FlashAnalyzerService>();

// Register Vehicle Profile, Calculated Signals & Session Services
builder.Services.AddSingleton<IVehicleProfileService, VehicleProfileService>();
builder.Services.AddSingleton<ICalculatedSignalEngine, CalculatedSignalEngine>();
builder.Services.AddSingleton<ISessionManagementService, SessionManagementService>();

// Register Log Analysis, Validation, Processing and Comparison Services
builder.Services.AddSingleton<ILogImportService, LogImportService>();
builder.Services.AddSingleton<LogValidationEngine>();
builder.Services.AddSingleton<SignalProcessingEngine>();
builder.Services.AddSingleton<RunComparisonService>();

// Register Extended Diagnostics, Mappacks & Dashboard Services
builder.Services.AddSingleton<IManufacturerLiveDataService, ManufacturerLiveDataService>();
builder.Services.AddSingleton<IVagMeasuringBlockService, VagMeasuringBlockService>();
builder.Services.AddSingleton<IFreezeFrameSnapshotService, FreezeFrameSnapshotService>();
builder.Services.AddSingleton<IDtcCorrelationService, DtcCorrelationService>();
builder.Services.AddSingleton<IEcuDefinitionPackService, EcuDefinitionPackService>();
builder.Services.AddSingleton<IDashboardDesignerService, DashboardDesignerService>();

// Register Centralized PDF Reporting Service
builder.Services.AddSingleton<IReportService, ReportService>();

// Register ECU Engineering Core & Safe Read Services (Phase 1 & Phase 2)
builder.Services.AddSingleton<IEcuCompatibilityManager, EcuCompatibilityManager>();
builder.Services.AddSingleton<IProtocolDiscoveryService, ProtocolDiscoveryService>();
builder.Services.AddSingleton<IEcuIdentificationService, EcuIdentificationService>();
builder.Services.AddSingleton<IMemoryDiscoveryService, MemoryDiscoveryService>();
builder.Services.AddSingleton<IEcuMemoryImageStore, EcuMemoryImageStore>();
builder.Services.AddSingleton<IEcuMemoryReader, EcuMemoryReader>();
builder.Services.AddSingleton<IEcuEngineeringProvider, EcuEngineeringProvider>();

// Register Phase 3 Diagnostic Engine Services
builder.Services.AddSingleton<IDiagnosticTraceService, DiagnosticTraceService>();
builder.Services.AddSingleton<IDiagnosticSessionManager, DiagnosticSessionManager>();
builder.Services.AddSingleton<IDtcService, DtcService>();
builder.Services.AddSingleton<ILiveDataService, LiveDataService>();
builder.Services.AddSingleton<ISensorPlausibilityService, SensorPlausibilityService>();
builder.Services.AddSingleton<IRoutineControlService, RoutineControlService>();
builder.Services.AddSingleton<IEcuResetService, EcuResetService>();
builder.Services.AddSingleton<IEcuHealthManager, EcuHealthManager>();
builder.Services.AddSingleton<IOemNormalizationService, OemNormalizationService>();
builder.Services.AddSingleton<IDiagnosticEngine, DiagnosticEngine>();

// Register Productization & Complete Automotive Engineering Services
builder.Services.AddSingleton<IProjectService, ProjectService>();
builder.Services.AddSingleton<IUnifiedTimelineService, UnifiedTimelineService>();
builder.Services.AddSingleton<IOneClickEngineeringWorkflow, OneClickEngineeringWorkflow>();
builder.Services.AddSingleton<ICanReverseEngineeringService, CanReverseEngineeringService>();
builder.Services.AddSingleton<IVehicleDigitalTwinService, VehicleDigitalTwinService>();
builder.Services.AddSingleton<IHilService, HilService>();
builder.Services.AddSingleton<IAsamProtocolService, AsamProtocolService>();
builder.Services.AddSingleton<IEcuProgrammingService, EcuProgrammingService>();
builder.Services.AddSingleton<IResearchKnowledgeService, ResearchKnowledgeService>();
builder.Services.AddSingleton<IAiCopilotService, AiCopilotService>();

// Register Background Gateway and Broadcaster Services
builder.Services.AddSingleton<GatewayClientService>();
builder.Services.AddSingleton<IGatewayService>(sp => sp.GetRequiredService<GatewayClientService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<GatewayClientService>());
builder.Services.AddHostedService<SignalRBroadcasterService>();

var app = builder.Build();

app.UseCors("AllowFrontend");

// Serve frontend static files from wwwroot or frontend/dist if present
string[] candidatePaths = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), "frontend/dist"),
    Path.Combine(Directory.GetCurrentDirectory(), "../../frontend/dist"),
    Path.Combine(Directory.GetCurrentDirectory(), "../frontend/dist"),
    Path.Combine(AppContext.BaseDirectory, "frontend/dist"),
    Path.Combine(AppContext.BaseDirectory, "../../../../frontend/dist"),
    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
};

string distPath = candidatePaths.FirstOrDefault(p => Directory.Exists(p) && File.Exists(Path.Combine(p, "index.html")))
    ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

if (Directory.Exists(distPath))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new PhysicalFileProvider(Path.GetFullPath(distPath))
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Path.GetFullPath(distPath))
    });
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseRouting();
app.MapControllers();
app.MapHub<CanHub>("/hubs/can");

// Fallback to index.html for Single Page Application routing
if (Directory.Exists(distPath) && File.Exists(Path.Combine(distPath, "index.html")))
{
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(Path.GetFullPath(distPath))
    });
}

// Eagerly activate live stream and diagnostic services at startup
app.Services.GetRequiredService<ILiveEcuDataService>();
app.Services.GetRequiredService<IDiagnosticSessionService>();
app.Services.GetRequiredService<IEcuIdentificationService>();

app.Run();
