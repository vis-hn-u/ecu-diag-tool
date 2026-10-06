using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Diagnostics.Engine;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Core;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Health;
using CanEngineeringPlatform.Core.Diagnostics.Engine.LiveData;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Normalization;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Plausibility;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Reset;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Routines;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Session;
using CanEngineeringPlatform.Core.Diagnostics.Engine.Trace;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;

namespace CanEngineeringPlatform.Api.Controllers;

public class SwitchSessionRequestDto
{
    public uint RequestCanId { get; set; } = 0x7E0;
    public DiagnosticSessionType SessionType { get; set; } = DiagnosticSessionType.ExtendedDiagnosticSession;
    public string EcuName { get; set; } = "ECM";
}

public class TesterPresentRequestDto
{
    public uint RequestCanId { get; set; } = 0x7E0;
    public string EcuName { get; set; } = "ECM";
    public int IntervalMs { get; set; } = 2000;
    public bool SuppressResponse { get; set; } = false;
    public bool Enable { get; set; } = true;
}

public class ReadDtcsRequestDto
{
    public uint RequestCanId { get; set; } = 0x7E0;
    public byte StatusMask { get; set; } = 0xFF;
    public string EcuName { get; set; } = "ECM";
}

public class PollLiveDataRequestDto
{
    public uint RequestCanId { get; set; } = 0x7E0;
    public string ParameterId { get; set; } = "ENGINE_RPM";
    public string EcuName { get; set; } = "ECM";
}

public class StartPollingRequestDto
{
    public uint RequestCanId { get; set; } = 0x7E0;
    public List<string> ParameterIds { get; set; } = new();
    public int IntervalMs { get; set; } = 250;
    public string EcuName { get; set; } = "ECM";
}

public class SimFaultInjectionDto
{
    public string EcuName { get; set; } = "ECM";
    public string? DtcCode { get; set; }
    public bool DtcActive { get; set; } = true;
    public double? Voltage { get; set; }
}

[ApiController]
[Route("api/diagnostic-engine")]
public class DiagnosticEngineController : ControllerBase
{
    private readonly IDiagnosticEngine _engine;
    private readonly IEcuSimulator _simulator;

