using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Analysis.ReverseEngineering;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/reverse-engineering")]
public class CanReverseEngineeringController : ControllerBase
{
    private readonly ICanReverseEngineeringService _reverseEngineeringService;

    public CanReverseEngineeringController(ICanReverseEngineeringService reverseEngineeringService)
    {
        _reverseEngineeringService = reverseEngineeringService ?? throw new ArgumentNullException(nameof(reverseEngineeringService));
    }

    [HttpGet("metrics")]
    public ActionResult<IReadOnlyList<CanIdDeepMetrics>> GetAllMetrics()
    {
        return Ok(_reverseEngineeringService.GetAllMetrics());
    }

    [HttpGet("metrics/{canId}")]
    public ActionResult<CanIdDeepMetrics> GetMetricsForId(uint canId)
    {
        var metrics = _reverseEngineeringService.GetMetricsForId(canId);
        if (metrics == null) return NotFound($"CAN ID 0x{canId:X} has not been observed yet.");
        return Ok(metrics);
    }

    [HttpGet("signals")]
    public ActionResult<IReadOnlyList<DiscoveredSignalCandidate>> GetDiscoveredSignals([FromQuery] uint? canId = null)
    {
        return Ok(_reverseEngineeringService.GetDiscoveredSignals(canId));
    }

    [HttpPost("baseline")]
    public ActionResult SetBaseline()
    {
        _reverseEngineeringService.SetBaselineCapture();
        return Ok(new { success = true, message = "Baseline CAN message snapshot recorded." });
    }

    [HttpPost("reset")]
    public ActionResult Reset()
    {
        _reverseEngineeringService.Reset();
        return Ok(new { success = true, message = "Reverse-engineering statistical engine reset." });
    }
}
