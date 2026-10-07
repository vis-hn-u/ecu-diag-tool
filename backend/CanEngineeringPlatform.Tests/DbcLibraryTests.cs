using System;
using System.IO;
using System.Linq;
using CanEngineeringPlatform.Core.Dbc;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class DbcLibraryTests
{
    private static string GetDbcLibraryPath()
    {
        // Navigate up from bin/Release/net8.0 to find DbcLibrary
        var current = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            var candidate = Path.Combine(current, "DbcLibrary");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        // Direct path fallback
        var direct = @"c:\Users\vishnu\Documents\ChatGPT\diag tool\backend\DbcLibrary";
        if (Directory.Exists(direct)) return direct;

        throw new DirectoryNotFoundException("DbcLibrary directory not found.");
    }

    [Theory]
    [InlineData("bosch_edc16_crde.dbc")]
    [InlineData("bosch_edc17_mhawk_revotorq.dbc")]
    [InlineData("bosch_md1_mg1_bs6.dbc")]
    [InlineData("denso_suzuki_toyota.dbc")]
    [InlineData("magneti_marelli_mjd_ddis.dbc")]
    [InlineData("mahindra_passenger.dbc")]
    [InlineData("tata_motors_passenger.dbc")]
    [InlineData("siemens_continental_sid_pcr.dbc")]
    [InlineData("universal_obd2_live_data.dbc")]
    [InlineData("sae_j1939_commercial.dbc")]
    [InlineData("hyundai_kia_ccan.dbc")]
    [InlineData("vw_skoda_pq25_edc17.dbc")]
    [InlineData("vw_skoda_mqb_india.dbc")]
    [InlineData("toyota_innova_fortuner_pt.dbc")]
    [InlineData("ford_india_cgea_pt.dbc")]
    [InlineData("mg_hector_astor.dbc")]
    public void CustomEcuDbcFiles_ParseSuccessfully_WithValidMessagesAndSignals(string fileName)
    {
        var dir = GetDbcLibraryPath();
        var fullPath = Path.Combine(dir, fileName);
        Assert.True(File.Exists(fullPath), $"File {fileName} does not exist in DbcLibrary");

        string content = File.ReadAllText(fullPath);
        var dbc = DbcParser.Parse(content);

        Assert.NotNull(dbc);
        Assert.NotEmpty(dbc.Messages);
        Assert.True(dbc.Messages.Count >= 1, $"Expected at least 1 message in {fileName}");

        // Verify common live data signals exist
        var allSignals = dbc.Messages.SelectMany(m => m.Signals).Select(s => s.Name).ToList();
        Assert.NotEmpty(allSignals);
    }

    [Fact]
    public void BoschEdc17Dbc_ContainsAuthoritativeRailPressureAndRpm()
    {
        var dir = GetDbcLibraryPath();
        var content = File.ReadAllText(Path.Combine(dir, "bosch_edc17_mhawk_revotorq.dbc"));
        var dbc = DbcParser.Parse(content);

        var msg120 = dbc.Messages.Find(m => m.Id == 288);
        Assert.NotNull(msg120);
        Assert.Contains(msg120.Signals, s => s.Name == "EngineRPM");

        var msg318 = dbc.Messages.Find(m => m.Id == 792);
        Assert.NotNull(msg318);
        Assert.Contains(msg318.Signals, s => s.Name == "RailPressureActual");
    }

    [Fact]
    public void BoschMd1Bs6Dbc_ContainsScrAdBlueAndDpfSignals()
    {
        var dir = GetDbcLibraryPath();
        var content = File.ReadAllText(Path.Combine(dir, "bosch_md1_mg1_bs6.dbc"));
        var dbc = DbcParser.Parse(content);

        var msg400 = dbc.Messages.Find(m => m.Id == 1024);
        Assert.NotNull(msg400);
        Assert.Contains(msg400.Signals, s => s.Name == "DefAdBlueTankLevel");
        Assert.Contains(msg400.Signals, s => s.Name == "DpfDifferentialPressure");
    }

    [Fact]
    public void MahindraPassengerDbc_ContainsScorpioNDoorStatusAnd4wd()
    {
        var dir = GetDbcLibraryPath();
        var content = File.ReadAllText(Path.Combine(dir, "mahindra_passenger.dbc"));
        var dbc = DbcParser.Parse(content);

        var bcmMsg = dbc.Messages.Find(m => m.Id == 2029); // 0x7ED
        Assert.NotNull(bcmMsg);
        Assert.Contains(bcmMsg.Signals, s => s.Name == "FrontRightDoorOpen");

        var chassisMsg = dbc.Messages.Find(m => m.Id == 544); // 0x220
        Assert.NotNull(chassisMsg);
        Assert.Contains(chassisMsg.Signals, s => s.Name == "FourWheelDriveMode");
    }

    [Fact]
    public void CanPipeline_LoadEdc17FromLibrary_DecodesRailPressureLiveFrames()
    {
        var pipeline = new CanEngineeringPlatform.Core.Pipeline.CanPipeline(
            new CanEngineeringPlatform.Core.Topology.TopologyEngine(),
            new CanEngineeringPlatform.Core.Logging.CanLoggerService());

        var dir = GetDbcLibraryPath();
        var edc17File = Path.Combine(dir, "bosch_edc17_mhawk_revotorq.dbc");
        var content = File.ReadAllText(edc17File);
        var dbc = DbcParser.Parse(content);

        pipeline.SetActiveDbc(dbc);

        var activeDbc = pipeline.GetActiveDbc();
        Assert.NotNull(activeDbc);
        Assert.Equal(dbc.Version, activeDbc.Version);
        Assert.Contains(activeDbc.Messages, m => m.Id == 288); // 0x120
        Assert.Contains(activeDbc.Messages, m => m.Id == 792); // 0x318 (RailPressure)
    }

    [Fact]
    public void CanPipeline_LoadMd1Bs6FromLibrary_DecodesAdBlueAndScrLiveFrames()
    {
        var pipeline = new CanEngineeringPlatform.Core.Pipeline.CanPipeline(
            new CanEngineeringPlatform.Core.Topology.TopologyEngine(),
            new CanEngineeringPlatform.Core.Logging.CanLoggerService());

        var dir = GetDbcLibraryPath();
        var md1File = Path.Combine(dir, "bosch_md1_mg1_bs6.dbc");
        var content = File.ReadAllText(md1File);
        var dbc = DbcParser.Parse(content);

        pipeline.SetActiveDbc(dbc);

        var activeDbc = pipeline.GetActiveDbc();
        Assert.NotNull(activeDbc);
        Assert.Contains(activeDbc.Messages, m => m.Id == 1024); // 0x400 (SCR / AdBlue)
        Assert.Contains(activeDbc.Messages, m => m.Id == 256);  // 0x100 (RPM)
    }
}
