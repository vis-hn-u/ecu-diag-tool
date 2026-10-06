using System;

namespace CanEngineeringPlatform.Core.Engine;

public class EngineParameters
{
    public double DisplacementLiters { get; set; } = 2.0;
    public int CylinderCount { get; set; } = 4;
    public double IdleRpm { get; set; } = 850.0;
    public double RedlineRpm { get; set; } = 6800.0;
    public double RevLimitRpm { get; set; } = 7000.0;
    public bool IsTurbocharged { get; set; } = true;
    public double MaxBoostBar { get; set; } = 1.35; // ~19.5 psi
    public double CompressionRatio { get; set; } = 9.8;
    public double InjectorFlowCcPerMin { get; set; } = 550.0;
    public double VehicleWeightKg { get; set; } = 1450.0;
}

public class EngineInputs
{
    public double ThrottlePositionPct { get; set; } = 0.0; // 0 - 100 %
    public double AmbientTempC { get; set; } = 25.0;
    public double AmbientPressureKpa { get; set; } = 101.3;
    public double RequestedTorqueNm { get; set; } = 0.0;
    public int Gear { get; set; } = 1; // 1..6
    public bool BrakePressed { get; set; } = false;
    public bool ClutchPressed { get; set; } = false;
}

public class EngineOutputs
{
    public double Rpm { get; set; } = 850.0;
    public double MapKpa { get; set; } = 35.0; // Manifold Absolute Pressure
    public double BoostBar { get; set; } = 0.0; // Gauge boost pressure
    public double MafGramsPerSec { get; set; } = 3.5;
    public double EngineLoadPct { get; set; } = 18.0;
    public double TorqueNm { get; set; } = 45.0;
    public double PowerKw { get; set; } = 4.0;
    public double PowerHp { get; set; } = 5.4;
    public double Lambda { get; set; } = 1.0;
    public double Afr { get; set; } = 14.7;
    public double IgnitionAdvanceDeg { get; set; } = 12.0;
    public double FuelFlowLitersPerHour { get; set; } = 0.9;
    public double CoolantTempC { get; set; } = 88.0;
    public double IntakeAirTempC { get; set; } = 32.0;
    public double OilTempC { get; set; } = 92.0;
    public double FuelPressureKpa { get; set; } = 400.0;
    public double RailPressureMpa { get; set; } = 15.0; // Direct Injection
    public double VehicleSpeedKmh { get; set; } = 0.0;
    public string OperatingState { get; set; } = "Idle"; // Idle, Cruise, Acceleration, FullLoad, Deceleration, RevLimit
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public class EngineOperatingPoint
{
    public double Rpm { get; set; }
    public double LoadPct { get; set; }
    public int CellIndexRpm { get; set; }
    public int CellIndexLoad { get; set; }
    public double WeightRpm { get; set; }
    public double WeightLoad { get; set; }
}

public static class PowerTorqueConverter
{
    // Power (kW) = Torque (Nm) * RPM / 9548.8
    public static double CalculateKw(double torqueNm, double rpm)
    {
        if (rpm <= 0 || torqueNm <= 0) return 0.0;
        return Math.Round(torqueNm * rpm / 9548.8, 2);
    }

    // Power (HP) = Power (kW) * 1.34102 or Torque (Nm) * RPM / 7120.8
    public static double CalculateHp(double torqueNm, double rpm)
    {
        if (rpm <= 0 || torqueNm <= 0) return 0.0;
        return Math.Round(torqueNm * rpm / 7120.8, 2);
    }

    // Torque (Nm) = Power (kW) * 9548.8 / RPM
    public static double CalculateTorqueNmFromKw(double kw, double rpm)
    {
        if (rpm <= 0 || kw <= 0) return 0.0;
        return Math.Round(kw * 9548.8 / rpm, 2);
    }

    public static double NmToLbFt(double nm) => Math.Round(nm * 0.737562, 2);
    public static double LbFtToNm(double lbft) => Math.Round(lbft / 0.737562, 2);
    public static double KwToHp(double kw) => Math.Round(kw * 1.34102, 2);
    public static double HpToKw(double hp) => Math.Round(hp / 1.34102, 2);
}
