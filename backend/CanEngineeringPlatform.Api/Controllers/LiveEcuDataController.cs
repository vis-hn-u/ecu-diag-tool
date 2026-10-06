using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.LiveEcuData;

namespace CanEngineeringPlatform.Api.Controllers;

public class PinRequestDto
{
    public string ParameterId { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
}

[ApiController]
[Route("api/live-ecu-data")]
public class LiveEcuDataController : ControllerBase
{
    private readonly ILiveEcuDataService _liveDataService;

    public LiveEcuDataController(ILiveEcuDataService liveDataService)
    {
        _liveDataService = liveDataService;
    }

    [HttpGet("parameters")]
    public ActionResult<IReadOnlyList<LiveParameter>> GetParameters([FromQuery] LiveParameterCategory? category = null)
    {
        if (category.HasValue)
        {
            return Ok(_liveDataService.GetParametersByCategory(category.Value));
        }
        return Ok(_liveDataService.GetAllParameters());
    }

    [HttpGet("requested-vs-actual")]
    public ActionResult<IReadOnlyList<RequestedVsActualPair>> GetRequestedVsActual()
    {
        return Ok(_liveDataService.GetRequestedVsActualPairs());
    }

    [HttpPost("pin")]
    public IActionResult SetPin([FromBody] PinRequestDto dto)
    {
        _liveDataService.SetPinned(dto.ParameterId, dto.IsPinned);
        return Ok(new { success = true });
    }

    [HttpPost("clear")]
    public IActionResult Clear()
    {
        _liveDataService.ClearParameters();
        return Ok(new { success = true });
    }
}
