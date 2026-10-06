using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CanEngineeringPlatform.Core.Hardware;
using CanEngineeringPlatform.Core.Hardware.Automotive;
using CanEngineeringPlatform.Core.Hardware.Transports;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Sessions;
using CanEngineeringPlatform.Core.Signals;
using CanEngineeringPlatform.Core.Signals.Calculated;
using CanEngineeringPlatform.Core.Topology;
using CanEngineeringPlatform.Core.Vehicles;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class VehicleAndSessionArchitectureTests
{
    // ==========================================
    // 1. Vehicle Profile & Gearing Tests
    // ==========================================
    [Fact]
    public void VehicleProfile_TireAndGearingCalculations_AreAccurate()
    {
        var profile = new VehicleProfile
        {
            Vin = "WVWZZZAUZEP123456",
            Make = "Volkswagen",
            Model = "Golf R",
            Year = 2021,
            CurbWeightKg = 1450.0,
            TestWeightKg = 1530.0,
            Tires = new TireSpecs
            {
                SizeSpec = "235/40R18",
                WidthMm = 235.0,
                AspectRatioPct = 40.0,
                RimDiameterInches = 18.0
            },
            Transmission = new TransmissionSpecs
            {
                FinalDriveRatio = 3.944,
                GearRatios = new double[] { 3.769, 2.087, 1.457, 1.109, 0.974, 0.733 }
            },
            Engine = new EngineSpecs
            {
                RedlineRpm = 6800.0,
                IdleRpm = 800.0
            }
        };

        // 1. Tire rolling radius & circumference
        // Sidewall: 235 * 0.40 = 94 mm. Rim: 18 * 25.4 = 457.2 mm. Total Dia = 645.2 mm. Radius = 0.3226 m
        Assert.InRange(profile.Tires.RollingRadiusMeters, 0.32, 0.33);
        Assert.InRange(profile.Tires.CircumferenceMeters, 2.00, 2.06);

        // 2. Speed at 3000 RPM in 3rd gear (ratio 1.457, final drive 3.944 -> total ratio 5.746)
        double speed3rd = profile.CalculateSpeedAtRpm(3, 3000.0);
        Assert.InRange(speed3rd, 60.0, 70.0); // ~63-66 km/h

        // 3. Round-trip RPM at that calculated speed
        double rpmCalculated = profile.CalculateRpmAtSpeed(3, speed3rd);
        Assert.InRange(rpmCalculated, 2980.0, 3020.0);

        // 4. Zero speed returns idle RPM
        Assert.Equal(profile.Engine.IdleRpm, profile.CalculateRpmAtSpeed(1, 0.0));
    }

    [Fact]
    public void VehicleProfileService_CrudAndActiveProfileManagement()
    {
        var service = new VehicleProfileService();
        var defaultProfile = service.GetActiveProfile();
        Assert.NotNull(defaultProfile);

        var newProfile = new VehicleProfile
        {
            Name = "Track Spec RS3",
            Vin = "WAUZZZ8V1GA999999",
            Make = "Audi",
            Model = "RS3 2.5 TFSI",
            Year = 2022
        };

        service.SaveProfile(newProfile);
        Assert.Contains(service.GetAllProfiles(), p => p.Id == newProfile.Id);

        service.SetActiveProfile(newProfile.Id);
        Assert.Equal(newProfile.Id, service.GetActiveProfile().Id);

        // Cannot delete sole profile or active profile cleanly transitions
        bool deleted = service.DeleteProfile(newProfile.Id);
        Assert.True(deleted);
        Assert.NotEqual(newProfile.Id, service.GetActiveProfile().Id);
    }

    // ==========================================
    // 2. Custom Signal Definition & Multi-Source Decoding
    // ==========================================
    [Fact]
    public void CustomSignalDefinition_CanBitfield_LittleEndian_DecodesCorrectly()
    {
        // 16-bit Engine Speed signal: start bit 16, length 16, factor 0.25, offset 0
        // Value = 3200 RPM -> raw = 12800 = 0x3200 -> in little endian bytes: [0x00, 0x32] at bytes 2,3
        byte[] canPayload = new byte[] { 0x00, 0x00, 0x00, 0x32, 0x00, 0x00, 0x00, 0x00 };

        var signal = new CustomSignalDefinition
        {
            Name = "EngineSpeed",
            SourceType = SignalDataSource.CanMessage,
            SourceIdentifier = 0x280,
            StartBit = 16,
            BitLength = 16,
            ByteOrder = SignalEndianness.LittleEndian,
            Factor = 0.25,
            Offset = 0.0,
            Units = "rpm"
        };

        double val = signal.Decode(canPayload);
        Assert.Equal(3200.0, val);
    }

    [Fact]
    public void CustomSignalDefinition_CustomEquation_EvaluatesCorrectly()
    {
        // Coolant Temp: raw - 40
        // Raw byte = 130 -> 130 - 40 = 90 °C
        byte[] payload = new byte[] { 0x82 }; // 0x82 = 130

        var signal = new CustomSignalDefinition
        {
            Name = "CoolantTemp",
            SourceType = SignalDataSource.ObdPid,
            SourceIdentifier = 0x05,
            StartBit = 0,
            BitLength = 8,
            CustomEquation = "raw - 40",
            Units = "°C",
            MinLimit = -40,
            MaxLimit = 150
        };

        double val = signal.Decode(payload);
        Assert.Equal(90.0, val);
    }

    [Fact]
    public void CustomSignalDefinition_LimitsClamping_EnforcesBounds()
    {
        // Value 200 clamped to MaxLimit 150
        byte[] payload = new byte[] { 200 };

        var signal = new CustomSignalDefinition
        {
            Name = "ClampedSignal",
            StartBit = 0,
            BitLength = 8,
            Factor = 1.0,
            Offset = 0.0,
            MaxLimit = 150.0
        };

        double val = signal.Decode(payload);
        Assert.Equal(150.0, val);
    }

    // ==========================================
    // 3. Parameter Metadata & Confidence Tests
    // ==========================================
    [Fact]
    public void ParameterMetadata_ConfidenceScores_ReflectHierarchy()
    {
        var metaObserved = new ParameterMetadata
        {
            ParameterId = "EngineRpm",
            Confidence = ParameterConfidence.Observed
        };
        var metaFactory = new ParameterMetadata
        {
            ParameterId = "EngineRpm",
            Confidence = ParameterConfidence.FactoryCalibrated
        };
        var metaEstimated = new ParameterMetadata
        {
            ParameterId = "EngineRpm",
            Confidence = ParameterConfidence.Estimated
        };

        Assert.Equal(0.75, metaObserved.ConfidenceScore);
        Assert.Equal(1.0, metaFactory.ConfidenceScore);
        Assert.Equal(0.5, metaEstimated.ConfidenceScore);
    }

    // ==========================================
    // 4. Calculated Signal Expression Engine Tests
    // ==========================================
    [Fact]
    public void CalculatedSignalEngine_EvaluatesArithmetic_AndPrecedence()
    {
        var engine = new CalculatedSignalEngine();
        var context = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["MapKpa"] = 236.325,
            ["BaroKpa"] = 101.325,
            ["TorqueNm"] = 400.0,
            ["Rpm"] = 5252.0
        };

        // 1. Boost PSI = (MAP - Baro) * 0.1450377 -> (236.325 - 101.325) * 0.1450377 = 135 * 0.1450377 = 19.58 psi
        double boostPsi = engine.Evaluate("(MapKpa - BaroKpa) * 0.1450377", context);
        Assert.InRange(boostPsi, 19.57, 19.59);

        // 2. Horsepower = (TorqueNm * Rpm) / 7120.8 (approx 295.0 hp)
        double hp = engine.Evaluate("(TorqueNm * Rpm) / 7120.8", context);
        Assert.InRange(hp, 294.0, 296.0);

        // 3. Power operator (^) and precedence
        double powResult = engine.Evaluate("2 + 3 * 2 ^ 2", context); // 2 + 3*4 = 14
        Assert.Equal(14.0, powResult);
    }

    [Fact]
    public void CalculatedSignalEngine_SupportsMathFunctions()
    {
        var engine = new CalculatedSignalEngine();
        var context = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["AccelX"] = 0.6,
            ["AccelY"] = 0.8,
            ["Val"] = -15.5
        };

        // Total G = sqrt(AccelX^2 + AccelY^2) = sqrt(0.36 + 0.64) = 1.0
        double totalG = engine.Evaluate("sqrt(AccelX^2 + AccelY^2)", context);
        Assert.Equal(1.0, totalG);

        // abs
        double absVal = engine.Evaluate("abs(Val)", context);
        Assert.Equal(15.5, absVal);

        // clamp(val, min, max)
        double clamped = engine.Evaluate("clamp(Val, 0, 100)", context);
        Assert.Equal(0.0, clamped);
    }

    [Fact]
    public void CalculatedSignalEngine_ExtractsDependencies_Correctly()
    {
        string expr = "(EngineTorqueNm * GearRatio * FinalDrive) / TireRadiusM";
        var deps = CalculatedSignalEngine.ExtractDependencies(expr);

        Assert.Equal(4, deps.Count);
        Assert.Contains("EngineTorqueNm", deps);
        Assert.Contains("GearRatio", deps);
        Assert.Contains("FinalDrive", deps);
        Assert.Contains("TireRadiusM", deps);
    }

    [Fact]
    public void CalculatedSignalEngine_TopologicalSorting_AndMultiStageEvaluation()
    {
        var engine = new CalculatedSignalEngine();

        // Stage 1: BoostPsi from MapKpa
        engine.RegisterSignal(new CalculatedSignalDefinition("BoostPsi", "Boost PSI", "(MapKpa - 100) * 0.145"));
        // Stage 2: BoostBar from BoostPsi (dependent on BoostPsi)
        engine.RegisterSignal(new CalculatedSignalDefinition("BoostBar", "Boost Bar", "BoostPsi / 14.5"));

        var order = engine.GetEvaluationOrder().ToList();
        Assert.True(order.IndexOf("BoostPsi") < order.IndexOf("BoostBar"));

        var inputs = new Dictionary<string, double> { ["MapKpa"] = 250.0 };
        var results = engine.EvaluateAll(inputs);

        Assert.True(results.ContainsKey("BoostPsi"));
        Assert.True(results.ContainsKey("BoostBar"));
        Assert.InRange(results["BoostPsi"], 21.7, 21.8);
        Assert.InRange(results["BoostBar"], 1.49, 1.51);
    }

    [Fact]
    public void CalculatedSignalEngine_DetectsCircularDependencies_ThrowsException()
    {
        var engine = new CalculatedSignalEngine();

        // Signal A depends on Signal B
        engine.RegisterSignal(new CalculatedSignalDefinition("SignalA", "Signal A", "SignalB * 2"));

        // Registering Signal B depending on Signal A creates cycle: SignalA -> SignalB -> SignalA
        Assert.Throws<InvalidOperationException>(() =>
        {
            engine.RegisterSignal(new CalculatedSignalDefinition("SignalB", "Signal B", "SignalA + 5"));
        });
    }

    // ==========================================
    // 5. Engineering Session & Run Lifecycle Tests
    // ==========================================
    [Fact]
    public void SessionManagementService_FullSessionAndRunLifecycle()
    {
        var vehicleService = new VehicleProfileService();
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var simTransport = new SimulationTransport();
        var hwManager = new HardwareManager(pipeline, new[] { simTransport });

        var sessionService = new SessionManagementService(vehicleService, hwManager, logger);

        // 1. Start Session
        var session = sessionService.StartSession(
            name: "Dyno Calibration Session #1",
            description: "Evaluating Stage 1 boost map",
            technician: "Alex",
            tags: new List<string> { "Dyno", "Stage1" }
        );

        Assert.NotNull(session);
        Assert.True(session.IsActive);
        Assert.Equal("default-vehicle-profile", session.VehicleProfileId);
        Assert.Equal("virtual-simulation", session.HardwareTransportId);
        Assert.NotNull(session.LogSessionId);

        // 2. Start Run 1 (Dyno Pull)
        var run1 = sessionService.StartRun(session.SessionId, "WOT 3rd Gear Pull 1", RunType.DynoPull);
        Assert.NotNull(run1);
        Assert.Equal(1, run1.RunNumber);
        Assert.Equal(RunType.DynoPull, run1.Type);
        Assert.False(run1.EndTimeUtc.HasValue);

        // 3. Stop Run 1 with metrics
        var metrics = new Dictionary<string, double>
        {
            ["PeakTorqueNm"] = 425.0,
            ["PeakPowerHp"] = 315.0,
            ["PeakBoostBar"] = 1.38
        };
        var stoppedRun = sessionService.StopRun(session.SessionId, run1.RunId, metrics);
        Assert.NotNull(stoppedRun);
        Assert.True(stoppedRun.EndTimeUtc.HasValue);
        Assert.Equal(425.0, stoppedRun.SummaryMetrics["PeakTorqueNm"]);

        // 4. Start Run 2 (Acceleration Test)
        var run2 = sessionService.StartRun(session.SessionId, "0-100 kph Launch", RunType.AccelerationTest);
        Assert.NotNull(run2);
        Assert.Equal(2, run2.RunNumber);

        // 5. Stop Session
        var stoppedSession = sessionService.StopSession(session.SessionId);
        Assert.NotNull(stoppedSession);
        Assert.False(stoppedSession.IsActive);
        Assert.True(stoppedSession.EndTimeUtc.HasValue);
        Assert.Equal(2, stoppedSession.Runs.Count);
    }
}
