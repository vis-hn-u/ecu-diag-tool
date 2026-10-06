using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Dashboard;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardDesignerController : ControllerBase
{
    private readonly IDashboardDesignerService _dashboardService;

    public DashboardDesignerController(IDashboardDesignerService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("layouts")]
    public ActionResult<IReadOnlyList<DashboardLayout>> GetLayouts()
    {
        return Ok(_dashboardService.GetAllLayouts());
    }

    [HttpGet("layouts/{id}")]
    public ActionResult<DashboardLayout> GetLayout(string id)
    {
        var layout = _dashboardService.GetLayoutById(id);
        if (layout == null) return NotFound($"Layout '{id}' not found");
        return Ok(layout);
    }

    [HttpPost("layouts")]
    public ActionResult<DashboardLayout> SaveLayout([FromBody] DashboardLayout layout)
    {
        if (layout == null) return BadRequest("Layout body cannot be null");
        var saved = _dashboardService.SaveLayout(layout);
        return Ok(saved);
    }

    [HttpDelete("layouts/{id}")]
    public IActionResult DeleteLayout(string id)
    {
        bool deleted = _dashboardService.DeleteLayout(id);
        if (!deleted) return NotFound($"Layout '{id}' not found");
        return Ok(new { success = true, message = $"Layout '{id}' deleted" });
    }

    [HttpGet("presets/{key}")]
    public ActionResult<DashboardLayout> LoadPreset(string key)
    {
        return Ok(_dashboardService.LoadPreset(key));
    }
}
