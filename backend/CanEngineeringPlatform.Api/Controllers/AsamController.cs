using System;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Protocols.Asam;

namespace CanEngineeringPlatform.Api.Controllers;

public class XcpConnectDto
{
    public XcpTransportType Transport { get; set; } = XcpTransportType.XcpOnCan;
}

public class XcpDownloadDto
{
    public long Address { get; set; }
    public string DataHex { get; set; } = string.Empty;
}

[ApiController]
[Route("api/asam")]
public class AsamController : ControllerBase
{
    private readonly IAsamProtocolService _asamService;

    public AsamController(IAsamProtocolService asamService)
    {
        _asamService = asamService ?? throw new ArgumentNullException(nameof(asamService));
    }

    [HttpGet("a2l")]
    public ActionResult<A2lDatabase> GetA2lDatabase()
    {
        return Ok(_asamService.GetA2lDatabase());
    }

    [HttpGet("odx")]
    public ActionResult<OdxEcuVariant> GetOdxVariant()
    {
        return Ok(_asamService.GetOdxVariant());
    }

    [HttpGet("xcp/status")]
    public ActionResult<XcpSessionStatus> GetXcpStatus()
    {
        return Ok(_asamService.GetXcpStatus());
    }

    [HttpPost("xcp/connect")]
    public ActionResult ConnectXcp([FromBody] XcpConnectDto dto)
    {
        bool success = _asamService.ConnectXcp(dto.Transport);
        return Ok(new { success, isConnected = success, transport = dto.Transport.ToString() });
    }

    [HttpPost("xcp/disconnect")]
    public ActionResult DisconnectXcp()
    {
        _asamService.DisconnectXcp();
        return Ok(new { success = true, isConnected = false });
    }

    [HttpGet("xcp/upload")]
    public ActionResult Upload([FromQuery] long address, [FromQuery] byte length = 8)
    {
        byte[] bytes = _asamService.XcpShortUpload(address, length);
        return Ok(new { address, length, dataHex = BitConverter.ToString(bytes).Replace("-", " ") });
    }

    [HttpPost("xcp/download")]
    public ActionResult Download([FromBody] XcpDownloadDto dto)
    {
        byte[] bytes = Array.Empty<byte>();
        if (!string.IsNullOrEmpty(dto.DataHex))
        {
            var parts = dto.DataHex.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
            bytes = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                byte.TryParse(parts[i], System.Globalization.NumberStyles.HexNumber, null, out bytes[i]);
            }
        }

        bool success = _asamService.XcpShortDownload(dto.Address, bytes);
        return Ok(new { success, address = dto.Address, bytesWritten = bytes.Length });
    }
}
