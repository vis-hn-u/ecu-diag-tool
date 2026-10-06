using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Engine;

public interface IEngineSimulator
{
    EngineParameters Parameters { get; set; }
    EngineInputs Inputs { get; set; }
    EngineOutputs CurrentOutputs { get; }
    void Reset();
    EngineOutputs Step(double dtSeconds = 0.05);
    List<EngineOutputs> RunDynoPullSimulation(double startRpm = 2000.0, double endRpm = 6800.0, double stepRpm = 100.0);
}

public class EngineSimulator : IEngineSimulator
{
    public EngineParameters Parameters { get; set; } = new();
    public EngineInputs Inputs { get; set; } = new();
    public EngineOutputs CurrentOutputs { get; private set; } = new();

    private double _currentRpm = 850.0;
    private double _currentCoolantTempC = 88.0;
    private double _currentOilTempC = 92.0;

    public EngineSimulator()
    {
        Reset();
    }

    public void Reset()
    {
        _currentRpm = Parameters.IdleRpm;
        _currentCoolantTempC = 88.0;
        _currentOilTempC = 92.0;
        CurrentOutputs = CalculateOutputs(Parameters.IdleRpm, 0.0, 1);
    }

    public EngineOutputs Step(double dtSeconds = 0.05)
    {
        double tps = Math.Clamp(Inputs.ThrottlePositionPct, 0.0, 100.0);
        int gear = Math.Clamp(Inputs.Gear, 1, 6);

        // RPM Dynamics
        double targetRpm;
        if (tps < 1.0)
        {
            targetRpm = Parameters.IdleRpm;
        }
        else
        {
            targetRpm = Parameters.IdleRpm + (Parameters.RedlineRpm - Parameters.IdleRpm) * (tps / 100.0);
        }

        // RPM slew rate based on engine load & gear
        double rpmRate = (targetRpm > _currentRpm)
            ? (2500.0 / Math.Sqrt(gear)) * (tps / 100.0)
            : -3000.0;

        _currentRpm = Math.Clamp(_currentRpm + rpmRate * dtSeconds, Parameters.IdleRpm, Parameters.RevLimitRpm);

        // Calculate outputs from physical model
        CurrentOutputs = CalculateOutputs(_currentRpm, tps, gear);
        return CurrentOutputs;
    }

