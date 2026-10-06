using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Api.Services;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GatewayController : ControllerBase
{
    private readonly IGatewayService _gatewayService;

    public GatewayController(IGatewayService gatewayService)
    {
        _gatewayService = gatewayService;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(_gatewayService.GetStatus());
    }

    [HttpPost("address")]
    public async Task<IActionResult> ConfigureAddress([FromBody] ConfigureAddressRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return BadRequest("Address cannot be empty");
        }

        await _gatewayService.ConfigureAddressAsync(request.Address);
        return Ok(_gatewayService.GetStatus());
    }

    [HttpPost("poll-status")]
    public async Task<IActionResult> PollStatus()
    {
        await _gatewayService.RequestEspStatusAsync();
        return Ok(_gatewayService.GetStatus());
    }
}

public class ConfigureAddressRequest
{
    public string Address { get; set; } = string.Empty;
}
