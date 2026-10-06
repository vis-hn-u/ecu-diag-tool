using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Replay;
using CanEngineeringPlatform.Core.Topology;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReplayController : ControllerBase
{
    private readonly ICanReplayService _replayService;
    private readonly ICanLoggerService _loggerService;
    private readonly ITopologyEngine _topologyEngine;

    public ReplayController(
        ICanReplayService replayService,
        ICanLoggerService loggerService,
        ITopologyEngine topologyEngine)
    {
        _replayService = replayService;
        _loggerService = loggerService;
        _topologyEngine = topologyEngine;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(_replayService.GetStatus());
    }

    [HttpPost("load-session/{sessionId}")]
    public IActionResult LoadSession(string sessionId)
    {
        var session = _loggerService.GetSession(sessionId);
        if (session == null || session.Frames.Count == 0)
        {
            return BadRequest("Session not found or contains no frames");
        }

        _replayService.LoadFrames(session.Frames, session.Name);
        _topologyEngine.SetMode(TopologyMode.Simulation);
        return Ok(_replayService.GetStatus());
    }

    [HttpPost("upload-csv")]
    public async Task<IActionResult> UploadCsv(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        using var streamReader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        string csv = await streamReader.ReadToEndAsync();
        if (_replayService.LoadFromCsv(csv, file.FileName))
        {
            _topologyEngine.SetMode(TopologyMode.Simulation);
            return Ok(_replayService.GetStatus());
        }

        return BadRequest("Failed to parse CAN CSV format");
    }

    [HttpPost("generate-sample")]
    public IActionResult GenerateSampleLog()
    {
        var frames = new List<CanFrame>();
        uint baseTs = 1000000;

        // Generate 30 seconds of realistic automotive CAN data
        for (int i = 0; i < 600; i++)
        {
            uint ts = baseTs + (uint)(i * 50000); // 50ms intervals
            double t = i * 0.05;

            // 1. EngineData (0x118 / 280 dec) from ECM (100Hz / 20ms)
            // EngineSpeed: 800 + 3000 * sin(t * 0.2)^2
            ushort rpm = (ushort)((800 + 2500 * Math.Pow(Math.Sin(t * 0.15), 2)) / 0.125);
            byte torque = (byte)(Math.Max(0, 50 + 80 * Math.Sin(t * 0.15)) + 100);
            byte throttle = (byte)(Math.Max(0, 10 + 60 * Math.Sin(t * 0.15)) / 0.392);
            byte coolant = (byte)(88 + 40); // 88 degC
            ushort fuelRail = (ushort)(1200 / 0.1); // 1200 bar

            byte[] d1 = new byte[8];
            d1[0] = (byte)(rpm & 0xFF);
            d1[1] = (byte)((rpm >> 8) & 0xFF);
            d1[2] = torque;
            d1[3] = throttle;
            d1[4] = coolant;
            d1[5] = (byte)(fuelRail & 0xFF);
            d1[6] = (byte)((fuelRail >> 8) & 0xFF);
            d1[7] = 0;
            frames.Add(new CanFrame(ts, 280, 8, 0, d1));

            // 2. VehicleDynamics (0x186 / 390 dec) from ABS (50Hz / 20ms)
            // VehicleSpeed: 20 + 80 * sin(t * 0.1)^2
            ushort speed = (ushort)((20 + 75 * Math.Pow(Math.Sin(t * 0.15), 2)) / 0.01);
            byte brake = (byte)(Math.Sin(t * 0.3) > 0.7 ? 45 : 0);
            ushort wheelSpd = (ushort)((speed * 0.01) / 0.25);

            byte[] d2 = new byte[8];
            d2[0] = (byte)(speed & 0xFF);
            d2[1] = (byte)((speed >> 8) & 0xFF);
            d2[2] = brake;
            d2[3] = (byte)(wheelSpd & 0xFF);
            d2[4] = (byte)((wheelSpd >> 8) & 0x03);
            d2[5] = (byte)(brake > 30 ? 1 : 0); // AbsActive
            frames.Add(new CanFrame(ts + 5000, 390, 8, 0, d2));

            // 3. BodyStatus (0x208 / 520 dec) from BCM (20Hz)
            if (i % 2 == 0)
            {
                short steer = (short)((120 * Math.Sin(t * 0.25)) / 0.1);
                byte steerLow = (byte)(steer & 0xFF);
                byte steerHigh = (byte)((steer >> 8) & 0xFF);
                byte lights = 1;
                byte leftSig = (byte)((int)(t * 2) % 2 == 0 ? 1 : 0);
                byte ambient = (byte)((24 + 40) / 0.5); // 24 degC
                byte batt = (byte)(13.8 / 0.1); // 13.8V

                byte[] d3 = new byte[6] { steerLow, steerHigh, (byte)(lights | (leftSig << 1)), ambient, batt, 0 };
                frames.Add(new CanFrame(ts + 10000, 520, 6, 0, d3));
            }

            // 4. ClusterDisplay (0x320 / 800 dec) from CLUSTER (10Hz)
            if (i % 5 == 0)
            {
                uint odo = (uint)(142500 + t * 0.02);
                byte fuel = (byte)(65 / 0.392); // 65%

                byte[] d4 = new byte[4];
                d4[0] = (byte)(odo & 0xFF);
                d4[1] = (byte)((odo >> 8) & 0xFF);
                d4[2] = (byte)((odo >> 16) & 0xFF);
                d4[3] = fuel;
                frames.Add(new CanFrame(ts + 15000, 800, 4, 0, d4));
            }

            // 5. Some unknown diagnostic frame (0x700) to test unknown ID handling
            if (i % 10 == 0)
            {
                frames.Add(new CanFrame(ts + 20000, 0x700, 4, 0, new byte[] { 0x01, 0x02, 0x03, 0x04 }));
            }
        }

        _replayService.LoadFrames(frames, "Simulated_Drive_Cycle.log");
        _topologyEngine.SetMode(TopologyMode.Simulation);
        return Ok(_replayService.GetStatus());
    }

    [HttpPost("play")]
    public IActionResult Play()
    {
        _replayService.Play();
        _topologyEngine.SetMode(TopologyMode.Simulation);
        return Ok(_replayService.GetStatus());
    }

    [HttpPost("pause")]
    public IActionResult Pause()
    {
        _replayService.Pause();
        _topologyEngine.SetMode(TopologyMode.Paused);
        return Ok(_replayService.GetStatus());
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        _replayService.Stop();
        _topologyEngine.SetMode(TopologyMode.Live);
        return Ok(_replayService.GetStatus());
    }

    [HttpPost("seek")]
    public IActionResult Seek([FromQuery] int index)
    {
        _replayService.Seek(index);
        return Ok(_replayService.GetStatus());
    }

    [HttpPost("speed")]
    public IActionResult SetSpeed([FromQuery] double multiplier)
    {
        _replayService.SetSpeed(multiplier);
        return Ok(_replayService.GetStatus());
    }
}
