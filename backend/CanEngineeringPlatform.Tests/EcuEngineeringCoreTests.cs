using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.EcuEngineering.Abstractions;
using CanEngineeringPlatform.Core.EcuEngineering.Compatibility;
using CanEngineeringPlatform.Core.EcuEngineering.Core;
using CanEngineeringPlatform.Core.EcuEngineering.Identification;
using CanEngineeringPlatform.Core.EcuEngineering.Protocols;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Topology;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class EcuEngineeringCoreTests
{
    private (DiagnosticSessionService session, CanPipeline pipeline) CreateTestDiagnosticSession()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        var pipeline = new CanPipeline(topology, logger);
        var isoTp = new IsoTpRouter();
        var sim = new EcuSimulator(pipeline, isoTp);
        var session = new DiagnosticSessionService(pipeline, isoTp, sim);
        return (session, pipeline);
    }

    #region Confidence Levels & Evidence Source Tests

    [Fact]
    public void FieldWithConfidence_PropertiesSetCorrectly()
    {
        var evidence = EvidenceSource.Create(
            "UdsDid",
            "0xF190",
            "UDS DID 0xF190 read returned 17 ASCII bytes",
            "3146413650384346354835313233343536");

        var field = new FieldWithConfidence<string>("1FA6P8CF5H5123456", IdentificationConfidence.Confirmed, evidence);

        Assert.Equal("1FA6P8CF5H5123456", field.Value);
        Assert.Equal(IdentificationConfidence.Confirmed, field.Confidence);
        Assert.NotEmpty(field.Citations);
        Assert.Equal("0xF190", field.Citations[0].ReferenceCode);
        Assert.Equal("UdsDid", field.Citations[0].SourceType);
        Assert.Equal("3146413650384346354835313233343536", field.Citations[0].RawBytesHex);
    }

    [Fact]
    public void FieldWithConfidence_DefaultUnknown()
    {
        var field = new FieldWithConfidence<string>();

        Assert.Null(field.Value);
        Assert.Equal(IdentificationConfidence.Unknown, field.Confidence);
        Assert.Empty(field.Citations);
    }

    [Fact]
    public void EvidenceSource_FormatsPropertiesCorrectly()
    {
        var evidence = EvidenceSource.Create(
            "ObdMode9",
            "PID 0x02",
            "VIN from OBD Mode 09 PID 02",
            "01020304");

        Assert.Equal("ObdMode9", evidence.SourceType);
        Assert.Equal("PID 0x02", evidence.ReferenceCode);
        Assert.Equal("01020304", evidence.RawBytesHex);
        Assert.Equal("VIN from OBD Mode 09 PID 02", evidence.Description);
        Assert.True(evidence.TimestampUtc <= DateTime.UtcNow);
    }

    #endregion

    #region Compatibility Catalog Tests

    [Fact]
    public void CompatibilityManager_DefaultProfilesSeeded()
    {
        var manager = new EcuCompatibilityManager();
        var profiles = manager.GetAllProfiles();

        Assert.NotNull(profiles);
        Assert.True(profiles.Count >= 7, $"Expected at least 7 seeded profiles, got {profiles.Count}");

        // Verify key families exist
        var med17 = profiles.FirstOrDefault(p => p.FamilyId == "BOSCH_MED17_5_5");
        Assert.NotNull(med17);
        Assert.Equal("Bosch", med17.Manufacturer);
        Assert.Equal(McuArchitecture.InfineonTriCore_TC17xx, med17.Mcu);
        Assert.Contains("UDS", med17.PrimaryDiagnosticProtocol);

        var simos = profiles.FirstOrDefault(p => p.FamilyId == "SIEMENS_SIMOS18_1");
        Assert.NotNull(simos);
        Assert.Contains("Continental", simos.Manufacturer);
        Assert.Equal(McuArchitecture.InfineonTriCore_AURIX_TC2xx, simos.Mcu);

        var edc17 = profiles.FirstOrDefault(p => p.FamilyId == "BOSCH_EDC17C64");
        Assert.NotNull(edc17);
        Assert.Equal(McuArchitecture.InfineonTriCore_TC17xx, edc17.Mcu);

        var dcm37 = profiles.FirstOrDefault(p => p.FamilyId == "DELPHI_DCM3_7");
        Assert.NotNull(dcm37);
        Assert.Equal("Delphi", dcm37.Manufacturer);
        Assert.Equal(McuArchitecture.Renesas_SuperH_SH705x, dcm37.Mcu);

        var denso = profiles.FirstOrDefault(p => p.FamilyId == "DENSO_SH7058");
        Assert.NotNull(denso);
        Assert.Equal("Denso", denso.Manufacturer);
        Assert.Equal(McuArchitecture.Renesas_SuperH_SH705x, denso.Mcu);
    }

    [Fact]
    public void CompatibilityManager_GetProfileById_CaseInsensitive()
    {
        var manager = new EcuCompatibilityManager();
        var profile = manager.GetProfileById("bosch_med17_5_5");

        Assert.NotNull(profile);
        Assert.Equal("BOSCH_MED17_5_5", profile.FamilyId);
        Assert.NotEmpty(profile.MemoryRegions);
        Assert.False(string.IsNullOrWhiteSpace(profile.ChecksumAlgorithm));
        Assert.NotEmpty(profile.OemNormalLimits);
    }

    [Fact]
    public void CompatibilityManager_GetProfileById_NonExistent_ReturnsNull()
    {
        var manager = new EcuCompatibilityManager();
        var profile = manager.GetProfileById("NON-EXISTENT-ECU-999");

        Assert.Null(profile);
    }

    [Fact]
    public void CompatibilityManager_MatchProfile_ByHardwareNumber()
    {
        var manager = new EcuCompatibilityManager();
        var matched = manager.MatchProfile("06J906026", null);

        Assert.NotNull(matched);
        Assert.Equal("BOSCH_MED17_5_5", matched.FamilyId);
    }

    [Fact]
    public void CompatibilityManager_MatchProfile_BySoftwareNumber()
    {
        var manager = new EcuCompatibilityManager();
        var matched = manager.MatchProfile(null, "5G0906259A");

        Assert.NotNull(matched);
        Assert.Equal("SIEMENS_SIMOS18_1", matched.FamilyId);
    }

    [Fact]
    public void CompatibilityManager_RegisterProfile_CustomEcuAdded()
    {
        var manager = new EcuCompatibilityManager();
        var custom = new EcuFamilyProfile
        {
            FamilyId = "CUSTOM_TEST_01",
            Name = "Custom Test ECU",
            Manufacturer = "TestMfg",
            Mcu = McuArchitecture.InfineonTriCore_AURIX_TC3xx,
            McuModel = "TC397",
            TypicalPlatforms = new() { "Generic Prototype" },
            PrimaryDiagnosticProtocol = "UDS on CAN"
        };

        manager.RegisterProfile(custom);

        var retrieved = manager.GetProfileById("CUSTOM_TEST_01");
        Assert.NotNull(retrieved);
        Assert.Equal("Custom Test ECU", retrieved.Name);
        Assert.Equal(McuArchitecture.InfineonTriCore_AURIX_TC3xx, retrieved.Mcu);
    }

    #endregion

    #region Protocol Discovery Tests

    [Fact]
    public async Task ProtocolDiscovery_ProbingSimulatorEcm_DetectsUdsOnIsoTp()
    {
        var (session, pipeline) = CreateTestDiagnosticSession();
        using (pipeline)
        {
            var discovery = new ProtocolDiscoveryService(session);
            var matrix = await discovery.DiscoverProtocolAsync(0x7E0, 0x7E8, "ECM");

            Assert.NotNull(matrix);
            Assert.Equal("ECM", matrix.EcuName);
            Assert.Equal(0x7E0u, matrix.RequestCanId);
            Assert.Equal(0x7E8u, matrix.ResponseCanId);
            Assert.Equal(DiagnosticProtocolType.UdsOnIsoTp, matrix.PrimaryProtocol);
            Assert.Equal(AddressingSchemeType.Standard11BitUnicast, matrix.AddressingScheme);

            // Verify standard services are mapped
            Assert.NotEmpty(matrix.Services);
            Assert.True(matrix.Services.ContainsKey(0x3E));
            Assert.True(matrix.Services[0x3E].IsSupported);

            Assert.True(matrix.Services.ContainsKey(0x22));
            Assert.True(matrix.Services[0x22].IsSupported);

            // Verify discovered matrices query
            var allMatrices = discovery.GetDiscoveredMatrices();
            Assert.Single(allMatrices);
            Assert.Equal("ECM", allMatrices[0].EcuName);

            var ecmMatrix = discovery.GetMatrixForEcu("ECM");
            Assert.NotNull(ecmMatrix);
            Assert.Equal(matrix.DiscoveryTimestampUtc, ecmMatrix.DiscoveryTimestampUtc);
        }
    }

    [Fact]
    public async Task ProtocolDiscovery_NonExistentEcu_GracefulDegradation()
    {
        var (session, pipeline) = CreateTestDiagnosticSession();
        using (pipeline)
        {
            var discovery = new ProtocolDiscoveryService(session);
            // Request to non-existent ECU 0x7E7 which has no responder in simulator
            var matrix = await discovery.DiscoverProtocolAsync(0x7E7, 0x7EF, "GHOST_ECU");

            Assert.NotNull(matrix);
            Assert.Equal("GHOST_ECU", matrix.EcuName);
            Assert.Equal(DiagnosticProtocolType.Unknown, matrix.PrimaryProtocol);
        }
    }

    #endregion

    #region ECU Identification Tests

    [Fact]
    public async Task EcuIdentification_SimulatorEcm_ExtractsConfirmedVinAndEvidence()
    {
        var (session, pipeline) = CreateTestDiagnosticSession();
        using (pipeline)
        {
            var compatManager = new EcuCompatibilityManager();
            var idService = new EcuIdentificationService(session, compatManager);

            var profile = await idService.IdentifyEcuAsync(0x7E0, 0x7E8, "ECM");

            Assert.NotNull(profile);
            Assert.Equal("ECM", profile.EcuName);

            // In simulator, DID 0xF190 returns "1FA6P8CF5H5123456"
            Assert.Equal(IdentificationConfidence.Confirmed, profile.Vin.Confidence);
            Assert.Equal("1FA6P8CF5H5123456", profile.Vin.Value);
            Assert.NotEmpty(profile.Vin.Citations);
            Assert.Equal("0xF190", profile.Vin.Citations[0].ReferenceCode);
            Assert.Equal("UdsDid", profile.Vin.Citations[0].SourceType);
            Assert.False(string.IsNullOrEmpty(profile.Vin.Citations[0].RawBytesHex));

            // Verify overall confidence reflects the confirmed VIN
            Assert.True(profile.OverallConfidence == IdentificationConfidence.Confirmed ||
                        profile.OverallConfidence == IdentificationConfidence.Detected);

            // Verify cache retrieval
            var allEcus = idService.GetAllIdentifiedEcus();
            Assert.Single(allEcus);
            Assert.Equal("ECM", allEcus[0].EcuName);

            var ecmProfile = idService.GetProfile("ECM");
            Assert.NotNull(ecmProfile);
            Assert.Equal("1FA6P8CF5H5123456", ecmProfile.Vin.Value);
        }
    }

    [Fact]
    public async Task EcuIdentification_NonExistentEcu_ReturnsUnknownProfileWithoutCrashing()
    {
        var (session, pipeline) = CreateTestDiagnosticSession();
        using (pipeline)
        {
            var compatManager = new EcuCompatibilityManager();
            var idService = new EcuIdentificationService(session, compatManager);

            // Non-responsive ID 0x7E7
            var profile = await idService.IdentifyEcuAsync(0x7E7, 0x7EF, "UNKNOWN_MODULE");

            Assert.NotNull(profile);
            Assert.Equal("UNKNOWN_MODULE", profile.EcuName);
            Assert.Equal(IdentificationConfidence.Unknown, profile.Vin.Confidence);
            Assert.Equal(IdentificationConfidence.Unknown, profile.OverallConfidence);
            Assert.Equal(IdentificationConfidence.Unknown, profile.EcuFamily.Confidence);
        }
    }

    #endregion

    #region Unified ECU Engineering Provider Tests

    [Fact]
    public void EcuEngineeringProvider_PropertiesAndNullValidation()
    {
        var (session, pipeline) = CreateTestDiagnosticSession();
        using (pipeline)
        {
            var compat = new EcuCompatibilityManager();
            var discovery = new ProtocolDiscoveryService(session);
            var idService = new EcuIdentificationService(session, compat);

            var provider = new EcuEngineeringProvider(idService, discovery, compat);

            Assert.NotNull(provider.Identification);
            Assert.NotNull(provider.ProtocolDiscovery);
            Assert.NotNull(provider.Compatibility);

            Assert.Same(idService, provider.Identification);
            Assert.Same(discovery, provider.ProtocolDiscovery);
            Assert.Same(compat, provider.Compatibility);

            // Null validations
            Assert.Throws<ArgumentNullException>(() => new EcuEngineeringProvider(null!, discovery, compat));
            Assert.Throws<ArgumentNullException>(() => new EcuEngineeringProvider(idService, null!, compat));
            Assert.Throws<ArgumentNullException>(() => new EcuEngineeringProvider(idService, discovery, null!));
        }
    }

    #endregion
}
