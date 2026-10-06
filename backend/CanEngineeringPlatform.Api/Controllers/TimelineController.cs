using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Sessions;
using CanEngineeringPlatform.Core.Sessions.Timeline;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/timeline")]
public class TimelineController : ControllerBase
{
    private readonly IUnifiedTimelineService _timelineService;
    private readonly IOneClickEngineeringWorkflow _oneClickWorkflow;

    public TimelineController(
        IUnifiedTimelineService timelineService,
        IOneClickEngineeringWorkflow oneClickWorkflow)
    {
        _timelineService = timelineService ?? throw new ArgumentNullException(nameof(timelineService));
        _oneClickWorkflow = oneClickWorkflow ?? throw new ArgumentNullException(nameof(oneClickWorkflow));
    }

    [HttpGet("entries")]
    public ActionResult<IReadOnlyList<TimelineEntry>> GetEntries(
        [FromQuery] int count = 200,
        [FromQuery] TimelineCategory? category = null,
        [FromQuery] TimelineSeverity? minSeverity = null)
    {
        return Ok(_timelineService.GetEntries(count, category, minSeverity));
    }

    [HttpGet("search")]
    public ActionResult<IReadOnlyList<TimelineEntry>> Search(
        [FromQuery] string query,
        [FromQuery] int count = 100)
    {
        return Ok(_timelineService.Search(query, count));
    }

    [HttpDelete("clear")]
    public ActionResult Clear()
    {
        _timelineService.Clear();
        return Ok(new { success = true });
    }

    [HttpPost("start-one-click-session")]
    public async Task<ActionResult<OneClickSessionReport>> StartOneClickSession(
        [FromBody] OneClickSessionRequest request,
        CancellationToken cancellationToken)
    {
        var report = await _oneClickWorkflow.StartSessionAsync(request, cancellationToken);
        return Ok(report);
    }
}
