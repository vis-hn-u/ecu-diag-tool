using System;
using System.Linq;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.FlashAnalysis;
using CanEngineeringPlatform.Core.LiveEcuData;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Topology;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class FlashAnalyzerTests
{
    [Fact]
    public void FlashAnalyzerService_ImportBinary_CalculatesHashesAndExtractsObjects()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var isoTp = new IsoTpRouter();
        var ecuSim = new EcuSimulator(pipeline, isoTp);
        var diagSession = new DiagnosticSessionService(pipeline, isoTp, ecuSim);
        using var liveService = new LiveEcuDataService(pipeline, diagSession);

        var flashService = new FlashAnalyzerService(liveService);
        var activeBinary = flashService.GetActiveBinary();

        Assert.NotNull(activeBinary);
        Assert.Equal(2097152, activeBinary.FileSizeBytes); // 2MB
        Assert.NotEmpty(activeBinary.Sha256Hash);
        Assert.NotEmpty(activeBinary.Md5Hash);
        Assert.Equal("Bosch MED17.5 (TriCore TC1766)", activeBinary.DetectedEcuFamily);

        // Verify read-only calibration objects
        Assert.NotEmpty(activeBinary.Objects);
        var driverDemand = activeBinary.Objects.FirstOrDefault(o => o.Id == "obj_driver_demand_torque");
        Assert.NotNull(driverDemand);
        Assert.Equal(ObjectCategory.Torque, driverDemand.Category);
        Assert.Equal("2D Map", driverDemand.ObjectType);
        Assert.Equal(ObjectProvenance.Confirmed, driverDemand.Provenance);
    }

    [Fact]
    public void FlashAnalyzerService_CompareBinaries_DetectsChangedBytes()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var isoTp = new IsoTpRouter();
        var ecuSim = new EcuSimulator(pipeline, isoTp);
        var diagSession = new DiagnosticSessionService(pipeline, isoTp, ecuSim);
        using var liveService = new LiveEcuDataService(pipeline, diagSession);

        var flashService = new FlashAnalyzerService(liveService);

        byte[] orig = new byte[1024];
        byte[] mod = new byte[1024];
        Array.Fill(orig, (byte)0xAA);
        Array.Fill(mod, (byte)0xAA);

        // Modify 16 bytes at offset 0x100
        for (int i = 0x100; i < 0x110; i++)
        {
            mod[i] = 0xFF;
        }

        var diff = flashService.CompareBinaries(orig, mod, "Orig.bin", "Mod.bin");

        Assert.Equal(16, diff.TotalDifferentBytes);
        Assert.True(diff.DifferencePercentage > 0.0);
        Assert.NotEmpty(diff.Chunks);
        Assert.Equal(0x100, diff.Chunks[0].Offset);
    }

    [Fact]
    public void FlashAnalyzerService_ChecksumValidation_ComputesValidBlocks()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var isoTp = new IsoTpRouter();
        var ecuSim = new EcuSimulator(pipeline, isoTp);
        var diagSession = new DiagnosticSessionService(pipeline, isoTp, ecuSim);
        using var liveService = new LiveEcuDataService(pipeline, diagSession);

        var flashService = new FlashAnalyzerService(liveService);
        var checksums = flashService.ValidateChecksums(new byte[1024 * 1024]);

        Assert.NotEmpty(checksums);
        Assert.All(checksums, c => Assert.True(c.IsValid));
    }
}
