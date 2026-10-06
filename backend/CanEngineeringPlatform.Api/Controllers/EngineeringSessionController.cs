using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Sessions;

namespace CanEngineeringPlatform.Api.Controllers;

public class StartSessionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Technician { get; set; }
    public List<string>? Tags { get; set; }
}

public class StartRunRequest
{
    public string Name { get; set; } = string.Empty;
    public RunType Type { get; set; } = RunType.DynoPull;
}

public class StopRunRequest
{
    public Dictionary<string, double>? SummaryMetrics { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class EngineeringSessionController : ControllerBase
{
    private readonly ISessionManagementService _sessionService;

    public EngineeringSessionController(ISessionManagementService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<EngineeringSession>> GetAll()
    {
        return Ok(_sessionService.GetAllSessions());
    }

    [HttpGet("active")]
    public ActionResult<EngineeringSession> GetActive()
    {
        var active = _sessionService.GetActiveSession();
        if (active == null) return NotFound("No active engineering session.");
        return Ok(active);
    }

    [HttpGet("{id}")]
    public ActionResult<EngineeringSession> GetById(string id)
    {
        var session = _sessionService.GetSession(id);
        if (session == null) return NotFound($"Session '{id}' not found.");
        return Ok(session);
    }

    [HttpPost("start")]
    public ActionResult<EngineeringSession> StartSession([FromBody] StartSessionRequest request)
    {
        var session = _sessionService.StartSession(request.Name, request.Description, request.Technician, request.Tags);
        return Ok(session);
    }

    [HttpPost("stop")]
    public ActionResult<EngineeringSession> StopSession([FromQuery] string? sessionId = null)
    {
        var stopped = _sessionService.StopSession(sessionId);
        if (stopped == null) return NotFound("No active session to stop.");
        return Ok(stopped);
    }

    [HttpPost("{sessionId}/runs/start")]
    public ActionResult<EngineeringRun> StartRun(string sessionId, [FromBody] StartRunRequest request)
    {
        var run = _sessionService.StartRun(sessionId, request.Name, request.Type);
        if (run == null) return BadRequest("Cannot start run on inactive or non-existent session.");
        return Ok(run);
    }

    [HttpPost("{sessionId}/runs/{runId}/stop")]
    public ActionResult<EngineeringRun> StopRun(string sessionId, string runId, [FromBody] StopRunRequest? request = null)
    {
        var stopped = _sessionService.StopRun(sessionId, runId, request?.SummaryMetrics);
        if (stopped == null) return NotFound($"Run '{runId}' not found or already stopped.");
        return Ok(stopped);
    }
}
