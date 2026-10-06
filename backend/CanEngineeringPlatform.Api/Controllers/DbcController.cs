using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Dbc;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DbcController : ControllerBase
{
    private readonly ICanPipeline _pipeline;

    public DbcController(ICanPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    [HttpGet("active")]
    public IActionResult GetActiveDbc()
    {
        var dbc = _pipeline.GetActiveDbc();
        if (dbc == null)
        {
            return Ok(new { isLoaded = false });
        }

        return Ok(new
        {
            isLoaded = true,
            version = dbc.Version,
            nodeCount = dbc.Nodes.Count,
            messageCount = dbc.Messages.Count,
            nodes = dbc.Nodes,
            messages = dbc.Messages
        });
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadDbc(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        using var streamReader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        string content = await streamReader.ReadToEndAsync();
        var dbc = DbcParser.Parse(content);

        _pipeline.SetActiveDbc(dbc);

        return Ok(new
        {
            success = true,
            fileName = file.FileName,
            version = dbc.Version,
            nodeCount = dbc.Nodes.Count,
            messageCount = dbc.Messages.Count
        });
    }

    [HttpPost("load-sample")]
    public IActionResult LoadSampleDbc()
    {
        string sampleDbc = @"VERSION ""1.0""

BU_: ECM BCM ABS CLUSTER HVAC

BO_ 280 EngineData: 8 ECM
 SG_ EngineSpeed : 0|16@1+ (0.125,0) [0|8000] ""rpm"" CLUSTER,BCM,ABS
 SG_ EngineTorque : 16|8@1+ (1,-100) [-100|155] ""Nm"" ABS
 SG_ ThrottlePosition : 24|8@1+ (0.392,0) [0|100] ""%"" CLUSTER
 SG_ EngineCoolantTemp : 32|8@1+ (1,-40) [-40|215] ""degC"" CLUSTER,HVAC
 SG_ FuelRailPressure : 40|16@1+ (0.1,0) [0|3000] ""bar"" CLUSTER

BO_ 390 VehicleDynamics: 8 ABS
 SG_ VehicleSpeed : 0|16@1+ (0.01,0) [0|250] ""km/h"" ECM,CLUSTER,BCM
 SG_ BrakePressure : 16|8@1+ (1,0) [0|250] ""bar"" ECM
 SG_ WheelSpeedFL : 24|10@1+ (0.25,0) [0|250] ""km/h"" CLUSTER
 SG_ WheelSpeedFR : 34|10@1+ (0.25,0) [0|250] ""km/h"" CLUSTER
 SG_ AbsActive : 44|1@1+ (1,0) [0|1] """" CLUSTER

BO_ 520 BodyStatus: 6 BCM
 SG_ SteeringAngle : 0|16@1- (0.1,0) [-720|720] ""deg"" ABS,ECM
 SG_ HeadlightsOn : 16|1@1+ (1,0) [0|1] """" CLUSTER
 SG_ TurnSignalLeft : 17|1@1+ (1,0) [0|1] """" CLUSTER
 SG_ TurnSignalRight : 18|1@1+ (1,0) [0|1] """" CLUSTER
 SG_ AmbientAirTemp : 24|8@1+ (0.5,-40) [-40|85] ""degC"" CLUSTER,HVAC
 SG_ BatteryVoltage : 32|8@1+ (0.1,0) [0|25] ""V"" CLUSTER

BO_ 800 ClusterDisplay: 4 CLUSTER
 SG_ OdometerKm : 0|24@1+ (1,0) [0|16777215] ""km"" BCM
 SG_ FuelLevel : 24|8@1+ (0.392,0) [0|100] ""%"" ECM

VAL_ 390 AbsActive 0 ""Inactive"" 1 ""Active"";
VAL_ 520 HeadlightsOn 0 ""Off"" 1 ""On"";

CM_ BU_ ECM ""Engine Control Module"";
CM_ BU_ BCM ""Body Control Module"";
CM_ BU_ ABS ""Anti-lock Braking System"";
CM_ BU_ CLUSTER ""Instrument Panel Cluster"";
";

        var dbc = DbcParser.Parse(sampleDbc);
        _pipeline.SetActiveDbc(dbc);

        return Ok(new
        {
            success = true,
            name = "Automotive_Powertrain_Body.dbc",
            version = dbc.Version,
            nodeCount = dbc.Nodes.Count,
            messageCount = dbc.Messages.Count
        });
    }

    [HttpDelete("active")]
    public IActionResult ClearActiveDbc()
    {
        _pipeline.SetActiveDbc(null);
        return Ok(new { success = true });
    }
}
