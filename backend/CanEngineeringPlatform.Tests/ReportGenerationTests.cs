using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using CanEngineeringPlatform.Core.Calibration;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.FreezeFrame;
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
using CanEngineeringPlatform.Core.Reporting;
using CanEngineeringPlatform.Core.Sessions;
using CanEngineeringPlatform.Core.Topology;
using CanEngineeringPlatform.Core.Vehicles;

namespace CanEngineeringPlatform.Tests;

public class ReportGenerationTests
{
    private readonly IReportService _reportService;
    private readonly IVehicleProfileService _vehicleService;
    private readonly IDynoAnalysisService _dynoService;

    public ReportGenerationTests()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        var pipeline = new CanPipeline(topology, logger);
        var isoTp = new IsoTpRouter();
        var sim = new EcuSimulator(pipeline, isoTp);
        var simTransport = new SimulationTransport();
        var hwManager = new HardwareManager(pipeline, new ICanTransport[] { simTransport });

        var diagSession = new DiagnosticSessionService(pipeline, isoTp, sim, hwManager);
        var liveData = new LiveEcuDataService(pipeline, diagSession);
        var flashService = new FlashAnalyzerService(liveData);
        var mappackService = new EcuDefinitionPackService();

        _vehicleService = new VehicleProfileService();
        var sessionService = new SessionManagementService(_vehicleService, hwManager, logger);
        var engineSim = new EngineSimulator();
        _dynoService = new DynoAnalysisService(engineSim);

        var dtcCorr = new DtcCorrelationService();
        var freezeFrame = new FreezeFrameSnapshotService();

