using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Reporting;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("types")]
    public ActionResult<IEnumerable<object>> GetReportTypes()
    {
        var types = new[]
        {
            new { Id = "diagnostic", Name = "Vehicle Diagnostic Audit Report", Description = "ECU identification, active DTCs, freeze-frame snapshots, live data, and AI anomaly correlation." },
            new { Id = "dyno", Name = "Chassis & Road Dyno Performance Report", Description = "Wheel/crank horsepower and torque curves, ambient corrections, 0-100/quarter-mile splits, and run quality." },
            new { Id = "ecu-flash", Name = "ECU Firmware & Calibration Analysis Report", Description = "Binary SHA-256 integrity, detected memory regions, and 2D/3D calibration mappack previews in read-only mode." },
            new { Id = "session-log", Name = "Telemetry & Bus Session Audit Report", Description = "Synchronized high-speed bus telemetry channels, statistics, trend graphs, and data quality metrics." },
            new { Id = "vehicle-health", Name = "Complete Vehicle Health Audit", Description = "Module inventory status, emissions readiness, sensor validation, and technician recommendations." }
        };
        return Ok(types);
    }

    [HttpGet("diagnostic/pdf")]
    public async Task<IActionResult> DownloadDiagnosticReport([FromQuery] string? vehicleId = null, [FromQuery] string? notes = null)
    {
        byte[] pdfBytes = await _reportService.GenerateDiagnosticReportPdfAsync(vehicleId, notes);
        string filename = $"Diagnostic_Report_{DateTime.UtcNow:yyyyMMdd_HHmm}.pdf";
        return File(pdfBytes, "application/pdf", filename);
    }

    [HttpGet("dyno/pdf")]
    public async Task<IActionResult> DownloadDynoReport(
        [FromQuery] string? pullId = null,
        [FromQuery] string? baselinePullId = null,
        [FromQuery] string? vehicleId = null)
    {
        byte[] pdfBytes = await _reportService.GenerateDynoReportPdfAsync(pullId, baselinePullId, vehicleId);
        string filename = $"Dyno_Performance_Report_{DateTime.UtcNow:yyyyMMdd_HHmm}.pdf";
        return File(pdfBytes, "application/pdf", filename);
    }

    [HttpGet("ecu-flash/pdf")]
    public async Task<IActionResult> DownloadEcuFlashReport(
        [FromQuery] string? fileHash = null,
        [FromQuery] string? packId = null,
        [FromQuery] string? vehicleId = null)
    {
        byte[] pdfBytes = await _reportService.GenerateEcuFlashReportPdfAsync(fileHash, packId, vehicleId);
        string filename = $"ECU_Calibration_Report_{DateTime.UtcNow:yyyyMMdd_HHmm}.pdf";
        return File(pdfBytes, "application/pdf", filename);
    }

    [HttpGet("session-log/pdf")]
    public async Task<IActionResult> DownloadSessionLogReport(
        [FromQuery] string? sessionId = null,
        [FromQuery] string? vehicleId = null)
    {
        byte[] pdfBytes = await _reportService.GenerateSessionLogReportPdfAsync(sessionId, vehicleId);
        string filename = $"Session_Log_Report_{DateTime.UtcNow:yyyyMMdd_HHmm}.pdf";
        return File(pdfBytes, "application/pdf", filename);
    }

    [HttpGet("vehicle-health/pdf")]
    public async Task<IActionResult> DownloadVehicleHealthReport([FromQuery] string? vehicleId = null, [FromQuery] string? notes = null)
    {
        byte[] pdfBytes = await _reportService.GenerateVehicleHealthReportPdfAsync(vehicleId, notes);
        string filename = $"Vehicle_Health_Report_{DateTime.UtcNow:yyyyMMdd_HHmm}.pdf";
        return File(pdfBytes, "application/pdf", filename);
    }

    [HttpGet("preview/{reportType}")]
    public ActionResult<ReportDefinition> GetReportPreview(string reportType, [FromQuery] string? vehicleId = null)
    {
        ReportDefinition def = reportType.ToLowerInvariant() switch
        {
            "diagnostic" => _reportService.BuildDiagnosticReport(vehicleId),
            "dyno" => _reportService.BuildDynoReport(vehicleId: vehicleId),
            "ecu-flash" or "flash" => _reportService.BuildEcuFlashReport(vehicleId: vehicleId),
            "session-log" or "session" or "log" => _reportService.BuildSessionLogReport(vehicleId: vehicleId),
            "vehicle-health" or "health" => _reportService.BuildVehicleHealthReport(vehicleId),
            _ => throw new ArgumentException($"Unknown report type '{reportType}'")
        };
        return Ok(def);
    }
}
