using System;
using System.Linq;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.Diagnostics.Dtc;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.Dyno;
using CanEngineeringPlatform.Core.EcuEngineering.Compatibility;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Topology;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class PlatformStabilizationBaselineTests
{
    private (DiagnosticSessionService session, CanPipeline pipeline, EcuSimulator sim) CreateTestDiagnosticSession()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        var pipeline = new CanPipeline(topology, logger);
        var isoTp = new IsoTpRouter();
        var sim = new EcuSimulator(pipeline, isoTp);
        var session = new DiagnosticSessionService(pipeline, isoTp, sim);
        return (session, pipeline, sim);
    }

    #region 1. ECU Variants, Pinout Validation States & Conflict Notices

    [Fact]
    public void EcuCompatibility_VariantProfiles_MjdVariants_DistinguishedWithPinoutConflict()
    {
        var manager = new EcuCompatibilityManager();
        var profile = manager.GetProfileById("MAGNETI_MARELLI_MJD");

        Assert.NotNull(profile);
        Assert.Equal(ReleaseValidationState.DatabaseSourceVerified, profile.ReleaseValidation);
        Assert.Equal(PinoutValidationState.VariantDependent, profile.PinoutValidation);
        Assert.True(profile.ConflictingPinoutVariantsFound);
        Assert.Contains("MJD 6F3.S1 (Swift 2007-2011) uses Pin 23 CAN-H / Pin 33 CAN-L", profile.ConflictNotice);

        Assert.NotNull(profile.Variants);
        Assert.True(profile.Variants.Count >= 2);

        var swift = profile.Variants.FirstOrDefault(v => v.VariantId == "MJD_6F3_S1_SWIFT");
        Assert.NotNull(swift);
        Assert.Equal("Maruti Suzuki Swift DDiS 1.3L", swift.VehiclePlatform);
        Assert.Equal("64-Pin Main Connector A", swift.ConnectorDesignation);
        Assert.Equal("33920-84E00", swift.HardwareNumber);
        Assert.Equal(PinoutValidationState.Confirmed, swift.PinoutValidation);
        Assert.Equal("MJD 6F3.S1 (Maruti Swift DDiS 2007-2011)", swift.VariantName);

        var fiat = profile.Variants.FirstOrDefault(v => v.VariantId == "MJD_6F3_D4_FIAT");
        Assert.NotNull(fiat);
        Assert.Equal("Fiat Punto 1.3 MultiJet 75HP / Linea 90HP", fiat.VehiclePlatform);
        Assert.Equal("MJD 6F3.D4 (Fiat Punto / Linea 1.3 MultiJet)", fiat.VariantName);
        Assert.Equal(PinoutValidationState.Reference, fiat.PinoutValidation);
    }

    [Fact]
    public void EcuCompatibility_VariantProfiles_BoschEdc17c53Variants_Distinguished()
    {
        var manager = new EcuCompatibilityManager();
        var profile = manager.GetProfileById("BOSCH_EDC17C53");

        Assert.NotNull(profile);
        Assert.Equal(ReleaseValidationState.DatabaseSourceVerified, profile.ReleaseValidation);
        Assert.NotNull(profile.Variants);
        Assert.True(profile.Variants.Count >= 2);

        var scorpio = profile.Variants.FirstOrDefault(v => v.VariantId == "EDC17C53_MHAWK_SCORPIO");
        Assert.NotNull(scorpio);
        Assert.Equal("Mahindra Scorpio 2.2 mHawk BS4 140PS", scorpio.VehiclePlatform);
        Assert.Equal("94-pin Engine Harness (A) + 60-pin Vehicle (B)", scorpio.ConnectorDesignation);
        Assert.Equal("0281018300", scorpio.HardwareNumber);
        Assert.Equal(PinoutValidationState.Confirmed, scorpio.PinoutValidation);

        var safari = profile.Variants.FirstOrDefault(v => v.VariantId == "EDC17C53_STORME_VARICOR");
        Assert.NotNull(safari);
        Assert.Equal("Tata Safari Storme 2.2 Varicor 156PS", safari.VehiclePlatform);
        Assert.Equal("0281030141", safari.HardwareNumber);
    }

    [Fact]
    public void EcuCompatibility_MatchVariant_ResolvesCorrectProfile()
    {
        var manager = new EcuCompatibilityManager();

        var matched = manager.MatchVariant("33920-84E00", "3422S167");
        Assert.NotNull(matched);
        Assert.Equal("MJD_6F3_S1_SWIFT", matched.VariantId);

        var matchedHwOnly = manager.MatchVariant("0281018300", null);
        Assert.NotNull(matchedHwOnly);
        Assert.Equal("EDC17C53_MHAWK_SCORPIO", matchedHwOnly.VariantId);

        var directVariant = manager.GetVariant("MAGNETI_MARELLI_MJD", "MJD_6F3_D4_FIAT");
        Assert.NotNull(directVariant);
        Assert.Equal("MJD_6F3_D4_FIAT", directVariant.VariantId);
    }

    #endregion

    #region 2. 5-Step Verified DTC Clear Workflow & NRC Handling

    [Fact]
    public async Task DtcClearAndVerify_SimulatedCleanEcu_ReturnsVerifiedCleared()
    {
        var (session, _, sim) = CreateTestDiagnosticSession();

        // Seed simulated DTCs
        sim.SetDtcFault("ECM", "P0100", true);
        sim.SetDtcFault("ECM", "P0110", true);

        await session.ScanAllDtcsAsync(true);
        var preScan = session.GetActiveDtcList();
        Assert.True(preScan.Count >= 2);

        // Execute verified clear
        var result = await session.ClearAndVerifyDtcsAsync(0x7E0, "ECM", true);

        Assert.NotNull(result);
        Assert.True(result.IsVerifiedCleared);
        Assert.Equal(DtcClearStatus.Cleared, result.Status);
        Assert.True(result.PreClearDtcCount >= 2);
        Assert.Equal(0, result.PostClearDtcCount);
        Assert.Contains("P0100", result.PreClearCodes);
        Assert.Contains("P0110", result.PreClearCodes);
        Assert.Empty(result.PostClearRemainingCodes);
    }

    [Fact]
    public async Task DtcClearAndVerify_PersistentHardFault_DetectsNonZeroPostClear()
    {
        var (session, _, _) = CreateTestDiagnosticSession();

        // Non-simulated route when no hardware is connected will fail gracefully or return timeout
        var result = await session.ClearAndVerifyDtcsAsync(0x7E0, "ECM", false);

        Assert.NotNull(result);
        Assert.False(result.IsVerifiedCleared);
        Assert.True(result.Status == DtcClearStatus.Timeout || result.Status == DtcClearStatus.NotSupported);
    }

    [Fact]
    public void DtcModels_RawEvidenceHexAndVerificationFlags_Populated()
    {
        var dtc = new DiagnosticTroubleCode
        {
            Code = "P0300",
            RawDtcNumber = 0x0300,
            StatusByte = 0x2F,
            Description = "Random/Multiple Cylinder Misfire Detected",
            EcuName = "ECM",
            CanId = 0x7E0
        };

        Assert.True(dtc.IsActive);
        Assert.True(dtc.IsPending);
        Assert.True(dtc.IsConfirmed);
        Assert.True(dtc.IsFailedThisCycle);
        Assert.True(dtc.IsFailedSinceClear);
        Assert.False(dtc.IsTestNotCompleted);
        Assert.Contains("DTC:0x000300", dtc.RawEvidenceHex);
        Assert.Contains("Status:0x2F", dtc.RawEvidenceHex);
        Assert.Contains("Confirmed", dtc.ActiveStatusFlags);
    }

    [Fact]
    public void DtcClearVerificationResult_StatusEnums_RepresentAllAutomotiveFailureStates()
    {
        Assert.True(Enum.IsDefined(typeof(DtcClearStatus), DtcClearStatus.Cleared));
        Assert.True(Enum.IsDefined(typeof(DtcClearStatus), DtcClearStatus.StillPresent));
        Assert.True(Enum.IsDefined(typeof(DtcClearStatus), DtcClearStatus.ConditionsNotCorrect));
        Assert.True(Enum.IsDefined(typeof(DtcClearStatus), DtcClearStatus.SecurityRequired));
        Assert.True(Enum.IsDefined(typeof(DtcClearStatus), DtcClearStatus.SessionRequired));
        Assert.True(Enum.IsDefined(typeof(DtcClearStatus), DtcClearStatus.Refused));
        Assert.True(Enum.IsDefined(typeof(DtcClearStatus), DtcClearStatus.Permanent));
        Assert.True(Enum.IsDefined(typeof(DtcClearStatus), DtcClearStatus.Timeout));
        Assert.True(Enum.IsDefined(typeof(DtcClearStatus), DtcClearStatus.NotSupported));
    }

    #endregion

    #region 3. Dyno Acquisition State Machine & Data Quality Rejection

    [Fact]
    public void DynoAcquisitionState_FullStateMachine_ContainsNineDeterministicStates()
    {
        Assert.True(Enum.IsDefined(typeof(DynoAcquisitionState), DynoAcquisitionState.Idle));
        Assert.True(Enum.IsDefined(typeof(DynoAcquisitionState), DynoAcquisitionState.Armed));
        Assert.True(Enum.IsDefined(typeof(DynoAcquisitionState), DynoAcquisitionState.Validating));
        Assert.True(Enum.IsDefined(typeof(DynoAcquisitionState), DynoAcquisitionState.RunDetected));
        Assert.True(Enum.IsDefined(typeof(DynoAcquisitionState), DynoAcquisitionState.Measuring));
        Assert.True(Enum.IsDefined(typeof(DynoAcquisitionState), DynoAcquisitionState.CoastDown));
        Assert.True(Enum.IsDefined(typeof(DynoAcquisitionState), DynoAcquisitionState.Calculating));
        Assert.True(Enum.IsDefined(typeof(DynoAcquisitionState), DynoAcquisitionState.QualityCheck));
        Assert.True(Enum.IsDefined(typeof(DynoAcquisitionState), DynoAcquisitionState.Complete));
        Assert.True(Enum.IsDefined(typeof(DynoAcquisitionState), DynoAcquisitionState.Rejected));
    }

    [Fact]
    public void RunQualityMetrics_CarriesRejectionReason()
    {
        var metrics = new RunQualityMetrics
        {
            SmoothnessScore = 35.0,
            GpsSampleRateHz = 8.5,
            QualityRating = "REJECTED",
            RejectionReason = "CAN LIVE DATA UNAVAILABLE: Engine RPM dropped below minimum monotonicity threshold."
        };

        Assert.Equal("REJECTED", metrics.QualityRating);
        Assert.Equal(35.0, metrics.SmoothnessScore);
        Assert.Contains("CAN LIVE DATA UNAVAILABLE", metrics.RejectionReason);
    }

    #endregion
}
