using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.FlashAnalysis;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/flash-analyzer")]
public class FlashAnalyzerController : ControllerBase
{
    private readonly IFlashAnalyzerService _flashService;

    public FlashAnalyzerController(IFlashAnalyzerService flashService)
    {
        _flashService = flashService;
    }

    [HttpGet("binary")]
    public ActionResult<EcuBinaryInfo> GetActiveBinary()
    {
        var bin = _flashService.GetActiveBinary();
        if (bin == null) return NotFound("No binary loaded");
        return Ok(bin);
    }

    [HttpGet("objects")]
    public ActionResult<IReadOnlyList<CalibrationObject>> GetObjects()
    {
        var bin = _flashService.GetActiveBinary();
        return Ok(bin?.Objects ?? new List<CalibrationObject>());
    }

    [HttpGet("object/{id}")]
    public ActionResult<CalibrationObject> GetObject(string id)
    {
        var obj = _flashService.GetCalibrationObject(id);
        if (obj == null) return NotFound($"Object {id} not found");
        return Ok(obj);
    }

    [HttpGet("checksums")]
    public ActionResult<IReadOnlyList<ChecksumInfo>> GetChecksums()
    {
        var bin = _flashService.GetActiveBinary();
        return Ok(bin?.Checksums ?? new List<ChecksumInfo>());
    }

    [HttpGet("correlations")]
    public ActionResult<IReadOnlyList<LiveFlashCorrelation>> GetCorrelations()
    {
        return Ok(_flashService.GetLiveFlashCorrelations());
    }

    [HttpGet("hexdump")]
    public ActionResult<string> GetHexDump([FromQuery] long offset = 0, [FromQuery] int length = 256)
    {
        return Ok(new { offset, dump = _flashService.GetHexDump(offset, length) });
    }

    [HttpPost("load-sample")]
    public ActionResult<EcuBinaryInfo> LoadSample()
    {
        return Ok(_flashService.LoadSampleEcuBinary());
    }

    [HttpPost("upload")]
    public async Task<ActionResult<EcuBinaryInfo>> UploadBinary([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        byte[] bytes = ms.ToArray();

        var info = _flashService.ImportBinary(file.FileName, bytes);
        return Ok(info);
    }

    [HttpPost("compare-sample")]
    public ActionResult<BinaryDiffResult> CompareSample()
    {
        // Compare active original against a synthetic modified stage binary
        var active = _flashService.GetActiveBinary();
        if (active == null)
        {
            _flashService.LoadSampleEcuBinary();
        }

        // Generate synthetic modified bytes for comparison
        byte[] origBytes = new byte[1024 * 1024];
        byte[] modBytes = new byte[1024 * 1024];
        var rng = new Random(42);
        rng.NextBytes(origBytes);
        Array.Copy(origBytes, modBytes, origBytes.Length);

        // Modify 48 bytes in torque and boost calibration regions
        for (int i = 0x1A240; i < 0x1A240 + 24; i++) modBytes[i] = (byte)(origBytes[i] + 5);
        for (int i = 0x22100; i < 0x22100 + 24; i++) modBytes[i] = (byte)(origBytes[i] + 12);

        var diff = _flashService.CompareBinaries(origBytes, modBytes, "Stock_MED17.bin", "Modified_Calibration.bin");
        return Ok(diff);
    }
}
