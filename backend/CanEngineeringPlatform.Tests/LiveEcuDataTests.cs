using System;
using System.Collections.Generic;
using System.Linq;
using CanEngineeringPlatform.Core.Diagnostics.IsoTp;
using CanEngineeringPlatform.Core.Diagnostics.Session;
using CanEngineeringPlatform.Core.Diagnostics.Simulator;
using CanEngineeringPlatform.Core.LiveEcuData;
using CanEngineeringPlatform.Core.Logging;
using CanEngineeringPlatform.Core.Models;
using CanEngineeringPlatform.Core.Pipeline;
using CanEngineeringPlatform.Core.Topology;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class LiveEcuDataTests
{
    [Fact]
    public void CategorizeSignal_MapsAutomotiveDomainsCorrectly()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var isoTp = new IsoTpRouter();
        var ecuSim = new EcuSimulator(pipeline, isoTp);
        var diagSession = new DiagnosticSessionService(pipeline, isoTp, ecuSim);

        using var liveService = new LiveEcuDataService(pipeline, diagSession);

        var signals = new List<DecodedSignal>
        {
            new() { CanId = 0x280, MessageName = "EngineData", SignalName = "EngineRPM", EngineeringValue = 3200, Unit = "rpm" },
            new() { CanId = 0x280, MessageName = "EngineData", SignalName = "EngineTorque", EngineeringValue = 280, Unit = "Nm" },
            new() { CanId = 0x280, MessageName = "EngineData", SignalName = "BoostPressure", EngineeringValue = 1.15, Unit = "bar" },
            new() { CanId = 0x300, MessageName = "FuelSystem", SignalName = "RailPressure", EngineeringValue = 16.5, Unit = "MPa" },
            new() { CanId = 0x400, MessageName = "Emissions", SignalName = "EgrPosition", EngineeringValue = 45.0, Unit = "%" },
            new() { CanId = 0x410, MessageName = "ExhaustAftertreatment", SignalName = "DpfDiffPressure", EngineeringValue = 18.0, Unit = "hPa" },
            new() { CanId = 0x420, MessageName = "ScrSystem", SignalName = "AdBlueLevel", EngineeringValue = 78.0, Unit = "%" },
            new() { CanId = 0x430, MessageName = "NoxSensor", SignalName = "NoxPpm", EngineeringValue = 120.0, Unit = "ppm" },
            new() { CanId = 0x500, MessageName = "VehicleSpeed", SignalName = "WheelSpeedFrontLeft", EngineeringValue = 85.5, Unit = "km/h" }
        };

        // Ingest into LiveEcuDataService
        liveService.IngestDecodedSignals(signals);

        var allParams = liveService.GetAllParameters();
        Assert.Equal(9, allParams.Count);

        var engineParams = liveService.GetParametersByCategory(LiveParameterCategory.Engine);
        Assert.Contains(engineParams, p => p.Name == "EngineRPM");

        var turboParams = liveService.GetParametersByCategory(LiveParameterCategory.AirTurbo);
        Assert.Contains(turboParams, p => p.Name == "BoostPressure");

        var egrParams = liveService.GetParametersByCategory(LiveParameterCategory.Egr);
        Assert.Contains(egrParams, p => p.Name == "EgrPosition");

        var dpfParams = liveService.GetParametersByCategory(LiveParameterCategory.Dpf);
        Assert.Contains(dpfParams, p => p.Name == "DpfDiffPressure");

        var scrParams = liveService.GetParametersByCategory(LiveParameterCategory.ScrAdBlue);
        Assert.Contains(scrParams, p => p.Name == "AdBlueLevel");

        var noxParams = liveService.GetParametersByCategory(LiveParameterCategory.Nox);
        Assert.Contains(noxParams, p => p.Name == "NoxPpm");

        var vehicleParams = liveService.GetParametersByCategory(LiveParameterCategory.Vehicle);
        Assert.Contains(vehicleParams, p => p.Name == "WheelSpeedFrontLeft");
    }

    [Fact]
    public void RequestedVsActual_CalculatesDeltaCorrectly()
    {
        var topology = new TopologyEngine();
        var logger = new CanLoggerService();
        using var pipeline = new CanPipeline(topology, logger);
        var isoTp = new IsoTpRouter();
        var ecuSim = new EcuSimulator(pipeline, isoTp);
        var diagSession = new DiagnosticSessionService(pipeline, isoTp, ecuSim);

        using var liveService = new LiveEcuDataService(pipeline, diagSession);

        var signals = new List<DecodedSignal>
        {
            new() { CanId = 0x280, MessageName = "EngineData", SignalName = "ReqTorque", EngineeringValue = 350.0, Unit = "Nm" },
            new() { CanId = 0x280, MessageName = "EngineData", SignalName = "EngineTorque", EngineeringValue = 330.0, Unit = "Nm" },
            new() { CanId = 0x290, MessageName = "TurboData", SignalName = "BoostTarget", EngineeringValue = 1.20, Unit = "bar" },
            new() { CanId = 0x290, MessageName = "TurboData", SignalName = "Boost", EngineeringValue = 1.15, Unit = "bar" }
        };

        liveService.IngestDecodedSignals(signals);

        var pairs = liveService.GetRequestedVsActualPairs();
        Assert.NotEmpty(pairs);

        var torquePair = pairs.First(p => p.Id == "torque");
        Assert.Equal(350.0, torquePair.TargetValue);
        Assert.Equal(330.0, torquePair.ActualValue);
        Assert.Equal(-20.0, torquePair.ErrorDelta);

        var boostPair = pairs.First(p => p.Id == "boost");
        Assert.Equal(1.20, boostPair.TargetValue);
        Assert.Equal(1.15, boostPair.ActualValue);
        Assert.Equal(-0.05, boostPair.ErrorDelta);
    }
}