    public DiagnosticEngineController(IDiagnosticEngine engine, IEcuSimulator simulator)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
    }

    // --- 1. Session Management & TesterPresent ---
    [HttpGet("session/status")]
    public ActionResult<DiagnosticSessionStatus> GetSessionStatus()
    {
        return Ok(_engine.SessionManager.GetStatus());
    }

    [HttpPost("session/switch")]
    public async Task<ActionResult<bool>> SwitchSession([FromBody] SwitchSessionRequestDto dto)
    {
        bool ok = await _engine.SessionManager.SwitchSessionAsync(dto.RequestCanId, dto.SessionType, dto.EcuName);
        if (!ok) return BadRequest(_engine.SessionManager.GetStatus().LastError ?? "Session switch failed or rejected by ECU.");
        return Ok(ok);
    }

    [HttpPost("session/tester-present")]
    public ActionResult TesterPresent([FromBody] TesterPresentRequestDto dto)
    {
        if (dto.Enable)
        {
            _engine.SessionManager.StartTesterPresent(dto.RequestCanId, dto.EcuName, dto.IntervalMs, dto.SuppressResponse);
        }
        else
        {
            _engine.SessionManager.StopTesterPresent();
        }
        return Ok(new { success = true, isTesterPresentActive = dto.Enable });
    }

    // --- 2. DTCs & Clear Safety ---
    [HttpGet("dtcs")]
    public ActionResult<IReadOnlyList<DtcRecord>> GetDtcs([FromQuery] string? ecuName = null, [FromQuery] bool historical = false)
    {
        if (historical)
        {
            return Ok(_engine.DtcService.GetHistoricalDtcs(ecuName));
        }
        return Ok(_engine.DtcService.GetActiveDtcs(ecuName));
    }

    [HttpPost("dtcs/read")]
    public async Task<ActionResult<IReadOnlyList<DtcRecord>>> ReadDtcs([FromBody] ReadDtcsRequestDto dto)
    {
        var dtcs = await _engine.DtcService.ReadDtcsByStatusMaskAsync(dto.RequestCanId, dto.StatusMask, dto.EcuName);
        return Ok(dtcs);
    }

    [HttpPost("dtcs/clear-safe")]
    public async Task<ActionResult<DtcClearResult>> ClearDtcsSafe([FromBody] DtcClearRequest request)
    {
        try
        {
            var result = await _engine.DtcService.ClearDtcsWithSafetyAsync(request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
    }

    [HttpGet("dtcs/clear-history")]
    public ActionResult<IReadOnlyList<DtcClearResult>> GetClearHistory([FromQuery] string? ecuName = null)
    {
        return Ok(_engine.DtcService.GetClearHistory(ecuName));
    }

    // --- 3. Live Data & Quality ---
    [HttpGet("live-data/definitions")]
    public ActionResult<IReadOnlyList<LiveDataParameterDefinition>> GetLiveDataDefinitions()
    {
        return Ok(_engine.LiveDataService.GetSupportedDefinitions());
    }

    [HttpGet("live-data/samples")]
    public ActionResult<IReadOnlyList<LiveDataSample>> GetLiveDataSamples()
    {
        return Ok(_engine.LiveDataService.GetLatestSamples());
    }

    [HttpPost("live-data/poll")]
    public async Task<ActionResult<LiveDataSample>> PollLiveDataParameter([FromBody] PollLiveDataRequestDto dto)
    {
        var sample = await _engine.LiveDataService.PollParameterAsync(dto.RequestCanId, dto.ParameterId, dto.EcuName);
        if (sample == null) return NotFound($"Parameter '{dto.ParameterId}' not found.");
        return Ok(sample);
    }

    [HttpPost("live-data/start-polling")]
    public ActionResult StartPolling([FromBody] StartPollingRequestDto dto)
    {
        _engine.LiveDataService.StartPolling(dto.RequestCanId, dto.ParameterIds, dto.IntervalMs, dto.EcuName);
        return Ok(new { success = true, isPolling = true });
    }

    [HttpPost("live-data/stop-polling")]
    public ActionResult StopPolling()
    {
        _engine.LiveDataService.StopPolling();
        return Ok(new { success = true, isPolling = false });
    }

    // --- 4. Sensor Plausibility ---
    [HttpGet("plausibility")]
    public ActionResult<SensorPlausibilityReport> GetPlausibilityReport([FromQuery] string ecuName = "ECM")
    {
        return Ok(_engine.PlausibilityService.EvaluatePlausibility(ecuName));
    }

    // --- 5. Routine Control ---
    [HttpGet("routines")]
    public ActionResult<IReadOnlyList<RoutineDescriptor>> GetRoutines()
    {
        return Ok(_engine.RoutineService.GetSupportedRoutines());
    }

    [HttpPost("routines/execute")]
    public async Task<ActionResult<RoutineExecutionResult>> ExecuteRoutine([FromBody] RoutineExecutionRequest request)
    {
        try
        {
            var res = await _engine.RoutineService.ExecuteRoutineAsync(request);
            return Ok(res);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("routines/history")]
    public ActionResult<IReadOnlyList<RoutineExecutionResult>> GetRoutineHistory()
    {
        return Ok(_engine.RoutineService.GetExecutionHistory());
    }

    // --- 6. ECU Reset ---
    [HttpPost("reset")]
    public async Task<ActionResult<EcuResetResult>> ExecuteReset([FromBody] EcuResetRequest request)
    {
        try
        {
            var res = await _engine.ResetService.ExecuteResetAsync(request);
            return Ok(res);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpGet("reset/history")]
    public ActionResult<IReadOnlyList<EcuResetResult>> GetResetHistory()
    {
        return Ok(_engine.ResetService.GetResetHistory());
    }

    // --- 7. Diagnostic Traces ---
    [HttpGet("traces")]
    public ActionResult<IReadOnlyList<DiagnosticTraceEntry>> GetRecentTraces([FromQuery] int count = 200)
    {
        return Ok(_engine.TraceService.GetRecentTraces(count));
    }

    [HttpGet("traces/search")]
    public ActionResult<IReadOnlyList<DiagnosticTraceEntry>> SearchTraces(
        [FromQuery] uint? canId = null,
        [FromQuery] byte? serviceId = null,
        [FromQuery] DiagnosticExchangeResult? result = null,
        [FromQuery] int count = 200)
    {
        return Ok(_engine.TraceService.SearchTraces(canId, serviceId, result, null, count));
    }

    [HttpDelete("traces")]
    public ActionResult ClearTraces()
    {
        _engine.TraceService.ClearTraces();
        return Ok(new { success = true });
    }

    // --- 8. ECU Health & Root-Cause Assistant ---
    [HttpGet("health")]
    public ActionResult<EcuHealthReport> GetHealthReport([FromQuery] string ecuName = "ECM")
    {
        return Ok(_engine.HealthManager.EvaluateHealth(ecuName));
    }

    // --- 9. OEM Normalization ---
    [HttpGet("oem-normalization")]
    public ActionResult<OemNormalizationPlan> GetOemNormalizationPlan(
        [FromQuery] string ecuName = "ECM",
        [FromQuery] string? hardwareNumber = null,
        [FromQuery] string? softwareNumber = null)
    {
        return Ok(_engine.NormalizationService.GenerateNormalizationPlan(ecuName, hardwareNumber, softwareNumber));
    }

    // --- 10. Simulator Fault Injection ---
    [HttpPost("simulator/fault")]
    public ActionResult InjectSimulatorFault([FromBody] SimFaultInjectionDto dto)
    {
        if (!string.IsNullOrEmpty(dto.DtcCode))
        {
            _simulator.SetDtcFault(dto.EcuName, dto.DtcCode, dto.DtcActive);
        }
        if (dto.Voltage.HasValue)
        {
            _simulator.SetSimulatedVoltage(dto.EcuName, dto.Voltage.Value);
        }
        return Ok(new { success = true, message = "Simulation fault state updated." });
    }
}
