using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Simulation.DigitalTwin;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/digital-twin")]
public class DigitalTwinController : ControllerBase
{
    private readonly IVehicleDigitalTwinService _twinService;

    public DigitalTwinController(IVehicleDigitalTwinService twinService)
    {
        _twinService = twinService ?? throw new ArgumentNullException(nameof(twinService));
    }

    [HttpGet("nodes")]
    public ActionResult<IReadOnlyList<DigitalTwinNode>> GetNodes()
    {
        return Ok(_twinService.GetNodes());
    }

    [HttpGet("nodes/{nodeId}")]
    public ActionResult<DigitalTwinNode> GetNode(string nodeId)
    {
        var node = _twinService.GetNode(nodeId);
        if (node == null) return NotFound($"Digital twin node '{nodeId}' not found.");
        return Ok(node);
    }

    [HttpGet("status")]
    public ActionResult GetStatus()
    {
        return Ok(new
        {
            isRunning = _twinService.IsRunning,
            activeNodeCount = _twinService.ActiveNodeCount,
            totalNodeCount = _twinService.GetNodes().Count
        });
    }

    [HttpPost("start")]
    public ActionResult StartSimulation()
    {
        _twinService.StartSimulation();
        return Ok(new { success = true, isRunning = true });
    }

    [HttpPost("stop")]
    public ActionResult StopSimulation()
    {
        _twinService.StopSimulation();
        return Ok(new { success = true, isRunning = false });
    }

    [HttpPost("fault")]
    public ActionResult SetFault([FromBody] DigitalTwinFaultRequest request)
    {
        _twinService.SetFault(request);
        return Ok(new { success = true, message = $"Fault configured for {request.NodeId}." });
    }

    [HttpPost("clear-faults")]
    public ActionResult ClearFaults([FromQuery] string? nodeId = null)
    {
        _twinService.ClearFaults(nodeId);
        return Ok(new { success = true, message = "Faults cleared." });
    }
}
