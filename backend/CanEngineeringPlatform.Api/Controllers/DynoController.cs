using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Dyno;
using CanEngineeringPlatform.Core.Vehicles;

namespace CanEngineeringPlatform.Api.Controllers;

public class DynoPullRequestDto
{
    public string Name { get; set; } = "Stage 1 Dyno Pull";
    public double StartRpm { get; set; } = 2000.0;
    public double EndRpm { get; set; } = 6800.0;
}

public class RoadDynoRunRequestDto
{
    public string Name { get; set; } = "Measured Road Dyno Run";
    public int Gear { get; set; } = 3;
    public AtmosphericStandard Standard { get; set; } = AtmosphericStandard.SAE_J1349;
    public AmbientConditions? Ambient { get; set; }
    public VehicleProfile? Vehicle { get; set; }
    public string? VehicleProfileId { get; set; }
    public CoastDownProfile? CoastDown { get; set; }
    public List<RoadDynoSample> Samples { get; set; } = new();
}

public class DetectPullsRequestDto
{
    public List<RoadDynoSample> Samples { get; set; } = new();
    public VehicleProfile? Vehicle { get; set; }
    public string? VehicleProfileId { get; set; }
    public double MinThrottlePct { get; set; } = 80.0;
    public double MinDurationSec { get; set; } = 2.5;
}

public class CoastDownRequestDto
{
    public List<RoadDynoSample> Samples { get; set; } = new();
    public VehicleProfile? Vehicle { get; set; }
    public string? VehicleProfileId { get; set; }
    public AmbientConditions? Ambient { get; set; }
}

public class CompareRoadRunsRequestDto
{
    public string? BaselineRunId { get; set; }
    public string? TestRunId { get; set; }
    public RoadDynoRun? BaselineRun { get; set; }
    public RoadDynoRun? TestRun { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class DynoController : ControllerBase
{
    private readonly IDynoAnalysisService _dynoService;
    private readonly IVehicleProfileService _vehicleProfileService;

    public DynoController(IDynoAnalysisService dynoService, IVehicleProfileService vehicleProfileService)
    {
        _dynoService = dynoService;
        _vehicleProfileService = vehicleProfileService;
    }

    // ==========================================
    // Simulation Mode Endpoints (Preserved)
    // ==========================================

    [HttpGet("pulls")]
    public ActionResult<IReadOnlyList<DynoPullSummary>> GetPulls()
    {
        return Ok(_dynoService.GetSavedPulls());
    }

    [HttpPost("run-pull")]
    public ActionResult<DynoPullSummary> RunPull([FromBody] DynoPullRequestDto dto)
    {
        var pull = _dynoService.GenerateSimulatedDynoPull(dto.Name, dto.StartRpm, dto.EndRpm);
        _dynoService.SavePull(pull);
        return Ok(pull);
    }

    [HttpGet("performance")]
    public ActionResult<VehiclePerformanceSummary> GetPerformance([FromQuery] string? pullId = null)
    {
        var pulls = _dynoService.GetSavedPulls();
        var pull = !string.IsNullOrEmpty(pullId)
            ? pulls.FirstOrDefault(p => p.PullId == pullId) ?? pulls.FirstOrDefault()
            : pulls.FirstOrDefault();

        if (pull == null)
        {
            pull = _dynoService.GenerateSimulatedDynoPull();
        }

        var perf = _dynoService.CalculatePerformanceMetrics(pull);
        return Ok(perf);
    }

    [HttpDelete("pulls")]
    public IActionResult ClearPulls()
    {
        _dynoService.ClearPulls();
        return Ok(new { success = true });
    }

    // ==========================================
    // Measurement-Based Road Dyno Endpoints
    // ==========================================

    [HttpPost("road-run")]
    public ActionResult<RoadDynoRun> RunRoadDyno([FromBody] RoadDynoRunRequestDto dto)
    {
        if (dto?.Samples == null || dto.Samples.Count < 5)
        {
            return BadRequest(new { error = "At least 5 RoadDynoSamples (speed, time, rpm) are required." });
        }

        var vehicle = dto.Vehicle
            ?? (!string.IsNullOrEmpty(dto.VehicleProfileId) ? _vehicleProfileService.GetProfile(dto.VehicleProfileId) : null)
            ?? _vehicleProfileService.GetActiveProfile();

        var ambient = dto.Ambient ?? new AmbientConditions();

        var run = _dynoService.AnalyzeRoadDynoRun(
            dto.Samples,
            vehicle,
            dto.Gear,
            ambient,
            dto.Standard,
            dto.CoastDown,
            dto.Name ?? "Road Dyno Run");

        return Ok(run);
    }

    [HttpPost("detect-pulls")]
    public ActionResult<List<RoadDynoSample[]>> DetectPulls([FromBody] DetectPullsRequestDto dto)
    {
        if (dto?.Samples == null || dto.Samples.Count < 10)
        {
            return BadRequest(new { error = "At least 10 continuous samples required for pull detection." });
        }

        var vehicle = dto.Vehicle
            ?? (!string.IsNullOrEmpty(dto.VehicleProfileId) ? _vehicleProfileService.GetProfile(dto.VehicleProfileId) : null)
            ?? _vehicleProfileService.GetActiveProfile();

        var detected = _dynoService.DetectRuns(dto.Samples, vehicle);
        return Ok(detected);
    }

    [HttpPost("coastdown")]
    public ActionResult<CoastDownProfile> AnalyzeCoastDown([FromBody] CoastDownRequestDto dto)
    {
        if (dto?.Samples == null || dto.Samples.Count < 5)
        {
            return BadRequest(new { error = "At least 5 deceleration samples required." });
        }

        var vehicle = dto.Vehicle
            ?? (!string.IsNullOrEmpty(dto.VehicleProfileId) ? _vehicleProfileService.GetProfile(dto.VehicleProfileId) : null)
            ?? _vehicleProfileService.GetActiveProfile();

        var ambient = dto.Ambient ?? new AmbientConditions();

        var profile = _dynoService.AnalyzeCoastDown(dto.Samples, vehicle, ambient);
        return Ok(profile);
    }

    [HttpPost("compare")]
    public ActionResult<DynoRunComparisonResult> CompareRuns([FromBody] CompareRoadRunsRequestDto dto)
    {
        RoadDynoRun? baseline = dto.BaselineRun;
        RoadDynoRun? test = dto.TestRun;

        if (baseline == null && !string.IsNullOrEmpty(dto.BaselineRunId))
        {
            baseline = _dynoService.GetSavedRoadRun(dto.BaselineRunId);
        }
        if (test == null && !string.IsNullOrEmpty(dto.TestRunId))
        {
            test = _dynoService.GetSavedRoadRun(dto.TestRunId);
        }

        if (baseline == null || test == null)
        {
            return BadRequest(new { error = "Both Baseline and Test RoadDynoRuns are required." });
        }

        var comparison = _dynoService.CompareRoadRuns(baseline, test);
        return Ok(comparison);
    }

    [HttpGet("road-runs")]
    public ActionResult<IReadOnlyList<RoadDynoRun>> GetRoadRuns()
    {
        return Ok(_dynoService.GetSavedRoadRuns());
    }

    [HttpGet("road-run/{id}")]
    public ActionResult<RoadDynoRun> GetRoadRun(string id)
    {
        var run = _dynoService.GetSavedRoadRun(id);
        if (run == null) return NotFound();
        return Ok(run);
    }
}
