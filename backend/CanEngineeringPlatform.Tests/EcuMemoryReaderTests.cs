using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.EcuEngineering.Analysis;
using CanEngineeringPlatform.Core.EcuEngineering.Compatibility;
using CanEngineeringPlatform.Core.EcuEngineering.Core;
using CanEngineeringPlatform.Core.EcuEngineering.Identification;
using CanEngineeringPlatform.Core.EcuEngineering.Images;
using CanEngineeringPlatform.Core.EcuEngineering.Memory;
using CanEngineeringPlatform.Core.EcuEngineering.Protocols;
using CanEngineeringPlatform.Core.EcuEngineering.Reading;
using CanEngineeringPlatform.Core.FlashAnalysis;
using CanEngineeringPlatform.Core.LiveEcuData;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Topology;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class EcuMemoryReaderTests
{
    private (DiagnosticSessionService session, CanPipeline pipeline, EcuSimulator sim) CreateTestEnvironment()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        var pipeline = new CanPipeline(topology, logger);
        var isoTp = new IsoTpRouter();
        var sim = new EcuSimulator(pipeline, isoTp);
        var session = new DiagnosticSessionService(pipeline, isoTp, sim);
        return (session, pipeline, sim);
    }

    #region Memory Region & Map Tests

    [Fact]
    public void MemoryRegion_BoundsAndLengthCalculation()
    {
        var region = new MemoryRegion
        {
            RegionName = "Calibration Sector",
            StartAddress = 0x001C0000,
            EndAddress = 0x001FFFFF,
            Type = MemoryType.Calibration,
            ReadCapability = ReadPermission.ReadSupported
        };

        Assert.Equal(262144, region.Length); // 256 KB
        Assert.True(region.ContainsAddress(0x001C0000));
        Assert.True(region.ContainsAddress(0x001E0000));
        Assert.True(region.ContainsAddress(0x001FFFFF));
        Assert.False(region.ContainsAddress(0x001BFFFF));
        Assert.False(region.ContainsAddress(0x00200000));
    }

    [Fact]
    public void MemoryMap_RecalculateGaps_DetectsDiscontinuousAddresses()
    {
        var map = new EcuMemoryMap();
        map.Regions.Add(new MemoryRegion
        {
            RegionName = "Flash Block A",
            StartAddress = 0x00000000,
            EndAddress = 0x0000FFFF
        });
        map.Regions.Add(new MemoryRegion
        {
            RegionName = "Flash Block B",
            StartAddress = 0x00020000,
            EndAddress = 0x0003FFFF
        });

        map.RecalculateGaps();

        Assert.Single(map.Gaps);
        var gap = map.Gaps[0];
        Assert.Equal(0x00010000, gap.StartAddress);
        Assert.Equal(0x0001FFFF, gap.EndAddress);
        Assert.Equal(65536, gap.LengthBytes);
    }

    [Fact]
    public async Task MemoryDiscovery_CatalogMatchedEcu_BuildsCatalogDerivedRegions()
    {
        var compat = new EcuCompatibilityManager();
        var discovery = new MemoryDiscoveryService(compat);

        var idProfile = new EcuIdentificationProfile
        {
            EcuName = "ECM",
            HardwareNumber = FieldWithConfidence<string>.Confirmed(
                "06J906026",
                EvidenceSource.Create("UdsDid", "0xF191", "HW number"))
        };

        var memoryMap = await discovery.DiscoverMemoryMapAsync(idProfile);

        Assert.NotNull(memoryMap);
        Assert.Equal("BOSCH_MED17_5_5", memoryMap.EcuFamily);
        Assert.NotEmpty(memoryMap.Regions);

        // Bosch MED17.5.5 profile should have Application Software and Calibration Maps
        var appRegion = memoryMap.Regions.FirstOrDefault(r => r.RegionName.Contains("Application Software"));
        Assert.NotNull(appRegion);
        Assert.Equal(MemoryRegionProvenance.CatalogDerived, appRegion.Provenance);
        Assert.NotEmpty(appRegion.Citations);
        Assert.True(appRegion.Length > 0);

        // Evaluate read capabilities
        var caps = await discovery.EvaluateReadCapabilitiesAsync(memoryMap);
        Assert.NotNull(caps);
        Assert.NotEmpty(caps.RegionCapabilities);
        Assert.True(caps.SupportsReadMemoryByAddress0x23);
    }

    [Fact]
    public async Task MemoryDiscovery_UnknownEcu_CreatesInferredBaselineWithoutFailing()
    {
        var compat = new EcuCompatibilityManager();
        var discovery = new MemoryDiscoveryService(compat);

        var idProfile = new EcuIdentificationProfile
        {
            EcuName = "UNKNOWN_MODULE"
        };

        var memoryMap = await discovery.DiscoverMemoryMapAsync(idProfile);

        Assert.NotNull(memoryMap);
        Assert.Equal("GENERIC_AUTOMOTIVE_ECU", memoryMap.EcuFamily);
        Assert.True(memoryMap.Regions.Count >= 3);

        // Inferred regions must be tagged as Inferred provenance
        foreach (var r in memoryMap.Regions)
        {
            Assert.Equal(MemoryRegionProvenance.Inferred, r.Provenance);
            Assert.Equal(IdentificationConfidence.Inferred, r.Confidence);
        }
    }

    #endregion

    #region Read Session State Machine Tests

    [Fact]
    public void ReadSessionState_ValidTransitionsSucceed()
    {
        var status = new ReadSessionStatus();
        Assert.Equal(ReadSessionState.Disconnected, status.State);

        status.TransitionTo(ReadSessionState.Connected, "Connected");
        Assert.Equal(ReadSessionState.Connected, status.State);

        status.TransitionTo(ReadSessionState.Identified, "Identified");
        Assert.Equal(ReadSessionState.Identified, status.State);

        status.TransitionTo(ReadSessionState.ProtocolConfirmed, "Protocol Confirmed");
        Assert.Equal(ReadSessionState.ProtocolConfirmed, status.State);

        status.TransitionTo(ReadSessionState.DiagnosticSessionActive, "Session Active");
        Assert.Equal(ReadSessionState.DiagnosticSessionActive, status.State);

        status.TransitionTo(ReadSessionState.ReadAuthorized, "Authorized");
        Assert.Equal(ReadSessionState.ReadAuthorized, status.State);

        status.TransitionTo(ReadSessionState.Reading, "Reading");
        Assert.Equal(ReadSessionState.Reading, status.State);

        status.TransitionTo(ReadSessionState.Verifying, "Verifying");
        Assert.Equal(ReadSessionState.Verifying, status.State);

        status.TransitionTo(ReadSessionState.Complete, "Complete");
        Assert.Equal(ReadSessionState.Complete, status.State);
    }

    [Fact]
    public void ReadSessionState_InvalidTransition_ThrowsInvalidOperationException()
    {
        var status = new ReadSessionStatus();
        Assert.Equal(ReadSessionState.Disconnected, status.State);

        // Attempting to jump directly from Disconnected to Reading must fail
        Assert.Throws<InvalidOperationException>(() =>
            status.TransitionTo(ReadSessionState.Reading, "Illegal Jump"));
    }

    [Fact]
    public void ReadSessionState_FailureTransitionAllowedFromAnyState()
    {
        var status = new ReadSessionStatus();
        status.TransitionTo(ReadSessionState.Connected, "Connected");
        status.TransitionTo(ReadSessionState.Failed, "Transport disconnected");

        Assert.Equal(ReadSessionState.Failed, status.State);
    }

    #endregion

    #region Safe Read Controller & Chunking Tests

    [Fact]
    public async Task EcuMemoryReader_SafeReadSimulation_ExtractsAndHashesImage()
    {
        var (session, pipeline, sim) = CreateTestEnvironment();
        using (pipeline)
        {
            var imageStore = new EcuMemoryImageStore();
            var reader = new EcuMemoryReader(session, imageStore);

            var idProfile = new EcuIdentificationProfile
            {
                EcuName = "ECM",
                RequestCanId = 0x7E0,
                ResponseCanId = 0x7E8
            };

            var memoryMap = new EcuMemoryMap { EcuName = "ECM" };
            memoryMap.Regions.Add(new MemoryRegion
            {
                RegionName = "Calibration Sector",
                StartAddress = 0x001C0000,
                EndAddress = 0x001C03FF, // 1024 bytes
                Type = MemoryType.Calibration,
                ReadCapability = ReadPermission.ReadSupported,
                MaxReadLengthBytes = 256
            });

            var options = new SafeReadOptions
            {
                ChunkSize = 256,
                TimeoutMs = 1000,
                InterChunkDelayMs = 0,
                Environment = ExecutionEnvironment.Simulation,
                RequireUserConfirmation = true,
                UserConfirmed = true
            };

            // Read 512 bytes (2 chunks of 256 bytes)
            var image = await reader.ReadMemoryAsync(
                idProfile,
                memoryMap,
                0x001C0000,
                512,
                "Calibration Sector",
                options);

            Assert.NotNull(image);
            Assert.Equal(512, image.Length);
            Assert.Equal(512, image.RawBytes.Length);
            Assert.False(string.IsNullOrWhiteSpace(image.Sha256Hash));
            Assert.Equal(ImageClassification.OriginalRead, image.Classification);

            // Verify stored in image store
            var retrieved = imageStore.GetImageById(image.ImageId);
            Assert.NotNull(retrieved);
            Assert.Equal(image.Sha256Hash, retrieved.Sha256Hash);

            // Verify manifest generation
            var manifest = imageStore.GetManifest(image.ImageId);
            Assert.NotNull(manifest);
            Assert.Equal("ECM", manifest.Ecu);
            Assert.Equal(image.Sha256Hash, manifest.Sha256);
            Assert.Equal(512, manifest.Length);
        }
    }

    [Fact]
    public async Task EcuMemoryReader_UnauthorizedMemorySector_RefusesToRead()
    {
        var (session, pipeline, sim) = CreateTestEnvironment();
        using (pipeline)
        {
            var imageStore = new EcuMemoryImageStore();
            var reader = new EcuMemoryReader(session, imageStore);

            var idProfile = new EcuIdentificationProfile { EcuName = "ECM" };
            var memoryMap = new EcuMemoryMap { EcuName = "ECM" };
            memoryMap.Regions.Add(new MemoryRegion
            {
                RegionName = "Protected Boot Sector",
                StartAddress = 0x00000000,
                EndAddress = 0x0001FFFF,
                Type = MemoryType.Bootloader,
                ReadCapability = ReadPermission.RequiresSecurityAccess,
                RequiredSecurityLevel = 0x01
            });

            var options = new SafeReadOptions
            {
                Environment = ExecutionEnvironment.Simulation,
                UserConfirmed = true
            };

            // Reading a security-locked sector without authorization must throw UnauthorizedAccessException
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                reader.ReadMemoryAsync(idProfile, memoryMap, 0x00000000, 256, "Protected Boot Sector", options));
        }
    }

    [Fact]
    public async Task EcuMemoryReader_UnconfirmedRead_ThrowsUnauthorizedAccessException()
    {
        var (session, pipeline, sim) = CreateTestEnvironment();
        using (pipeline)
        {
            var imageStore = new EcuMemoryImageStore();
            var reader = new EcuMemoryReader(session, imageStore);

            var idProfile = new EcuIdentificationProfile { EcuName = "ECM" };
            var memoryMap = new EcuMemoryMap { EcuName = "ECM" };
            memoryMap.Regions.Add(new MemoryRegion
            {
                RegionName = "Open Sector",
                StartAddress = 0x00020000,
                EndAddress = 0x0003FFFF,
                ReadCapability = ReadPermission.ReadSupported
            });

            var options = new SafeReadOptions
            {
                Environment = ExecutionEnvironment.Simulation,
                RequireUserConfirmation = true,
                UserConfirmed = false // Operator has not confirmed
            };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                reader.ReadMemoryAsync(idProfile, memoryMap, 0x00020000, 256, "Open Sector", options));
        }
    }

    [Fact]
    public async Task EcuMemoryReader_OutOfBoundAddresses_ThrowsAppropriateExceptions()
    {
        var (session, pipeline, sim) = CreateTestEnvironment();
        using (pipeline)
        {
            var imageStore = new EcuMemoryImageStore();
            var reader = new EcuMemoryReader(session, imageStore);

            var idProfile = new EcuIdentificationProfile { EcuName = "ECM" };
            var memoryMap = new EcuMemoryMap { EcuName = "ECM" };
            memoryMap.Regions.Add(new MemoryRegion
            {
                RegionName = "Sector A",
                StartAddress = 0x00010000,
                EndAddress = 0x000100FF, // 256 bytes
                ReadCapability = ReadPermission.ReadSupported
            });

            var options = new SafeReadOptions { Environment = ExecutionEnvironment.Simulation, UserConfirmed = true };

            // Negative address
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                reader.ReadMemoryAsync(idProfile, memoryMap, -1, 100, "Sector A", options));

            // Zero length
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                reader.ReadMemoryAsync(idProfile, memoryMap, 0x00010000, 0, "Sector A", options));

            // Address outside any region
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                reader.ReadMemoryAsync(idProfile, memoryMap, 0x00080000, 100, "Sector A", options));

            // Read overflowing region boundary
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                reader.ReadMemoryAsync(idProfile, memoryMap, 0x00010050, 300, "Sector A", options));
        }
    }

    #endregion

    #region Image Analysis & Forensics Tests

    [Fact]
    public void ImageAnalysisEngine_CalculatesEntropyAndDetectsStrings()
    {
        // Construct synthetic 8KB binary
        byte[] testImage = new byte[8192];
        for (int i = 0; i < 4096; i++) testImage[i] = 0x00; // Zero block -> 0 entropy

        // Inject ASCII text
        string banner = "BOSCH_MED17.5_CALIBRATION_TEST_2026";
        byte[] bannerBytes = Encoding.ASCII.GetBytes(banner);
        Array.Copy(bannerBytes, 0, testImage, 1000, bannerBytes.Length);

        // High entropy pseudo-random block for upper 4KB
        var rng = new Random(1234);
        for (int i = 4096; i < 8192; i++) testImage[i] = (byte)rng.Next(256);

        var analysis = ImageAnalysisEngine.Analyze(testImage, "BOSCH_MED17");

        Assert.NotNull(analysis);
        Assert.Equal(8192, analysis.ImageSizeBytes);
        Assert.True(analysis.ZeroBytePercent >= 45.0);

        // Verify ASCII string detected
        var foundString = analysis.IdentifiedAsciiStrings.FirstOrDefault(s => s.Text.Contains("BOSCH_MED17.5"));
        Assert.NotNull(foundString);
        Assert.Equal(1000, foundString.Offset);

        // Verify Checksum candidate detected at trailer
        Assert.NotEmpty(analysis.ChecksumCandidates);
    }

    [Fact]
    public void ImageAnalysisEngine_ShannonEntropy_AllZerosIsZero()
    {
        byte[] zeros = new byte[1024];
        double entropy = ImageAnalysisEngine.ComputeShannonEntropy(zeros);
        Assert.Equal(0.0, entropy);
    }

    #endregion

    #region Flash Analyzer Integration Tests

    [Fact]
    public void FlashAnalyzer_ImportBinary_SuccessfullyConnectsMemoryImage()
    {
        var (session, pipeline, sim) = CreateTestEnvironment();
        using (pipeline)
        {
            var liveData = new LiveEcuDataService(pipeline, session);
            var flashAnalyzer = new FlashAnalyzerService(liveData);

            // Synthetic 16KB memory read image
        byte[] readBytes = new byte[16384];
        byte[] sig = Encoding.ASCII.GetBytes("BOSCH MED17.5 CALIBRATION IMAGE");
        Array.Copy(sig, 0, readBytes, 0x100, sig.Length);

        var binaryInfo = flashAnalyzer.ImportBinary("ECM_Calibration_Sector.bin", readBytes);

        Assert.NotNull(binaryInfo);
        Assert.Equal(16384, binaryInfo.FileSizeBytes);
        Assert.False(string.IsNullOrWhiteSpace(binaryInfo.Sha256Hash));
        Assert.NotNull(flashAnalyzer.GetActiveBinary());
        Assert.Equal(binaryInfo.Sha256Hash, flashAnalyzer.GetActiveBinary()?.Sha256Hash);
        }
    }

    #endregion
}
