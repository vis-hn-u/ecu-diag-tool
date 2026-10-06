using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Analysis.Comparison;
using CanEngineeringPlatform.Core.Analysis.Import;
using CanEngineeringPlatform.Core.Analysis.Processing;
using CanEngineeringPlatform.Core.Analysis.TimeSeries;
using CanEngineeringPlatform.Core.Analysis.Validation;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Api.Controllers;

public class LogImportPayload
{
    public string? Content { get; set; }
    public string? FileName { get; set; }
}

public class ProcessChannelPayload
{
    public TimeSeriesChannel Channel { get; set; } = new();
    public ProcessingOptions Options { get; set; } = new();
}

public class CompareRunsPayload
{
    public CanonicalLogDataSet RunA { get; set; } = new();
    public CanonicalLogDataSet RunB { get; set; } = new();
    public ComparisonAlignmentMode Mode { get; set; } = ComparisonAlignmentMode.TimeTriggered;
    public string? TriggerChannel { get; set; }
    public double TriggerThreshold { get; set; } = 0.0;
}

public class GpsSyncPayload
{
    public CanonicalLogDataSet DataSet { get; set; } = new();
    public double CanMonotonicOffsetMs { get; set; } = 0.0;
}

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly ILogImportService _importService;
    private readonly LogValidationEngine _validationEngine;
    private readonly SignalProcessingEngine _processingEngine;
    private readonly RunComparisonService _comparisonService;
    private readonly ICanLoggerService _loggerService;

    // Cache of imported datasets for session/run comparison workflows
    private static readonly ConcurrentDictionary<string, CanonicalLogDataSet> _cachedDataSets = new();

    public AnalysisController(
        ILogImportService importService,
        LogValidationEngine validationEngine,
        SignalProcessingEngine processingEngine,
        RunComparisonService comparisonService,
        ICanLoggerService loggerService)
    {
        _importService = importService;
        _validationEngine = validationEngine;
        _processingEngine = processingEngine;
        _comparisonService = comparisonService;
        _loggerService = loggerService;
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportLogFile([FromForm] IFormFile? file, [FromBody] LogImportPayload? body)
    {
        string content;
        string fileName = "imported_log";

        if (file != null && file.Length > 0)
        {
            fileName = file.FileName;
            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            content = await reader.ReadToEndAsync();
        }
        else if (body != null && !string.IsNullOrWhiteSpace(body.Content))
        {
            content = body.Content;
            if (!string.IsNullOrWhiteSpace(body.FileName))
            {
                fileName = body.FileName;
            }
        }
        else
        {
            return BadRequest(new { error = "No log file or text content was provided." });
        }

        var result = _importService.ImportLog(content, fileName);
        if (result.Success && result.DataSet != null)
        {
            _cachedDataSets[result.DataSet.DatasetId] = result.DataSet;
        }

        return Ok(result);
    }

    [HttpPost("validate")]
    public IActionResult ValidateLog([FromBody] CanonicalLogDataSet dataset)
    {
        if (dataset == null) return BadRequest("DataSet is required.");
        var report = _validationEngine.Validate(dataset);
        return Ok(report);
    }

    [HttpPost("process")]
    public IActionResult ProcessSignal([FromBody] ProcessChannelPayload payload)
    {
        if (payload?.Channel == null || payload.Options == null)
        {
            return BadRequest("Channel and Options are required.");
        }

        var processed = _processingEngine.ProcessChannel(payload.Channel, payload.Options);
        return Ok(processed);
    }

    [HttpPost("compare")]
    public IActionResult CompareRuns([FromBody] CompareRunsPayload payload)
    {
        if (payload?.RunA == null || payload.RunB == null)
        {
            return BadRequest("Both RunA and RunB datasets are required.");
        }

        var result = _comparisonService.CompareDataSets(
            payload.RunA,
            payload.RunB,
            payload.Mode,
            payload.TriggerChannel,
            payload.TriggerThreshold);

        return Ok(result);
    }

    [HttpPost("gps-sync")]
    public IActionResult SynchronizeGps([FromBody] GpsSyncPayload payload)
    {
        if (payload?.DataSet == null) return BadRequest("DataSet is required.");

        GpsSyncEngine.SynchronizeGpsWithCan(payload.DataSet, payload.CanMonotonicOffsetMs);
        return Ok(payload.DataSet);
    }

    [HttpGet("dataset/{id}")]
    public IActionResult GetCachedDataSet(string id)
    {
        if (_cachedDataSets.TryGetValue(id, out var ds))
        {
            return Ok(ds);
        }
        return NotFound();
    }

    [HttpGet("datasets")]
    public IActionResult GetCachedDataSets()
    {
        var list = _cachedDataSets.Values.Select(ds => new
        {
            id = ds.DatasetId,
            name = ds.Name,
            sourceFormat = ds.SourceFormat.ToString(),
            startTimeUtc = ds.StartTimeUtc,
            endTimeUtc = ds.EndTimeUtc,
            durationSeconds = ds.DurationSeconds,
            channelCount = ds.Channels.Count,
            channelNames = ds.Channels.Keys.ToList(),
            gpsPointCount = ds.GpsTrack.Count
        });

        return Ok(list);
    }

    [HttpGet("session/{sessionId}/canonical")]
    public IActionResult ConvertSessionToCanonical(string sessionId)
    {
        var session = _loggerService.GetSession(sessionId);
        if (session == null) return NotFound("Session not found.");

        var ds = new CanonicalLogDataSet
        {
            DatasetId = session.Id,
            Name = session.Name,
            SourceFormat = LogFileFormat.GenericCanCsv,
            StartTimeUtc = session.StartTimeUtc,
            EndTimeUtc = session.EndTimeUtc ?? DateTime.UtcNow
        };

        var frameRateChan = new TimeSeriesChannel("CanBusPacketCount", "frames", "CAN");
        var ch0CountChan = new TimeSeriesChannel("Can0Count", "frames", "CAN");
        var ch1CountChan = new TimeSeriesChannel("Can1Count", "frames", "CAN");

        double totalFrames = 0;
        double ch0Count = 0;
        double ch1Count = 0;

        uint firstTsUs = session.Frames.Count > 0 ? session.Frames[0].TimestampUs : 0;

        for (int i = 0; i < session.Frames.Count; i++)
        {
            var f = session.Frames[i];
            double ms = (f.TimestampUs >= firstTsUs ? f.TimestampUs - firstTsUs : f.TimestampUs) / 1000.0;
            totalFrames++;
            if (f.Channel == 1) ch1Count++;
            else ch0Count++;

            if (i % 25 == 0 || i == session.Frames.Count - 1)
            {
                frameRateChan.AddPoint(ms, totalFrames);
                ch0CountChan.AddPoint(ms, ch0Count);
                ch1CountChan.AddPoint(ms, ch1Count);
            }
        }

        ds.Channels[frameRateChan.Name] = frameRateChan;
        ds.Channels[ch0CountChan.Name] = ch0CountChan;
        ds.Channels[ch1CountChan.Name] = ch1CountChan;

        _cachedDataSets[ds.DatasetId] = ds;

        return Ok(ds);
    }
}
