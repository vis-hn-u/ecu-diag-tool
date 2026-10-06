using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Ai;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/copilot")]
public class AiCopilotController : ControllerBase
{
    private readonly IAiCopilotService _copilotService;

    public AiCopilotController(IAiCopilotService copilotService)
    {
        _copilotService = copilotService ?? throw new ArgumentNullException(nameof(copilotService));
    }

    [HttpPost("analyze")]
    public async Task<ActionResult<CopilotResponse>> Analyze([FromBody] CopilotQueryRequest request)
    {
        var response = await _copilotService.AnalyzeAsync(request);
        return Ok(response);
    }
}
