using System;
using System.Linq;
using CanEngineeringPlatform.Core.Dyno;
using CanEngineeringPlatform.Core.Engine;
using Xunit;

namespace CanEngineeringPlatform.Tests;

public class EngineSimulatorAndDynoTests
{
    [Fact]
    public void PowerTorqueConverter_ValidCalculations_MatchPhysics()
    {
        // 350 Nm at 5000 RPM -> Power (kW) = 350 * 5000 / 9548.8 = 183.27 kW
        double kw = PowerTorqueConverter.CalculateKw(350, 5000);
        Assert.Equal(183.27, kw);

        // Power (HP) = 350 * 5000 / 7120.8 = 245.76 HP
        double hp = PowerTorqueConverter.CalculateHp(350, 5000);
        Assert.Equal(245.76, hp);

        // Unit conversions
        Assert.Equal(258.15, PowerTorqueConverter.NmToLbFt(350));
        Assert.Equal(245.77, PowerTorqueConverter.KwToHp(183.27));
    }

    [Fact]
    public void EngineSimulator_Step_IsDeterministic()
    {
        var sim1 = new EngineSimulator();
        var sim2 = new EngineSimulator();

        sim1.Inputs.ThrottlePositionPct = 75.0;
        sim2.Inputs.ThrottlePositionPct = 75.0;

        for (int i = 0; i < 20; i++)
        {
            sim1.Step(0.05);
            sim2.Step(0.05);
        }

        Assert.Equal(sim1.CurrentOutputs.Rpm, sim2.CurrentOutputs.Rpm);
        Assert.Equal(sim1.CurrentOutputs.TorqueNm, sim2.CurrentOutputs.TorqueNm);
        Assert.Equal(sim1.CurrentOutputs.MapKpa, sim2.CurrentOutputs.MapKpa);
        Assert.Equal(sim1.CurrentOutputs.BoostBar, sim2.CurrentOutputs.BoostBar);
    }

    [Fact]
    public void DynoAnalysisService_RunPull_FindsPeakPowerAndTorque()
    {
        var sim = new EngineSimulator();
        var dyno = new DynoAnalysisService(sim);

        var pull = dyno.GenerateSimulatedDynoPull("Test Pull", 2000, 6800);

        Assert.NotNull(pull);
        Assert.NotEmpty(pull.Points);
        Assert.True(pull.PeakTorqueNm > 200.0);
        Assert.True(pull.PeakPowerHp > 150.0);
        Assert.True(pull.PeakTorqueRpm >= 2000 && pull.PeakTorqueRpm <= 6800);
        Assert.True(pull.PeakPowerRpm >= 4000 && pull.PeakPowerRpm <= 6800);
        Assert.Equal("SIMULATED", pull.Type);

        var perf = dyno.CalculatePerformanceMetrics(pull);
        Assert.True(perf.ZeroToSixtyMphSeconds > 2.0 && perf.ZeroToSixtyMphSeconds < 12.0);
        Assert.True(perf.QuarterMileSeconds > 5.0 && perf.QuarterMileSeconds < 20.0);
    }
}
