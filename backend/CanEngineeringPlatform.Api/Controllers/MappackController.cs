using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Calibration;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MappackController : ControllerBase
{
    private readonly IEcuDefinitionPackService _mappackService;

    public MappackController(IEcuDefinitionPackService mappackService)
    {
        _mappackService = mappackService;
    }

    [HttpGet("packs")]
    public ActionResult<IReadOnlyList<EcuDefinitionPack>> GetAllPacks()
    {
        return Ok(_mappackService.GetAllPacks());
    }

    [HttpGet("packs/{id}")]
    public ActionResult<EcuDefinitionPack> GetPackById(string id)
    {
        var pack = _mappackService.GetPackById(id);
        if (pack == null) return NotFound($"Definition pack '{id}' not found");
        return Ok(pack);
    }

    [HttpGet("match")]
    public ActionResult<EcuDefinitionPack> MatchPack(
        [FromQuery] string? hwPartNumber = null,
        [FromQuery] string? swVersion = null,
        [FromQuery] string? calibrationId = null)
    {
        var matched = _mappackService.MatchPack(hwPartNumber, swVersion, calibrationId);
        if (matched == null) return NotFound("No matching ECU definition pack found");
        return Ok(matched);
    }

    [HttpGet("packs/{packId}/maps/{mapId}")]
    public ActionResult<ReadonlyEcuMap> GetMap(string packId, string mapId)
    {
        var map = _mappackService.GetMap(packId, mapId);
        if (map == null) return NotFound($"Map '{mapId}' in pack '{packId}' not found");
        return Ok(map);
    }

    [HttpGet("packs/{packId}/maps/{mapId}/trace")]
    public ActionResult<OperatingPointTracer> GetOperatingPoint(
        string packId,
        string mapId,
        [FromQuery] double rpm = 2500,
        [FromQuery] double load = 80.0)
    {
        var tracer = _mappackService.CalculateOperatingPoint(packId, mapId, rpm, load);
        return Ok(tracer);
    }

    [HttpPost("packs/custom")]
    public ActionResult<EcuDefinitionPack> AddCustomPack([FromBody] EcuDefinitionPack pack)
    {
        if (pack == null || string.IsNullOrWhiteSpace(pack.Id))
        {
            return BadRequest("A valid pack with an Id is required.");
        }
        _mappackService.AddCustomPack(pack);
        return Ok(pack);
    }
}