    private EngineOutputs CalculateOutputs(double rpm, double tps, int gear)
    {
        var outState = new EngineOutputs();
        outState.Rpm = Math.Round(rpm, 1);

        // 1. Manifold Pressure & Boost
        double atmosphericKpa = Inputs.AmbientPressureKpa;
        double idleMapKpa = 32.0; // Idle vacuum
        double targetMapKpa;

        if (tps < 2.0)
        {
            targetMapKpa = idleMapKpa;
            outState.OperatingState = "Idle";
        }
        else if (tps <= 25.0)
        {
            targetMapKpa = idleMapKpa + (atmosphericKpa - idleMapKpa) * (tps / 25.0);
            outState.OperatingState = "Cruise";
        }
        else
        {
            // Boost build-up for turbocharged engines
            double boostFraction = (tps - 25.0) / 75.0;
            double maxBoostKpa = Parameters.IsTurbocharged ? Parameters.MaxBoostBar * 100.0 : 0.0;
            // Boost threshold starts above 2200 RPM
            double rpmSpool = Math.Clamp((rpm - 1800.0) / 1200.0, 0.0, 1.0);
            targetMapKpa = atmosphericKpa + (maxBoostKpa * boostFraction * rpmSpool);
            outState.OperatingState = tps > 75.0 ? "FullLoad" : "Acceleration";
        }

        if (rpm >= Parameters.RedlineRpm)
        {
            outState.OperatingState = "RevLimit";
        }

        outState.MapKpa = Math.Round(targetMapKpa, 1);
        outState.BoostBar = Math.Max(0.0, Math.Round((targetMapKpa - atmosphericKpa) / 100.0, 2));

        // 2. Volumetric Efficiency & Airflow (MAF)
        // Peak VE around 4500 RPM
        double rpmNormalized = rpm / 4500.0;
        double veBase = 0.88 * Math.Exp(-0.5 * Math.Pow((rpmNormalized - 1.0) / 0.55, 2));
        double volumetricEfficiency = Math.Clamp(veBase * (0.6 + 0.4 * (tps / 100.0)), 0.45, 0.98);

        // MAF (g/s) = (RPM / 120) * Displacement * AirDensity * (MAP / Atmospheric) * VE
        double airDensityGramsPerLiter = 1.18; // at ~25C
        double maf = (rpm / 120.0) * Parameters.DisplacementLiters * airDensityGramsPerLiter * (targetMapKpa / 101.3) * volumetricEfficiency;
        outState.MafGramsPerSec = Math.Round(Math.Max(2.5, maf), 2);

        // 3. Engine Load %
        double theoreticalMaxCharge = (Parameters.DisplacementLiters * airDensityGramsPerLiter * (atmosphericKpa / 101.3)) / Parameters.CylinderCount;
        double actualCylinderCharge = (maf / (rpm * Parameters.CylinderCount / 120.0));
        outState.EngineLoadPct = Math.Clamp(Math.Round((actualCylinderCharge / theoreticalMaxCharge) * 100.0, 1), 10.0, 240.0);

        // 4. Target AFR & Lambda
        double targetAfr;
        if (outState.OperatingState == "Idle" || outState.OperatingState == "Cruise")
        {
            targetAfr = 14.7; // Stoichiometric
        }
        else if (outState.OperatingState == "Acceleration")
        {
            targetAfr = 13.2; // Power enrichment
        }
        else // FullLoad / WOT
        {
            targetAfr = Parameters.IsTurbocharged ? 11.8 : 12.5; // Rich for knock protection & cooling
        }
        outState.Afr = targetAfr;
        outState.Lambda = Math.Round(targetAfr / 14.7, 3);

        // 5. Ignition Timing Advance (deg BTDC)
        // High MAP reduces advance to prevent detonation; High RPM increases advance
        double baseAdvance = 36.0 * (rpm / 6000.0);
        double loadRetard = (outState.EngineLoadPct / 100.0) * 18.0;
        outState.IgnitionAdvanceDeg = Math.Clamp(Math.Round(baseAdvance - loadRetard + 8.0, 1), 4.0, 42.0);

        // 6. Torque & Power
        // Peak Torque for 2.0L Turbo ~ 350-380 Nm around 2500-4800 RPM
        double torqueScale = (tps / 100.0) * (targetMapKpa / 101.3) * volumetricEfficiency;
        double maxEngineTorqueNm = Parameters.IsTurbocharged ? 380.0 : 210.0;
        double frictionLossNm = 25.0 + (rpm / 1000.0) * 4.5;
        double netTorque = Math.Max(15.0, (maxEngineTorqueNm * torqueScale) - frictionLossNm);

        if (outState.OperatingState == "Idle") netTorque = 40.0;
        if (outState.OperatingState == "RevLimit") netTorque *= 0.2; // Cut fuel/ignition on limiter

        outState.TorqueNm = Math.Round(netTorque, 1);
        outState.PowerKw = PowerTorqueConverter.CalculateKw(outState.TorqueNm, rpm);
        outState.PowerHp = PowerTorqueConverter.CalculateHp(outState.TorqueNm, rpm);

        // 7. Fuel Flow
        // Fuel Flow (L/h) = (MAF g/s / AFR) * 3600 / GasolineDensity (750 g/L)
        double fuelGramsPerSec = outState.MafGramsPerSec / outState.Afr;
        outState.FuelFlowLitersPerHour = Math.Round(fuelGramsPerSec * 3600.0 / 750.0, 2);

        // 8. Vehicle Speed
        // Gear ratios: 1st=3.6, 2nd=2.1, 3rd=1.4, 4th=1.0, 5th=0.8, 6th=0.65, FinalDrive=3.9, TireCircumference=2.0m
        double[] gearRatios = { 3.6, 2.1, 1.4, 1.0, 0.8, 0.65 };
        double currentRatio = gearRatios[gear - 1] * 3.9;
        double wheelRpm = rpm / currentRatio;
        outState.VehicleSpeedKmh = Math.Round((wheelRpm * 2.0 * 60.0) / 1000.0, 1);

        // Temperatures & Pressures
        outState.CoolantTempC = _currentCoolantTempC;
        outState.OilTempC = _currentOilTempC;
        outState.IntakeAirTempC = Math.Round(Inputs.AmbientTempC + outState.BoostBar * 18.0, 1);
        outState.FuelPressureKpa = 400.0;
        outState.RailPressureMpa = Math.Round(5.0 + (outState.EngineLoadPct / 100.0) * 15.0, 1);
        outState.TimestampUtc = DateTime.UtcNow;

        return outState;
    }

    public List<EngineOutputs> RunDynoPullSimulation(double startRpm = 2000.0, double endRpm = 6800.0, double stepRpm = 100.0)
    {
        var pullData = new List<EngineOutputs>();
        for (double r = startRpm; r <= endRpm; r += stepRpm)
        {
            var outState = CalculateOutputs(r, 100.0, 4); // WOT in 4th gear (1:1 direct drive)
            pullData.Add(outState);
        }
        return pullData;
    }
}
