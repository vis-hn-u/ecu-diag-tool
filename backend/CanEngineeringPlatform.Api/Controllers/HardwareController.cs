using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Api.Controllers;

public class SelectTransportRequest
{
    public string TransportId { get; set; } = string.Empty;
}

public class TransmitFrameRequest
{
    public uint Id { get; set; }
    public byte[]? Data { get; set; }
    public bool IsExtended { get; set; }
    public bool IsCanFd { get; set; }
    public bool BitRateSwitch { get; set; } = true;
    public byte Channel { get; set; } = 0;
}

public class ConfigureTransportRequest
{
    public string TransportId { get; set; } = string.Empty;
    public uint? NominalBaudRate { get; set; }
    public uint? DataBaudRate { get; set; }
    public byte? ActiveChannel { get; set; }
    public string? GatewayAddress { get; set; }
    public string? PortName { get; set; }
    public int? SerialBaudRate { get; set; }
    public bool? ListenOnly { get; set; }
}

public class GvretListenOnlyRequest
{
    public bool ListenOnly { get; set; } = true;
}

public class ElmCommandRequest
{
    public string Command { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 2;
}

[ApiController]
[Route("api/[controller]")]
public class HardwareController : ControllerBase
{
    private readonly IHardwareManager _hardwareManager;

    public HardwareController(IHardwareManager hardwareManager)
    {
        _hardwareManager = hardwareManager;
    }

    [HttpGet("transports")]
    public ActionResult<IEnumerable<HardwareStatus>> GetTransports()
    {
        var statuses = _hardwareManager.GetAllTransports().Select(t => t.GetStatus());
        return Ok(statuses);
    }

    [HttpGet("protocols")]
    public ActionResult<IEnumerable<object>> GetSupportedProtocols()
    {
        var protocols = new[]
        {
            new { Protocol = "Can", Name = "Classic CAN & CAN FD (ISO 11898-1/2)", Status = "Active", HardwareSupported = true },
            new { Protocol = "Lin", Name = "Local Interconnect Network (ISO 17987)", Status = "Architecture Ready", HardwareSupported = false },
            new { Protocol = "KLine", Name = "ISO 9141-2 / ISO 14230 K-Line", Status = "Architecture Ready", HardwareSupported = false },
            new { Protocol = "Kwp2000", Name = "Keyword Protocol 2000 (ISO 14230)", Status = "Architecture Ready", HardwareSupported = false },
            new { Protocol = "J1850Vpw", Name = "SAE J1850 VPW (10.4 kbps GM/Chrysler)", Status = "Architecture Ready", HardwareSupported = false },
            new { Protocol = "J1850Pwm", Name = "SAE J1850 PWM (41.6 kbps Ford SCP)", Status = "Architecture Ready", HardwareSupported = false },
            new { Protocol = "AutomotiveEthernet", Name = "Automotive Ethernet (100BASE-T1 / 1000BASE-T1)", Status = "Architecture Ready", HardwareSupported = false },
            new { Protocol = "DoIp", Name = "Diagnostics over IP (ISO 13400)", Status = "Architecture Ready", HardwareSupported = false },
            new { Protocol = "FlexRay", Name = "FlexRay (ISO 17458)", Status = "Architecture Ready", HardwareSupported = false },
            new { Protocol = "J1939", Name = "Commercial Vehicle SAE J1939 / J1939-22", Status = "Architecture Ready", HardwareSupported = false },
            new { Protocol = "OemSpecific", Name = "OEM-Specific Protocols (BMW-FAST, VAG TP2.0, SGW, etc.)", Status = "Architecture Ready", HardwareSupported = false }
        };
        return Ok(protocols);
    }

    [HttpGet("status")]
    public ActionResult<HardwareStatus> GetActiveStatus()
    {
        var active = _hardwareManager.GetActiveTransport();
        if (active == null)
        {
            return NotFound("No active transport configured");
        }
        return Ok(active.GetStatus());
    }

    [HttpGet("esp8266usb/ports")]
    public ActionResult<IEnumerable<string>> GetEsp8266UsbPorts()
    {
        var ports = CanEngineeringPlatform.Core.Hardware.Transports.Esp8266UsbSerialTransport.GetAvailablePorts();
        return Ok(ports);
    }

    [HttpGet("elm327/ports")]
    public ActionResult<IEnumerable<string>> GetElm327Ports()
    {
        var ports = CanEngineeringPlatform.Core.Hardware.Transports.Elm327Transport.GetAvailablePorts();
        return Ok(ports);
    }

    [HttpGet("gvret/ports")]
    public ActionResult<IEnumerable<string>> GetGvretPorts()
    {
        var ports = CanEngineeringPlatform.Core.Hardware.Transports.GvretSerialTransport.GetAvailablePorts();
        return Ok(ports);
    }

