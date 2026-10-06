using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.EcuEngineering.Programming;

namespace CanEngineeringPlatform.Api.Controllers;

public class StartProgrammingDto
{
    public string EcuName { get; set; } = "ECM";
    public uint RequestCanId { get; set; } = 0x7E0;
    public string TargetImageBase64 { get; set; } = string.Empty;
    public bool OperatorConfirmed { get; set; }
    public string OperatorRationale { get; set; } = string.Empty;
    public double CurrentSupplyVoltage { get; set; } = 13.8;
}

[ApiController]
[Route("api/ecu-programming")]
public class EcuProgrammingController : ControllerBase
{
    private readonly IEcuProgrammingService _programmingService;

    public EcuProgrammingController(IEcuProgrammingService programmingService)
    {
        _programmingService = programmingService ?? throw new ArgumentNullException(nameof(programmingService));
    }

    [HttpGet("status")]
    public ActionResult<EcuProgrammingSessionStatus> GetStatus()
    {
        return Ok(_programmingService.GetStatus());
    }

    [HttpPost("execute")]
    public async Task<ActionResult<EcuProgrammingSessionStatus>> ExecuteProgramming(
        [FromBody] StartProgrammingDto dto,
        CancellationToken cancellationToken)
    {
        byte[] imageBytes = Array.Empty<byte>();
        if (!string.IsNullOrEmpty(dto.TargetImageBase64))
        {
            try { imageBytes = Convert.FromBase64String(dto.TargetImageBase64); } catch { }
        }

        var req = new ExecuteProgrammingRequest
        {
            EcuName = dto.EcuName,
            RequestCanId = dto.RequestCanId,
            TargetImageBytes = imageBytes,
            OperatorConfirmed = dto.OperatorConfirmed,
            OperatorRationale = dto.OperatorRationale,
            CurrentSupplyVoltage = dto.CurrentSupplyVoltage
        };

        var status = await _programmingService.ExecuteFullWorkflowAsync(req, cancellationToken);
        if (status.State == ProgrammingLifecycleState.Fault)
        {
            return StatusCode(400, status);
        }

        return Ok(status);
    }

    [HttpPost("abort")]
    public ActionResult Abort([FromQuery] string reason = "User aborted operation")
    {
        _programmingService.Abort(reason);
        return Ok(new { success = true, message = "Programming aborted. Safe recovery state initialized." });
    }
}
