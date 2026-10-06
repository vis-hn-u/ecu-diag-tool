using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.EcuEngineering.Analysis;
using CanEngineeringPlatform.Core.EcuEngineering.Compatibility;
using CanEngineeringPlatform.Core.EcuEngineering.Core;
using CanEngineeringPlatform.Core.EcuEngineering.Identification;
using CanEngineeringPlatform.Core.EcuEngineering.Images;
using CanEngineeringPlatform.Core.EcuEngineering.Memory;
using CanEngineeringPlatform.Core.EcuEngineering.Protocols;
using CanEngineeringPlatform.Core.EcuEngineering.Reading;
using CanEngineeringPlatform.Core.FlashAnalysis;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/ecu-engineering")]
public class EcuEngineeringController : ControllerBase
{
    private readonly IEcuEngineeringProvider _provider;
    private readonly IFlashAnalyzerService _flashAnalyzer;

    public EcuEngineeringController(
        IEcuEngineeringProvider provider,
        IFlashAnalyzerService flashAnalyzer)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _flashAnalyzer = flashAnalyzer ?? throw new ArgumentNullException(nameof(flashAnalyzer));
    }

    #region Phase 1 Endpoints (Preserved)

    [HttpGet("compatibility/catalog")]
    public ActionResult<IReadOnlyList<EcuFamilyProfile>> GetCompatibilityCatalog()
    {
        var profiles = _provider.Compatibility.GetAllProfiles();
        return Ok(profiles);
    }

    [HttpGet("compatibility/{familyId}")]
    public ActionResult<EcuFamilyProfile> GetCompatibilityProfile(string familyId)
    {
        var profile = _provider.Compatibility.GetProfileById(familyId);
        if (profile == null) return NotFound(new { message = $"ECU family '{familyId}' not found in compatibility database." });
        return Ok(profile);
    }

    [HttpPost("discovery/probe")]
    public async Task<ActionResult<EcuProtocolCapabilityMatrix>> ProbeProtocol([FromBody] ProbeProtocolRequest request)
    {
        uint reqId = request.RequestCanId ?? 0x7E0;
        uint respId = request.ResponseCanId ?? 0x7E8;
        string name = string.IsNullOrWhiteSpace(request.EcuName) ? "ECM" : request.EcuName;

        var matrix = await _provider.ProtocolDiscovery.DiscoverProtocolAsync(reqId, respId, name);
        return Ok(matrix);
    }

    [HttpGet("discovery/matrices")]
    public ActionResult<IReadOnlyList<EcuProtocolCapabilityMatrix>> GetDiscoveredMatrices()
    {
        return Ok(_provider.ProtocolDiscovery.GetDiscoveredMatrices());
    }

    [HttpPost("identification/identify")]
    public async Task<ActionResult<EcuIdentificationProfile>> IdentifyEcu([FromBody] IdentifyEcuRequest request)
    {
        uint reqId = request.RequestCanId ?? 0x7E0;
        uint respId = request.ResponseCanId ?? 0x7E8;
        string name = string.IsNullOrWhiteSpace(request.EcuName) ? "ECM" : request.EcuName;

        var profile = await _provider.Identification.IdentifyEcuAsync(reqId, respId, name);
        return Ok(profile);
    }

    [HttpGet("identification/profiles")]
    public ActionResult<IReadOnlyList<EcuIdentificationProfile>> GetAllProfiles()
    {
        return Ok(_provider.Identification.GetAllIdentifiedEcus());
    }

    [HttpGet("identification/profiles/{ecuName}")]
    public ActionResult<EcuIdentificationProfile> GetProfileByName(string ecuName)
    {
        var profile = _provider.Identification.GetProfile(ecuName);
        if (profile == null) return NotFound(new { message = $"No profile found for ECU '{ecuName}'." });
        return Ok(profile);
    }

    #endregion

    #region Phase 2 Memory Map & Read Endpoints

    [HttpPost("memory/discover")]
    public async Task<ActionResult<EcuMemoryMap>> DiscoverMemoryMap([FromBody] DiscoverMemoryRequest request)
    {
        uint reqId = request.RequestCanId ?? 0x7E0;
        uint respId = request.ResponseCanId ?? 0x7E8;
        string name = string.IsNullOrWhiteSpace(request.EcuName) ? "ECM" : request.EcuName;

        // Retrieve existing identification or run fresh
        var idProfile = _provider.Identification.GetProfile(name)
            ?? await _provider.Identification.IdentifyEcuAsync(reqId, respId, name);

        var protoMatrix = _provider.ProtocolDiscovery.GetMatrixForEcu(name);

        var memoryMap = await _provider.MemoryDiscovery.DiscoverMemoryMapAsync(idProfile, protoMatrix);
        return Ok(memoryMap);
    }

    [HttpGet("memory/map/{ecuName}")]
    public ActionResult<EcuMemoryMap> GetMemoryMap(string ecuName)
    {
        var map = _provider.MemoryDiscovery.GetCachedMemoryMap(ecuName);
        if (map == null) return NotFound(new { message = $"No discovered memory map found for ECU '{ecuName}'." });
        return Ok(map);
    }

    [HttpGet("memory/capabilities/{ecuName}")]
    public async Task<ActionResult<EcuReadCapabilityMatrix>> GetReadCapabilities(string ecuName)
    {
        var cap = _provider.MemoryDiscovery.GetCachedReadCapabilities(ecuName);
        if (cap != null) return Ok(cap);

        var map = _provider.MemoryDiscovery.GetCachedMemoryMap(ecuName);
        if (map == null) return NotFound(new { message = $"No memory map available for ECU '{ecuName}'." });

        var protoMatrix = _provider.ProtocolDiscovery.GetMatrixForEcu(ecuName);
        var matrix = await _provider.MemoryDiscovery.EvaluateReadCapabilitiesAsync(map, protoMatrix);
        return Ok(matrix);
    }

    [HttpPost("read/start")]
    public async Task<ActionResult<EcuMemoryImage>> StartSafeRead([FromBody] StartSafeReadRequest request)
    {
        if (_provider.MemoryReader == null)
        {
            return StatusCode(500, new { message = "Memory reader service is uninitialized." });
        }

        uint reqId = request.RequestCanId ?? 0x7E0;
        uint respId = request.ResponseCanId ?? 0x7E8;
        string name = string.IsNullOrWhiteSpace(request.EcuName) ? "ECM" : request.EcuName;

        // Retrieve or discover memory map
        var idProfile = _provider.Identification.GetProfile(name)
            ?? await _provider.Identification.IdentifyEcuAsync(reqId, respId, name);

        var memoryMap = _provider.MemoryDiscovery.GetCachedMemoryMap(name)
            ?? await _provider.MemoryDiscovery.DiscoverMemoryMapAsync(idProfile);

        var options = new SafeReadOptions
        {
            ChunkSize = request.ChunkSize ?? 256,
            TimeoutMs = request.TimeoutMs ?? 5000,
            InterChunkDelayMs = request.InterChunkDelayMs ?? 10,
            Environment = request.Environment ?? ExecutionEnvironment.Simulation,
            UserConfirmed = request.UserConfirmed ?? true,
            RequireUserConfirmation = true
        };

        try
        {
            var image = await _provider.MemoryReader.ReadMemoryAsync(
                idProfile,
                memoryMap,
                request.StartAddress,
                request.LengthBytes,
                request.RegionName ?? "Selected Sector",
                options);

            return Ok(image);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Safe read failed: {ex.Message}" });
        }
    }

    [HttpGet("read/sessions")]
    public ActionResult<IReadOnlyList<ReadSessionStatus>> GetAllReadSessions()
    {
        if (_provider.MemoryReader == null) return Ok(Array.Empty<ReadSessionStatus>());
        return Ok(_provider.MemoryReader.GetAllSessions());
    }

    [HttpGet("read/sessions/{sessionId:guid}")]
    public ActionResult<ReadSessionStatus> GetReadSession(Guid sessionId)
    {
        if (_provider.MemoryReader == null) return NotFound();
        var session = _provider.MemoryReader.GetActiveSession(sessionId);
        if (session == null) return NotFound(new { message = $"Read session '{sessionId}' not found." });
        return Ok(session);
    }

    [HttpGet("images")]
    public ActionResult<IReadOnlyList<EcuMemoryImage>> GetAllImages()
    {
        return Ok(_provider.ImageStore.GetAllImages());
    }

    [HttpGet("images/{imageId}")]
    public ActionResult<EcuMemoryImage> GetImage(string imageId)
    {
        var img = _provider.ImageStore.GetImageById(imageId);
        if (img == null) return NotFound(new { message = $"Image '{imageId}' not found." });
        return Ok(img);
    }

    [HttpGet("images/{imageId}/manifest")]
    public ActionResult<ReadManifest> GetImageManifest(string imageId)
    {
        var manifest = _provider.ImageStore.GetManifest(imageId);
        if (manifest == null) return NotFound(new { message = $"Manifest for image '{imageId}' not found." });
        return Ok(manifest);
    }

    [HttpGet("images/{imageId}/raw")]
    public ActionResult DownloadImageRaw(string imageId)
    {
        var img = _provider.ImageStore.GetImageById(imageId);
        if (img == null || img.RawBytes.Length == 0) return NotFound(new { message = $"Raw content for image '{imageId}' not found." });
        string fileName = $"{img.EcuName}_{img.RegionName.Replace(" ", "_")}_{img.Sha256Hash[..Math.Min(8, img.Sha256Hash.Length)]}.bin";
        return File(img.RawBytes, "application/octet-stream", fileName);
    }

    [HttpPost("images/{imageId}/analyze")]
    public ActionResult<MemoryImageAnalysis> AnalyzeImage(string imageId)
    {
        var img = _provider.ImageStore.GetImageById(imageId);
        if (img == null || img.RawBytes.Length == 0) return NotFound(new { message = $"Image '{imageId}' not found." });
        var analysis = ImageAnalysisEngine.Analyze(img.RawBytes, img.EcuName);
        return Ok(analysis);
    }

    [HttpPost("images/{imageId}/send-to-flash-analyzer")]
    public ActionResult<EcuBinaryInfo> SendToFlashAnalyzer(string imageId)
    {
        var img = _provider.ImageStore.GetImageById(imageId);
        if (img == null || img.RawBytes.Length == 0) return NotFound(new { message = $"Image '{imageId}' not found." });
        string fileName = $"{img.EcuName}_{img.RegionName.Replace(" ", "_")}.bin";
        var binaryInfo = _flashAnalyzer.ImportBinary(fileName, img.RawBytes);
        return Ok(binaryInfo);
    }

    #endregion
}

public class ProbeProtocolRequest
{
    public uint? RequestCanId { get; set; }
    public uint? ResponseCanId { get; set; }
    public string? EcuName { get; set; }
}

public class IdentifyEcuRequest
{
    public uint? RequestCanId { get; set; }
    public uint? ResponseCanId { get; set; }
    public string? EcuName { get; set; }
}

public class DiscoverMemoryRequest
{
    public uint? RequestCanId { get; set; }
    public uint? ResponseCanId { get; set; }
    public string? EcuName { get; set; }
}

public class StartSafeReadRequest
{
    public uint? RequestCanId { get; set; }
    public uint? ResponseCanId { get; set; }
    public string? EcuName { get; set; }
    public long StartAddress { get; set; }
    public long LengthBytes { get; set; }
    public string? RegionName { get; set; }
    public int? ChunkSize { get; set; }
    public int? TimeoutMs { get; set; }
    public int? InterChunkDelayMs { get; set; }
    public ExecutionEnvironment? Environment { get; set; }
    public bool? UserConfirmed { get; set; }
}
