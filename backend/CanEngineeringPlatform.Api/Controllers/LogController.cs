using System.Text;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Logging;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogController : ControllerBase
{
    private readonly ICanLoggerService _loggerService;

    public LogController(ICanLoggerService loggerService)
    {
        _loggerService = loggerService;
    }

    [HttpGet("current")]
    public IActionResult GetCurrentSession()
    {
        var session = _loggerService.GetCurrentSession();
        if (session == null)
        {
            return Ok(new { isActive = false });
        }

        return Ok(new
        {
            id = session.Id,
            name = session.Name,
            startTimeUtc = session.StartTimeUtc,
            isActive = session.IsActive,
            isPaused = session.IsPaused,
            frameCount = session.FrameCount
        });
    }

    [HttpGet("sessions")]
    public IActionResult GetSessions()
    {
        var list = _loggerService.GetSessions().Select(s => new
        {
            id = s.Id,
            name = s.Name,
            startTimeUtc = s.StartTimeUtc,
            endTimeUtc = s.EndTimeUtc,
            frameCount = s.FrameCount,
            isActive = s.IsActive
        });

        return Ok(list);
    }

    [HttpGet("session/{id}")]
    public IActionResult GetSession(string id)
    {
        var session = _loggerService.GetSession(id);
        if (session == null)
        {
            return NotFound();
        }

        return Ok(session);
    }

    [HttpPost("start")]
    public IActionResult StartLogging([FromBody] StartLogRequest request)
    {
        var session = _loggerService.StartLogging(request?.Name ?? string.Empty);
        return Ok(session);
    }

    [HttpPost("pause")]
    public IActionResult PauseLogging()
    {
        _loggerService.PauseLogging();
        return Ok(new { success = true });
    }

    [HttpPost("resume")]
    public IActionResult ResumeLogging()
    {
        _loggerService.ResumeLogging();
        return Ok(new { success = true });
    }

    [HttpPost("stop")]
    public IActionResult StopLogging()
    {
        var session = _loggerService.StopLogging();
        return Ok(session);
    }

    [HttpGet("export/{id}")]
    public IActionResult ExportCsv(string id)
    {
        var session = _loggerService.GetSession(id);
        if (session == null)
        {
            return NotFound();
        }

        string csv = _loggerService.ExportCsv(id);
        byte[] bytes = Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"{session.Name}.csv");
    }
}

public class StartLogRequest
{
    public string? Name { get; set; }
}
