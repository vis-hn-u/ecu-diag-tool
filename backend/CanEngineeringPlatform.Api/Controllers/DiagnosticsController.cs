using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using CanEngineeringPlatform.Api.Hubs;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.EcuInfo;
using CanEngineeringPlatform.Core.Diagnostics.FreezeFrame;
using CanEngineeringPlatform.Core.Diagnostics.Manufacturer;
using CanEngineeringPlatform.Core.Diagnostics.Obd;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.Diagnostics.Uds;
using CanEngineeringPlatform.Core.Diagnostics.Vag;

namespace CanEngineeringPlatform.Api.Controllers;

public class DiagnosticRequestDto
{
    public uint RequestCanId { get; set; } = 0x7E0;
    public string EcuName { get; set; } = "ECM";
    public string HexPayload { get; set; } = "22 F1 90";
    public bool IsSimulation { get; set; } = true;
}

public class DtcFaultInjectionDto
{
    public string EcuName { get; set; } = "ECM";
    public string DtcCode { get; set; } = "P0300";
    public bool Active { get; set; } = true;
}

public class VagReadGroupDto
{
    public int GroupNumber { get; set; } = 1;
    public double Rpm { get; set; } = 800;
    public double BoostBar { get; set; } = 0.0;
    public double LoadPct { get; set; } = 15.0;
    public string? RawHexPayload { get; set; }
}

public class DtcCorrelationRequestDto
{
    public string DtcCode { get; set; } = "P0300";
}

[ApiController]
[Route("api/[controller]")]
public class DiagnosticsController : ControllerBase
{
    private readonly IDiagnosticSessionService _diagnosticService;
    private readonly IEcuSimulator _ecuSimulator;
    private readonly IFreezeFrameSnapshotService _freezeFrameService;
    private readonly IDtcCorrelationService _dtcCorrelationService;
    private readonly IVagMeasuringBlockService _vagService;
    private readonly IManufacturerLiveDataService _manufacturerService;
    private readonly IHubContext<CanHub, ICanHubClient> _hubContext;

    public DiagnosticsController(
        IDiagnosticSessionService diagnosticService,
        IEcuSimulator ecuSimulator,
        IFreezeFrameSnapshotService freezeFrameService,
        IDtcCorrelationService dtcCorrelationService,
        IVagMeasuringBlockService vagService,
        IManufacturerLiveDataService manufacturerService,
        IHubContext<CanHub, ICanHubClient> hubContext)
    {
        _diagnosticService = diagnosticService;
        _ecuSimulator = ecuSimulator;
        _freezeFrameService = freezeFrameService;
        _dtcCorrelationService = dtcCorrelationService;
        _vagService = vagService;
        _manufacturerService = manufacturerService;
        _hubContext = hubContext;

        _diagnosticService.OnExchangeUpdated += async exchange =>
        {
            await _hubContext.Clients.All.ReceiveDiagnosticExchange(exchange);
        };

        _diagnosticService.OnDtcsUpdated += async dtcs =>
        {
            await _hubContext.Clients.All.ReceiveDtcUpdate(dtcs);
        };
    }

    [HttpGet("exchanges")]
    public ActionResult<IReadOnlyList<DiagnosticExchange>> GetRecentExchanges([FromQuery] int count = 100)
    {
        return Ok(_diagnosticService.GetRecentExchanges(count));
    }

