using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Calibration;

namespace CanEngineeringPlatform.Api.Controllers;

public class CellUpdateDto
{
    public string TableId { get; set; } = string.Empty;
    public int XIndex { get; set; }
    public int YIndex { get; set; }
    public double NewValue { get; set; }
}

public class BulkEditDto
{
    public string TableId { get; set; } = string.Empty;
    public double Value { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class CalibrationController : ControllerBase
{
    private readonly ICalibrationService _calibrationService;

    public CalibrationController(ICalibrationService calibrationService)
    {
        _calibrationService = calibrationService;
    }

    [HttpGet("project")]
    public ActionResult<CalibrationProject> GetProject()
    {
        return Ok(_calibrationService.GetProject());
    }

    [HttpGet("table/{id}")]
    public ActionResult<CalibrationTable> GetTable(string id)
    {
        var table = _calibrationService.GetTable(id);
        if (table == null) return NotFound($"Table {id} not found");
        return Ok(table);
    }

    [HttpPost("cell")]
    public IActionResult UpdateCell([FromBody] CellUpdateDto dto)
    {
        bool success = _calibrationService.UpdateCell(dto.TableId, dto.XIndex, dto.YIndex, dto.NewValue);
        if (!success) return BadRequest("Invalid cell coordinates or table ID");
        return Ok(new { success = true });
    }

    [HttpPost("offset")]
    public IActionResult BulkOffset([FromBody] BulkEditDto dto)
    {
        bool success = _calibrationService.BulkOffset(dto.TableId, dto.Value);
        if (!success) return BadRequest("Invalid table ID");
        return Ok(new { success = true });
    }

    [HttpPost("scale")]
    public IActionResult BulkScale([FromBody] BulkEditDto dto)
    {
        bool success = _calibrationService.BulkScale(dto.TableId, dto.Value);
        if (!success) return BadRequest("Invalid table ID");
        return Ok(new { success = true });
    }

    [HttpPost("undo/{tableId}")]
    public IActionResult Undo(string tableId)
    {
        bool success = _calibrationService.Undo(tableId);
        return Ok(new { success });
    }

    [HttpPost("redo/{tableId}")]
    public IActionResult Redo(string tableId)
    {
        bool success = _calibrationService.Redo(tableId);
        return Ok(new { success });
    }

    [HttpGet("diff/{tableId}")]
    public ActionResult<IReadOnlyList<CalibrationDiffCell>> GetDiff(string tableId)
    {
        return Ok(_calibrationService.ComputeDiff(tableId));
    }

    [HttpGet("history/{tableId}")]
    public ActionResult<IReadOnlyList<CalibrationEditRecord>> GetHistory(string tableId)
    {
        return Ok(_calibrationService.GetEditHistory(tableId));
    }

    [HttpPost("reset-table/{tableId}")]
    public IActionResult ResetTable(string tableId)
    {
        _calibrationService.ResetTableToOriginal(tableId);
        return Ok(new { success = true });
    }

    [HttpPost("reset-all")]
    public IActionResult ResetAll()
    {
        _calibrationService.ResetAllToStock();
        return Ok(new { success = true });
    }
}
