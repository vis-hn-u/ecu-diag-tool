using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Vehicles;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehicleProfileController : ControllerBase
{
    private readonly IVehicleProfileService _vehicleService;

    public VehicleProfileController(IVehicleProfileService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<VehicleProfile>> GetAll()
    {
        return Ok(_vehicleService.GetAllProfiles());
    }

    [HttpGet("active")]
    public ActionResult<VehicleProfile> GetActive()
    {
        return Ok(_vehicleService.GetActiveProfile());
    }

    [HttpGet("{id}")]
    public ActionResult<VehicleProfile> GetById(string id)
    {
        var profile = _vehicleService.GetProfile(id);
        if (profile == null) return NotFound($"Vehicle profile '{id}' not found.");
        return Ok(profile);
    }

    [HttpPost]
    public ActionResult<VehicleProfile> Save([FromBody] VehicleProfile profile)
    {
        if (profile == null) return BadRequest("Profile payload is required.");
        var saved = _vehicleService.SaveProfile(profile);
        return Ok(saved);
    }

    [HttpPost("active/{id}")]
    public IActionResult SetActive(string id)
    {
        var profile = _vehicleService.GetProfile(id);
        if (profile == null) return NotFound($"Vehicle profile '{id}' not found.");
        _vehicleService.SetActiveProfile(id);
        return Ok(_vehicleService.GetActiveProfile());
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        bool deleted = _vehicleService.DeleteProfile(id);
        if (!deleted) return BadRequest("Cannot delete the active or sole remaining vehicle profile.");
        return Ok(new { success = true });
    }
}