    [HttpPost("gvret/listen-only")]
    public ActionResult SetGvretListenOnly([FromBody] GvretListenOnlyRequest request)
    {
        var transport = _hardwareManager.GetTransport(CanEngineeringPlatform.Core.Hardware.Transports.GvretSerialTransport.TransportIdentifier)
                     ?? _hardwareManager.GetAllTransports().FirstOrDefault(t => t is CanEngineeringPlatform.Core.Hardware.Transports.GvretSerialTransport);

        if (transport is not CanEngineeringPlatform.Core.Hardware.Transports.GvretSerialTransport gvret)
        {
            return NotFound("GVRET transport is not registered");
        }

        gvret.SetListenOnly(request.ListenOnly);
        return Ok(gvret.GetStatus());
    }

    [HttpPost("elm327/command")]
    public async Task<IActionResult> ExecuteElm327Command([FromBody] ElmCommandRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return BadRequest("Command cannot be empty");
        }

        var transport = _hardwareManager.GetTransport("elm327-adapter") 
                     ?? _hardwareManager.GetAllTransports().FirstOrDefault(t => t is CanEngineeringPlatform.Core.Hardware.Transports.Elm327Transport);

        if (transport is not CanEngineeringPlatform.Core.Hardware.Transports.Elm327Transport elm)
        {
            return NotFound("ELM327 transport is not registered");
        }

        var response = await elm.ExecuteElmCommandAsync(request.Command, TimeSpan.FromSeconds(request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 2));
        return Ok(response);
    }

    [HttpPost("select")]
    public async Task<IActionResult> SelectTransport([FromBody] SelectTransportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TransportId))
        {
            return BadRequest("TransportId cannot be empty");
        }

        bool success = await _hardwareManager.SetActiveTransportAsync(request.TransportId);
        if (!success)
        {
            return NotFound($"Transport '{request.TransportId}' not found");
        }

        var active = _hardwareManager.GetActiveTransport();
        return Ok(active?.GetStatus());
    }

    [HttpPost("transmit")]
    public async Task<IActionResult> TransmitFrame([FromBody] TransmitFrameRequest request)
    {
        byte[] payload = request.Data ?? Array.Empty<byte>();

        CanFrame frame;
        if (request.IsCanFd)
        {
            frame = CanFrame.CreateCanFd(
                id: request.Id,
                data: payload,
                isExtended: request.IsExtended,
                bitRateSwitch: request.BitRateSwitch,
                channel: request.Channel,
                sourceId: "Api-Tx",
                direction: CanDirection.Tx
            );
        }
        else
        {
            frame = CanFrame.CreateClassic(
                id: request.Id,
                data: payload,
                isExtended: request.IsExtended,
                channel: request.Channel,
                sourceId: "Api-Tx",
                direction: CanDirection.Tx
            );
        }

        bool sent = await _hardwareManager.SendFrameAsync(frame);
        return Ok(new { success = sent, frame = frame.ToHexString(), actualLength = frame.ActualLength });
    }

    [HttpPost("configure")]
    public ActionResult<HardwareStatus> ConfigureTransport([FromBody] ConfigureTransportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TransportId))
        {
            return BadRequest("TransportId cannot be empty");
        }

        var transport = _hardwareManager.GetTransport(request.TransportId);
        if (transport == null)
        {
            return NotFound($"Transport '{request.TransportId}' not found");
        }

        if (transport is CanEngineeringPlatform.Core.Hardware.Transports.WaveshareCanFdTransport ws)
        {
            ws.Configure(request.NominalBaudRate, request.DataBaudRate, request.ActiveChannel);
        }
        else if (transport is CanEngineeringPlatform.Core.Hardware.Transports.Esp8266Transport esp)
        {
            if (!string.IsNullOrWhiteSpace(request.GatewayAddress))
            {
                esp.ConfigureAddress(request.GatewayAddress);
            }
        }
        else if (transport is CanEngineeringPlatform.Core.Hardware.Transports.Elm327Transport elm)
        {
            elm.Configure(request.PortName, request.SerialBaudRate, request.NominalBaudRate);
        }
        else if (transport is CanEngineeringPlatform.Core.Hardware.Transports.Esp8266UsbSerialTransport usbEsp)
        {
            if (!string.IsNullOrWhiteSpace(request.PortName))
            {
                usbEsp.Configure(request.PortName);
            }
        }
        else if (transport is CanEngineeringPlatform.Core.Hardware.Transports.GvretSerialTransport gvret)
        {
            uint speed = request.NominalBaudRate.GetValueOrDefault(500000u);
            int uartBaud = request.SerialBaudRate.GetValueOrDefault(1000000);
            bool listen = request.ListenOnly.GetValueOrDefault(true);
            gvret.Configure(request.PortName ?? "COM8", speed, listen, uartBaud);
        }

        return Ok(transport.GetStatus());
    }
}
