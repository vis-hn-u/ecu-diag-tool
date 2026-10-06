using System;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Engine;

namespace CanEngineeringPlatform.Api.Controllers;

public class EngineControlDto
{
    public double? ThrottlePositionPct { get; set; }
    public int? Gear { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class EngineController : ControllerBase
{
    private readonly IEngineSimulator _engineSimulator;
    private readonly IEngineCanBridgeService _engineBridge;

    public EngineController(
        IEngineSimulator engineSimulator,
        IEngineCanBridgeService engineBridge)
    {
        _engineSimulator = engineSimulator;
        _engineBridge = engineBridge;
    }

    [HttpGet("status")]
    public ActionResult<object> GetStatus()
    {
        return Ok(new
        {
            parameters = _engineSimulator.Parameters,
            inputs = _engineSimulator.Inputs,
            outputs = _engineSimulator.CurrentOutputs,
            isSimulationRunning = _engineBridge.IsRunning
        });
    }

    [HttpPost("control")]
    public ActionResult<EngineOutputs> SetControl([FromBody] EngineControlDto dto)
    {
        if (dto.ThrottlePositionPct.HasValue)
        {
            _engineBridge.SetThrottle(dto.ThrottlePositionPct.Value);
        }
        if (dto.Gear.HasValue)
        {
            _engineBridge.SetGear(dto.Gear.Value);
        }

        var outputs = _engineSimulator.Step(0.05);
        return Ok(outputs);
    }

    [HttpPost("start-simulation")]
    public IActionResult StartSimulation()
    {
        _engineBridge.Start();
        return Ok(new { isRunning = true });
    }

    [HttpPost("stop-simulation")]
    public IActionResult StopSimulation()
    {
        _engineBridge.Stop();
        return Ok(new { isRunning = false });
    }

    [HttpPost("reset")]
    public ActionResult<EngineOutputs> Reset()
    {
        _engineSimulator.Reset();
        return Ok(_engineSimulator.CurrentOutputs);
    }
}
