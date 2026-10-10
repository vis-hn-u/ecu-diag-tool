using System.Text;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Analysis.OperatingEnvelope;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/tuning")]
public class LiveTuningAnalysisController : ControllerBase
{
    private readonly ILiveOperatingGridService _gridService;

    public LiveTuningAnalysisController(ILiveOperatingGridService gridService)
    {
        _gridService = gridService;
    }

    [HttpGet("grid")]
    [HttpGet("grid/{gridId}")]
    public ActionResult<LiveOperatingGridSnapshot> GetSnapshot(string? gridId = null)
    {
        var snap = _gridService.GetSnapshot(gridId ?? "rpm_vs_load_lambda");
        return Ok(snap);
    }

    [HttpGet("configs")]
    public ActionResult GetConfigs()
    {
        return Ok(_gridService.GetAvailableConfigs());
    }

    [HttpPost("config")]
    public ActionResult ConfigureGrid([FromBody] OperatingGridConfig config)
    {
        if (config == null || string.IsNullOrWhiteSpace(config.GridId))
        {
            return BadRequest("Invalid grid configuration");
        }
        _gridService.ConfigureGrid(config);
        return Ok(_gridService.GetSnapshot(config.GridId));
    }

    [HttpPost("reset")]
    [HttpPost("reset/{gridId}")]
    public ActionResult ResetGrid(string? gridId = null)
    {
        string id = gridId ?? "rpm_vs_load_lambda";
        _gridService.ResetGrid(id);
        return Ok(new { message = $"Operating grid '{id}' reset successfully" });
    }

    [HttpGet("export/csv")]
    [HttpGet("export/csv/{gridId}")]
    public IActionResult ExportCsv(string? gridId = null)
    {
        string id = gridId ?? "rpm_vs_load_lambda";
        string csv = _gridService.ExportToCsv(id);
        byte[] bytes = Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"{id}_telemetry_grid.csv");
    }

    public record IngestPointRequest(string GridId, double XVal, double YVal, double ZVal);

    [HttpPost("point")]
    public ActionResult IngestPoint([FromBody] IngestPointRequest req)
    {
        if (req == null) return BadRequest();
        _gridService.IngestPoint(req.GridId, req.XVal, req.YVal, req.ZVal);
        return Ok();
    }
}