    [HttpPost("send")]
    public async Task<ActionResult<DiagnosticExchange>> SendRequest([FromBody] DiagnosticRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.HexPayload))
        {
            return BadRequest("HexPayload cannot be empty");
        }

        try
        {
            byte[] payload = ParseHexBytes(dto.HexPayload);
            var exchange = await _diagnosticService.SendRequestAsync(dto.RequestCanId, payload, dto.EcuName, dto.IsSimulation);
            return Ok(exchange);
        }
        catch (Exception ex)
        {
            return BadRequest($"Invalid hex payload or request failure: {ex.Message}");
        }
    }

    [HttpGet("dtcs")]
    public ActionResult<IReadOnlyList<DiagnosticTroubleCode>> GetDtcs()
    {
        return Ok(_diagnosticService.GetActiveDtcList());
    }

    [HttpPost("clear-dtcs")]
    public async Task<IActionResult> ClearDtcs([FromQuery] uint requestCanId = 0x7E0, [FromQuery] string ecuName = "ECM", [FromQuery] bool isSimulation = true)
    {
        await _diagnosticService.ClearDtcsAsync(requestCanId, ecuName, isSimulation);
        return Ok(new { success = true, message = $"DTCs cleared for {ecuName}" });
    }

    [HttpPost("scan-all-dtcs")]
    public async Task<ActionResult<IReadOnlyList<DiagnosticTroubleCode>>> ScanAllDtcs([FromQuery] bool isSimulation = true)
    {
        await _diagnosticService.ScanAllDtcsAsync(isSimulation);
        return Ok(_diagnosticService.GetActiveDtcList());
    }

    [HttpGet("ecu-info")]
    public ActionResult<IReadOnlyList<EcuIdentification>> GetEcuIdentifications()
    {
        return Ok(_diagnosticService.GetEcuIdentifications());
    }

    [HttpGet("obd-live")]
    public ActionResult<object> GetObdLiveValues()
    {
        var values = _diagnosticService.GetLatestObdValues();
        var definitions = ObdDecoder.GetAllPidDefinitions();
        return Ok(new
        {
            values,
            supportedDefinitions = definitions
        });
    }

    // Freeze-Frame / Event Snapshots
    [HttpGet("freeze-frames")]
    public ActionResult<IReadOnlyList<DiagnosticFreezeFrame>> GetAllFreezeFrames()
    {
        return Ok(_freezeFrameService.GetAllSnapshots());
    }

    [HttpGet("freeze-frames/{id}")]
    public ActionResult<DiagnosticFreezeFrame> GetFreezeFrameById(string id)
    {
        var snap = _freezeFrameService.GetSnapshotById(id);
        if (snap == null) return NotFound($"Freeze frame snapshot '{id}' not found");
        return Ok(snap);
    }

    [HttpGet("freeze-frames/dtc/{code}")]
    public ActionResult<DiagnosticFreezeFrame> GetFreezeFrameForDtc(string code)
    {
        var snap = _freezeFrameService.GetSnapshotForDtc(code) ?? _freezeFrameService.GenerateSimulatedFreezeFrame(code);
        return Ok(snap);
    }

    // DTC Metadata & Contextual Signal Correlation
    [HttpGet("dtc-metadata")]
    public ActionResult<IReadOnlyList<DtcMetadata>> GetAllDtcMetadata()
    {
        return Ok(_dtcCorrelationService.GetAllKnownDtcMetadata());
    }

    [HttpGet("dtc-metadata/{code}")]
    public ActionResult<DtcMetadata> GetDtcMetadata(string code)
    {
        var meta = _dtcCorrelationService.GetMetadata(code);
        if (meta == null)
        {
            return Ok(new DtcMetadata
            {
                Code = code,
                Title = $"{code} Diagnostic Fault Code",
                TechnicalExplanation = "Manufacturer specific or standard diagnostic trouble code.",
                LikelyCauses = new List<string> { "Sensor circuit malfunction", "Wiring harness short/open", "Component mechanical failure" },
                DiagnosticSteps = new List<string> { "Inspect freeze frame data", "Perform component live data test", "Verify wiring continuity" }
            });
        }
        return Ok(meta);
    }

    [HttpPost("correlate-dtc")]
    public ActionResult<DtcCorrelationResult> CorrelateDtc([FromBody] DtcCorrelationRequestDto dto)
    {
        var result = _dtcCorrelationService.GenerateSimulatedCorrelation(dto.DtcCode);
        return Ok(result);
    }

    // VAG Measuring Blocks (Messwertblöcke)
    [HttpGet("vag/groups")]
    public ActionResult<IReadOnlyList<VagMeasuringGroup>> GetVagGroups()
    {
        return Ok(_vagService.GetAllSupportedGroups());
    }

    [HttpGet("vag/groups/{groupNumber}")]
    public ActionResult<VagMeasuringGroup> GetVagGroup(int groupNumber)
    {
        var g = _vagService.GetGroup(groupNumber);
        if (g == null) return NotFound($"VAG Group {groupNumber} not found");
        return Ok(g);
    }

    [HttpPost("vag/read-group")]
    public ActionResult<VagMeasuringGroup> ReadVagGroup([FromBody] VagReadGroupDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.RawHexPayload))
        {
            byte[] payload = ParseHexBytes(dto.RawHexPayload);
            return Ok(_vagService.DecodeGroupPayload(dto.GroupNumber, payload));
        }

        return Ok(_vagService.GetSimulatedGroup(dto.GroupNumber, dto.Rpm, dto.BoostBar, dto.LoadPct));
    }

    // Manufacturer Live-Data Definitions
    [HttpGet("manufacturer/definitions")]
    public ActionResult<IReadOnlyList<ManufacturerLiveDataDefinition>> GetManufacturerDefinitions([FromQuery] ManufacturerOem? oem = null)
    {
        if (oem.HasValue) return Ok(_manufacturerService.GetDefinitionsByOem(oem.Value));
        return Ok(_manufacturerService.GetAllDefinitions());
    }

    [HttpGet("manufacturer/snapshot")]
    public ActionResult<IReadOnlyList<DecodedManufacturerValue>> GetManufacturerSnapshot(
        [FromQuery] ManufacturerOem oem = ManufacturerOem.VAG,
        [FromQuery] double rpm = 2500,
        [FromQuery] double boostBar = 0.5,
        [FromQuery] double loadPct = 45.0)
    {
        return Ok(_manufacturerService.GetSimulatedSnapshot(oem, rpm, boostBar, loadPct));
    }

    // Simulator Controls
    [HttpGet("simulator/status")]
    public ActionResult<object> GetSimulatorStatus()
    {
        return Ok(new
        {
            isEnabled = _ecuSimulator.IsEnabled,
            virtualEcus = _ecuSimulator.GetVirtualEcus()
        });
    }

    [HttpPost("simulator/toggle")]
    public IActionResult ToggleSimulator([FromQuery] bool enabled)
    {
        _ecuSimulator.IsEnabled = enabled;
        return Ok(new { isEnabled = _ecuSimulator.IsEnabled });
    }

    [HttpPost("simulator/dtc-fault")]
    public IActionResult SetDtcFault([FromBody] DtcFaultInjectionDto dto)
    {
        _ecuSimulator.SetDtcFault(dto.EcuName, dto.DtcCode, dto.Active);
        return Ok(new { success = true, message = $"DTC {dto.DtcCode} set to {dto.Active} on {dto.EcuName}" });
    }

    [HttpPost("simulator/reset")]
    public IActionResult ResetSimulator()
    {
        _ecuSimulator.ResetAll();
        return Ok(new { success = true, message = "Virtual ECUs reset to default state" });
    }

    [HttpDelete("history")]
    public IActionResult ClearHistory()
    {
        _diagnosticService.ClearHistory();
        return Ok(new { success = true });
    }

    private static byte[] ParseHexBytes(string hex)
    {
        var cleaned = hex.Replace(" ", "").Replace("0x", "").Replace(",", "").Replace("-", "");
        if (cleaned.Length % 2 != 0)
        {
            cleaned = "0" + cleaned;
        }

        byte[] bytes = new byte[cleaned.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}