        _reportService = new ReportService(
            _vehicleService,
            diagSession,
            _dynoService,
            flashService,
            mappackService,
            sessionService,
            hwManager,
            dtcCorr,
            freezeFrame);
    }

    [Fact]
    public async Task GenerateDiagnosticReportPdfAsync_ProducesValidPdf()
    {
        byte[] pdfBytes = await _reportService.GenerateDiagnosticReportPdfAsync(
            notes: "Test technician notes: Intake smoke test passed. No vacuum leaks found.");

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 1000, $"PDF size ({pdfBytes.Length} bytes) is suspiciously small");

        // Verify PDF Magic Bytes: %PDF-
        string header = Encoding.ASCII.GetString(pdfBytes.AsSpan(0, 5));
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void BuildDiagnosticReport_ContainsAllRequiredSections()
    {
        var def = _reportService.BuildDiagnosticReport(notes: "Bench verification");

        Assert.NotNull(def);
        Assert.Equal(ReportType.Diagnostic, def.Type);
        Assert.Contains("Diagnostic", def.Metadata.Title);
        Assert.NotEmpty(def.Sections);

        // Required sections: VehicleSummary, KeyMetrics, DTC Table, Freeze-Frame Table or Alert
        Assert.Contains(def.Sections, s => s.Type == ReportSectionType.VehicleSummary || s.Type == ReportSectionType.KeyValueList);
        Assert.Contains(def.Sections, s => s.Type == ReportSectionType.KeyMetrics);
        Assert.Contains(def.Sections, s => s.Type == ReportSectionType.Table && s.Title.Contains("DTC"));
        Assert.Contains(def.Sections, s => s.Type == ReportSectionType.Notes);
    }

    [Fact]
    public async Task GenerateDynoReportPdfAsync_ProducesValidPdf()
    {
        byte[] pdfBytes = await _reportService.GenerateDynoReportPdfAsync();

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);
        string header = Encoding.ASCII.GetString(pdfBytes.AsSpan(0, 5));
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void BuildDynoReport_ContainsChartsAndMetrics()
    {
        var def = _reportService.BuildDynoReport();

        Assert.NotNull(def);
        Assert.Equal(ReportType.Dyno, def.Type);
        Assert.Contains(def.Sections, s => s.Type == ReportSectionType.KeyMetrics);
        Assert.Contains(def.Sections, s => s.Type == ReportSectionType.Chart && s.Chart != null && s.Chart.Series.Count > 0);
        Assert.Contains(def.Sections, s => s.Type == ReportSectionType.Table && s.Title.Contains("ACCELERATION"));
    }

    [Fact]
    public void BuildDynoReport_WithBaseline_ContainsComparisonData()
    {
        var savedPulls = _dynoService.GetSavedPulls();
        var pull1 = savedPulls.FirstOrDefault();
        var pull2 = _dynoService.GenerateSimulatedDynoPull("Tuned Pull 2", 2200, 7000);
        _dynoService.SavePull(pull2);

        var def = _reportService.BuildDynoReport(pullId: pull2.PullId, baselinePullId: pull1?.PullId);

        Assert.NotNull(def);
        var chartSection = def.Sections.FirstOrDefault(s => s.Type == ReportSectionType.Chart);
        Assert.NotNull(chartSection);
        Assert.NotNull(chartSection.Chart);
        // With baseline, there should be multiple series
        Assert.True(chartSection.Chart.Series.Count >= 2);
    }

    [Fact]
    public async Task GenerateEcuFlashReportPdfAsync_ProducesValidPdf()
    {
        byte[] pdfBytes = await _reportService.GenerateEcuFlashReportPdfAsync();

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);
        string header = Encoding.ASCII.GetString(pdfBytes.AsSpan(0, 5));
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void BuildEcuFlashReport_EnforcesReadOnlyIntegrityNotice()
    {
        var def = _reportService.BuildEcuFlashReport();

        Assert.NotNull(def);
        Assert.Equal(ReportType.EcuFlashAnalysis, def.Type);

        // Must have Read-Only Integrity Notice
        var alert = def.Sections.FirstOrDefault(s => s.Type == ReportSectionType.AlertNotice);
        Assert.NotNull(alert);
        Assert.Contains("READ-ONLY", alert.Title.ToUpperInvariant());
        Assert.Contains("read-only", alert.AlertMessage?.ToLowerInvariant() ?? "");

        // Must have detected regions table and mappack table
        Assert.Contains(def.Sections, s => s.Type == ReportSectionType.Table && s.Title.Contains("REGIONS"));
        Assert.Contains(def.Sections, s => s.Type == ReportSectionType.Table && s.Title.Contains("CALIBRATION"));
    }

    [Fact]
    public async Task GenerateSessionLogReportPdfAsync_ProducesValidPdf()
    {
        byte[] pdfBytes = await _reportService.GenerateSessionLogReportPdfAsync();

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);
        string header = Encoding.ASCII.GetString(pdfBytes.AsSpan(0, 5));
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void BuildSessionLogReport_ContainsTelemetryOverview()
    {
        var def = _reportService.BuildSessionLogReport();

        Assert.NotNull(def);
        Assert.Equal(ReportType.SessionLog, def.Type);
        Assert.Contains(def.Sections, s => s.Type == ReportSectionType.KeyValueList && s.Title.Contains("SESSION"));
        Assert.Contains(def.Sections, s => s.Type == ReportSectionType.Table && s.Title.Contains("STATISTICAL"));
    }

    [Fact]
    public async Task GenerateVehicleHealthReportPdfAsync_ProducesValidPdf()
    {
        byte[] pdfBytes = await _reportService.GenerateVehicleHealthReportPdfAsync(
            notes: "Routine 10,000 km pre-inspection audit.");

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 1000);
        string header = Encoding.ASCII.GetString(pdfBytes.AsSpan(0, 5));
        Assert.Equal("%PDF-", header);
    }

    [Fact]
    public void BuildVehicleHealthReport_ContainsModuleInventoryAndReadiness()
    {
        var def = _reportService.BuildVehicleHealthReport(notes: "Health check");

        Assert.NotNull(def);
        Assert.Equal(ReportType.VehicleHealth, def.Type);
        Assert.Contains(def.Sections, s => s.Type == ReportSectionType.Table && s.Title.Contains("INVENTORY"));
        Assert.Contains(def.Sections, s => s.Type == ReportSectionType.Table && s.Title.Contains("SENSOR"));
        Assert.Contains(def.Sections, s => s.Type == ReportSectionType.AlertNotice && s.Title.Contains("CONCLUSION"));
    }

    [Fact]
    public void SafetyAudit_ReportingLayer_ContainsNoWriteOrFlashMethods()
    {
        string[] forbiddenStems = 
        { 
            "Write", "SaveModified", "EditCell", "Patch", 
            "Bypass", "Stage1", "Stage2", "Stage3", "Tune", 
            "Immo", "Immobilizer", "UnlockSecurity", "SeedKey", "FlashEcu", "WriteFlash" 
        };

        var typesToAudit = new[]
        {
            typeof(ReportService),
            typeof(PdfReportGenerator),
            typeof(ReportDefinition),
            typeof(ReportTemplate)
        };

        foreach (var type in typesToAudit)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var m in methods)
            {
                // Skip default Object methods
                if (m.DeclaringType == typeof(object)) continue;

                foreach (var stem in forbiddenStems)
                {
                    Assert.False(
                        m.Name.Contains(stem, StringComparison.OrdinalIgnoreCase),
                        $"Safety violation: Type '{type.Name}' contains forbidden method '{m.Name}' matching stem '{stem}'.");
                }
            }
        }
    }
}
