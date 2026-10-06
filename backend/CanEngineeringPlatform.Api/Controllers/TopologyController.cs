using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Topology;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TopologyController : ControllerBase
{
    private readonly ITopologyEngine _topologyEngine;

    public TopologyController(ITopologyEngine topologyEngine)
    {
        _topologyEngine = topologyEngine;
    }

    [HttpGet]
    public IActionResult GetTopology()
    {
        return Ok(_topologyEngine.GetSnapshot());
    }

    [HttpPost("mode")]
    public IActionResult SetMode([FromBody] SetModeRequest request)
    {
        _topologyEngine.SetMode(request.Mode);
        return Ok(_topologyEngine.GetSnapshot());
    }

    [HttpPost("reset")]
    public IActionResult Reset()
    {
        _topologyEngine.Reset();
        return Ok(_topologyEngine.GetSnapshot());
    }
}

public class SetModeRequest
{
    public TopologyMode Mode { get; set; }
}
