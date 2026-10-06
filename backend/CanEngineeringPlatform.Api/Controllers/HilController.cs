using System;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Hardware.Hil;

namespace CanEngineeringPlatform.Api.Controllers;

public class SetChannelValueDto
{
    public string ChannelId { get; set; } = string.Empty;
    public double Value { get; set; }
}

public class SetPowerSupplyDto
{
    public double Voltage { get; set; } = 13.8;
    public bool Enabled { get; set; } = true;
}

[ApiController]
[Route("api/hil")]
public class HilController : ControllerBase
{
    private readonly IHilService _hilService;

    public HilController(IHilService hilService)
    {
        _hilService = hilService ?? throw new ArgumentNullException(nameof(hilService));
    }

    [HttpGet("fixture")]
    public ActionResult<HilTestFixture> GetFixture()
    {
        return Ok(_hilService.GetFixtureStatus());
    }

    [HttpPost("channel/value")]
    public ActionResult SetValue([FromBody] SetChannelValueDto dto)
    {
        _hilService.SetChannelValue(dto.ChannelId, dto.Value);
        return Ok(new { success = true, channelId = dto.ChannelId, value = dto.Value });
    }

    [HttpPost("channel/fault")]
    public ActionResult InjectFault([FromBody] HilFaultInjectionCommand cmd)
    {
        _hilService.InjectChannelFault(cmd);
        return Ok(new { success = true, channelId = cmd.ChannelId, state = cmd.InjectedState.ToString() });
    }

    [HttpPost("power/main")]
    public ActionResult SetPower([FromBody] SetPowerSupplyDto dto)
    {
        _hilService.SetMainPower(dto.Voltage, dto.Enabled);
        return Ok(new { success = true, voltage = dto.Voltage, enabled = dto.Enabled });
    }

    [HttpPost("power/ignition")]
    public ActionResult SetIgnition([FromQuery] bool active)
    {
        _hilService.SetIgnition(active);
        return Ok(new { success = true, ignitionActive = active });
    }

    [HttpPost("reset")]
    public ActionResult ResetAll()
    {
        _hilService.ResetAllChannels();
        return Ok(new { success = true, message = "All HIL channels and relays restored to nominal bench defaults." });
    }
}
