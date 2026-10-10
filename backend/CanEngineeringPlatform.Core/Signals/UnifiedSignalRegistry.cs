using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CanEngineeringPlatform.Core.Models;

namespace CanEngineeringPlatform.Core.Signals;

public enum CanonicalSourceType
{
    Dbc,
    ObdPid,
    UdsDid,
    J1939Spn,
    Simulation,
    Calculated,
    Manual
}

public enum SignalQuality
{
    Valid,
    Stale,      // No updates for > 1500 ms
    Invalid,    // Out of plausible physical range
    Unknown     // Unmapped or discovery required
}

public class CanonicalSignalRecord
{
    public string CanonicalId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Category { get; init; } = "Powertrain";
    public string Unit { get; init; } = string.Empty;
    public double Value { get; set; }
    public string FormattedValue => $"{Value:0.##} {Unit}".Trim();
    public string RawSourceName { get; set; } = string.Empty;
    public CanonicalSourceType SourceType { get; set; } = CanonicalSourceType.Dbc;
    public double MinObserved { get; set; } = double.MaxValue;
    public double MaxObserved { get; set; } = double.MinValue;
    public long UpdateCount { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    public double Confidence { get; set; } = 1.0;

    public bool IsActive => (DateTime.UtcNow - LastUpdatedUtc).TotalMilliseconds < 1500;
    public SignalQuality Quality => !IsActive ? SignalQuality.Stale : SignalQuality.Valid;
}

public interface IUnifiedSignalRegistry
{
    void IngestSignal(string sourceName, double value, string unit, string sourceTransport = "CAN", CanonicalSourceType sourceType = CanonicalSourceType.Dbc);
    void IngestDecodedSignals(IEnumerable<DecodedSignal> signals);
    CanonicalSignalRecord? GetSignal(string canonicalId);
    IReadOnlyList<CanonicalSignalRecord> GetAllSignals();
    IReadOnlyList<CanonicalSignalRecord> GetActiveSignals();
    bool TryResolveCanonicalName(string sourceName, out string canonicalId);
    void Clear();
    event Action<CanonicalSignalRecord>? OnSignalUpdated;
    event Action<IReadOnlyList<CanonicalSignalRecord>>? OnBatchUpdated;
}

public class UnifiedSignalRegistry : IUnifiedSignalRegistry
{
    private static readonly Dictionary<string, (string CanonicalId, string DisplayName, string Category, string Unit, double MinPlausible, double MaxPlausible)> CanonicalDefinitions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "EngineRPM", ("EngineRPM", "Engine Speed", "Powertrain", "rpm", 0, 12000) },
        { "VehicleSpeed", ("VehicleSpeed", "Vehicle Speed", "Dynamics", "km/h", 0, 400) },
        { "EngineCoolantTemperature", ("EngineCoolantTemperature", "Coolant Temperature", "Thermal", "°C", -40, 160) },
        { "ThrottlePosition", ("ThrottlePosition", "Throttle Position", "Powertrain", "%", 0, 100) },
        { "AcceleratorPedalPosition", ("AcceleratorPedalPosition", "Accelerator Pedal Position", "Powertrain", "%", 0, 100) },
        { "EngineLoad", ("EngineLoad", "Engine Load", "Powertrain", "%", 0, 100) },
        { "FuelRailPressure", ("FuelRailPressure", "Fuel Rail Pressure", "Fuel", "bar", 0, 3000) },
        { "BoostPressure", ("BoostPressure", "Boost Pressure", "Powertrain", "bar", 0, 5) },
        { "MAP", ("MAP", "Manifold Absolute Pressure", "Powertrain", "kPa", 0, 400) },
        { "MAF", ("MAF", "Mass Air Flow Rate", "Powertrain", "g/s", 0, 1000) },
        { "Lambda", ("Lambda", "Air-Fuel Equivalence Ratio", "Combustion", "λ", 0.5, 2.5) },
        { "O2", ("O2", "Oxygen Sensor Voltage", "Combustion", "V", 0, 2) },
        { "BatteryVoltage", ("BatteryVoltage", "Battery Voltage", "Electrical", "V", 0, 32) },
        { "FuelRate", ("FuelRate", "Engine Fuel Rate", "Fuel", "L/h", 0, 150) },
        { "OilPressure", ("OilPressure", "Engine Oil Pressure", "Lubrication", "bar", 0, 15) },
        { "OilTemperature", ("OilTemperature", "Engine Oil Temperature", "Thermal", "°C", -40, 180) },
        { "IntakeAirTemperature", ("IntakeAirTemperature", "Intake Air Temperature", "Thermal", "°C", -40, 120) },
        { "AmbientTemperature", ("AmbientTemperature", "Ambient Air Temperature", "Thermal", "°C", -50, 70) },
        { "DPFPressure", ("DPFPressure", "DPF Differential Pressure", "Exhaust", "mbar", 0, 1000) },
        { "DPFSootLoad", ("DPFSootLoad", "DPF Soot Mass", "Exhaust", "g", 0, 150) },
        { "EGT", ("EGT", "Exhaust Gas Temperature", "Exhaust", "°C", 0, 1200) },
        { "EGRPosition", ("EGRPosition", "EGR Valve Position", "Exhaust", "%", 0, 100) },
        { "InjectionQuantity", ("InjectionQuantity", "Main Injection Quantity", "Fuel", "mg/str", 0, 200) },
        { "EngineTorque", ("EngineTorque", "Engine Indicated Torque", "Powertrain", "Nm", -200, 2000) },
        { "RequestedTorque", ("RequestedTorque", "Driver Requested Torque", "Powertrain", "Nm", -200, 2000) },
        { "ActualTorque", ("ActualTorque", "Actual Flywheel Torque", "Powertrain", "Nm", -200, 2000) },
        { "Gear", ("Gear", "Selected Transmission Gear", "Transmission", "gear", 0, 12) },
        { "WheelSpeedFL", ("WheelSpeedFL", "Front Left Wheel Speed", "Dynamics", "km/h", 0, 400) },
        { "WheelSpeedFR", ("WheelSpeedFR", "Front Right Wheel Speed", "Dynamics", "km/h", 0, 400) },
        { "WheelSpeedRL", ("WheelSpeedRL", "Rear Left Wheel Speed", "Dynamics", "km/h", 0, 400) },
        { "WheelSpeedRR", ("WheelSpeedRR", "Rear Right Wheel Speed", "Dynamics", "km/h", 0, 400) },
        { "SteeringAngle", ("SteeringAngle", "Steering Wheel Angle", "Dynamics", "deg", -900, 900) },
        { "BrakePressure", ("BrakePressure", "Brake Master Cylinder Pressure", "Dynamics", "bar", 0, 300) }
    };

    // Fast O(1) alias resolver mapping OEM and standard source signal names to canonical IDs
    private static readonly Dictionary<string, string> AliasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Engine RPM
        { "EngineRPM", "EngineRPM" },
        { "EngineSpeed", "EngineRPM" },
        { "EngSpd", "EngineRPM" },
        { "MO_Drehzahl", "EngineRPM" },
        { "CR_Ems_EngSpd", "EngineRPM" },
        { "ENGINE_RPM", "EngineRPM" },
        { "EngineSpeed_Sim", "EngineRPM" },
        { "RPM", "EngineRPM" },

        // Vehicle Speed
        { "VehicleSpeed", "VehicleSpeed" },
        { "VehSpd", "VehicleSpeed" },
        { "Fahrgeschwindigkeit", "VehicleSpeed" },
        { "VFzg", "VehicleSpeed" },
        { "VEHICLE_SPEED", "VehicleSpeed" },
        { "VehicleSpeed_Sim", "VehicleSpeed" },
        { "Speed", "VehicleSpeed" },

        // Engine Coolant Temperature
        { "EngineCoolantTemperature", "EngineCoolantTemperature" },
        { "EngineCoolantTemp", "EngineCoolantTemperature" },
        { "CoolantTemp", "EngineCoolantTemperature" },
        { "ECT", "EngineCoolantTemperature" },
        { "Kuehlmitteltemperatur", "EngineCoolantTemperature" },
        { "COOLANT_TEMP", "EngineCoolantTemperature" },
        { "EngineCoolantTemp_Sim", "EngineCoolantTemperature" },

        // Throttle Position
        { "ThrottlePosition", "ThrottlePosition" },
        { "Throttle", "ThrottlePosition" },
        { "DK_Winkel", "ThrottlePosition" },
        { "THROTTLE_POS", "ThrottlePosition" },
        { "ThrottlePosition_Sim", "ThrottlePosition" },
        { "TPS", "ThrottlePosition" },

        // Accelerator Pedal Position
        { "AcceleratorPedalPosition", "AcceleratorPedalPosition" },
        { "AcceleratorPedal", "AcceleratorPedalPosition" },
        { "AcceleratorPedalPositionD", "AcceleratorPedalPosition" },
        { "APP", "AcceleratorPedalPosition" },
        { "Fahrpedal", "AcceleratorPedalPosition" },

        // Engine Load
        { "EngineLoad", "EngineLoad" },
        { "CalculatedEngineLoad", "EngineLoad" },
        { "ENGINE_LOAD", "EngineLoad" },
        { "Motorlast", "EngineLoad" },

        // Fuel Rail Pressure
        { "FuelRailPressure", "FuelRailPressure" },
        { "RailPressure", "FuelRailPressure" },
        { "Kraftstoffdruck", "FuelRailPressure" },
        { "FUEL_RAIL_PRESSURE", "FuelRailPressure" },
        { "FuelRailPressure_Sim", "FuelRailPressure" },
        { "FRP", "FuelRailPressure" },

        // Boost Pressure
        { "BoostPressure", "BoostPressure" },
        { "Ladedruck", "BoostPressure" },
        { "BOOST_PRESSURE", "BoostPressure" },
        { "BoostBar", "BoostPressure" },

        // MAP
        { "MAP", "MAP" },
        { "IntakeManifoldPressure", "MAP" },
        { "Saugrohrdruck", "MAP" },

        // MAF
        { "MAF", "MAF" },
        { "MafAirFlowRate", "MAF" },
        { "Luftmasse", "MAF" },

        // Lambda
        { "Lambda", "Lambda" },
        { "Lambdawert", "Lambda" },

        // Oxygen Sensor
        { "O2", "O2" },
        { "OxygenSensors", "O2" },

        // Battery Voltage
        { "BatteryVoltage", "BatteryVoltage" },
        { "ControlModuleVoltage", "BatteryVoltage" },
        { "MODULE_VOLTAGE", "BatteryVoltage" },
        { "BattVolt", "BatteryVoltage" },
        { "UBatt", "BatteryVoltage" },

        // Fuel Rate
        { "FuelRate", "FuelRate" },
        { "EngineFuelRate", "FuelRate" },
        { "Kraftstoffverbrauch", "FuelRate" },

        // Oil Pressure
        { "OilPressure", "OilPressure" },
        { "Oeldruck", "OilPressure" },

        // Oil Temperature
        { "OilTemperature", "OilTemperature" },
        { "EngineOilTemperature", "OilTemperature" },
        { "Oeltemperatur", "OilTemperature" },

        // Intake Air Temperature
        { "IntakeAirTemperature", "IntakeAirTemperature" },
        { "IntakeAirTemp", "IntakeAirTemperature" },
        { "IAT", "IntakeAirTemperature" },
        { "Ansauglufttemperatur", "IntakeAirTemperature" },

        // Ambient Temperature
        { "AmbientTemperature", "AmbientTemperature" },
        { "AmbientAirTemp", "AmbientTemperature" },
        { "AmbientAirTemperature", "AmbientTemperature" },
        { "Aussentemperatur", "AmbientTemperature" },

        // DPF
        { "DPFPressure", "DPFPressure" },
        { "Differenzdruck_DPF", "DPFPressure" },
        { "DPFSootLoad", "DPFSootLoad" },
        { "Russbeladung_DPF", "DPFSootLoad" },

        // EGT
        { "EGT", "EGT" },
        { "ExhaustGasTemp", "EGT" },
        { "Abgastemperatur", "EGT" },

        // EGR
        { "EGRPosition", "EGRPosition" },
        { "AGR_Position", "EGRPosition" },

        // Injection Quantity
        { "InjectionQuantity", "InjectionQuantity" },
        { "Einspritzmenge", "InjectionQuantity" },

        // Torque
        { "EngineTorque", "EngineTorque" },
        { "EngineTorque_Sim", "EngineTorque" },
        { "RequestedTorque", "RequestedTorque" },
        { "ActualTorque", "ActualTorque" },
        { "Motormoment", "EngineTorque" },

        // Gear
        { "Gear", "Gear" },
        { "CurrentGear", "Gear" },
        { "Istgang", "Gear" },

        // Wheel Speeds
        { "WheelSpeedFL", "WheelSpeedFL" },
        { "WheelSpeedFR", "WheelSpeedFR" },
        { "WheelSpeedRL", "WheelSpeedRL" },
        { "WheelSpeedRR", "WheelSpeedRR" },

        // Steering & Brake
        { "SteeringAngle", "SteeringAngle" },
        { "Lenkwinkel", "SteeringAngle" },
        { "BrakePressure", "BrakePressure" },
        { "BrakePressure_Sim", "BrakePressure" }
    };

    private readonly ConcurrentDictionary<string, CanonicalSignalRecord> _records = new(StringComparer.OrdinalIgnoreCase);

    public event Action<CanonicalSignalRecord>? OnSignalUpdated;
    public event Action<IReadOnlyList<CanonicalSignalRecord>>? OnBatchUpdated;

    public UnifiedSignalRegistry()
    {
        // Pre-initialize canonical records so they are registered with default state
        foreach (var def in CanonicalDefinitions.Values)
        {
            _records[def.CanonicalId] = new CanonicalSignalRecord
            {
                CanonicalId = def.CanonicalId,
                DisplayName = def.DisplayName,
                Category = def.Category,
                Unit = def.Unit,
                Value = 0.0,
                RawSourceName = def.CanonicalId,
                SourceType = CanonicalSourceType.Dbc,
                LastUpdatedUtc = DateTime.MinValue // Initially stale
            };
        }
    }

    public bool TryResolveCanonicalName(string sourceName, out string canonicalId)
    {
        if (AliasMap.TryGetValue(sourceName, out var id))
        {
            canonicalId = id;
            return true;
        }

        // Try fuzzy stripping (_Sim, etc.)
        string clean = sourceName.Replace("_Sim", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (AliasMap.TryGetValue(clean, out id))
        {
            canonicalId = id;
            return true;
        }

        canonicalId = string.Empty;
        return false;
    }

    public void IngestSignal(string sourceName, double value, string unit, string sourceTransport = "CAN", CanonicalSourceType sourceType = CanonicalSourceType.Dbc)
    {
        if (!TryResolveCanonicalName(sourceName, out var canonicalId))
        {
            return; // Not an authoritative canonical signal, preserve separation
        }

        if (!_records.TryGetValue(canonicalId, out var rec))
        {
            if (CanonicalDefinitions.TryGetValue(canonicalId, out var def))
            {
                rec = new CanonicalSignalRecord
                {
                    CanonicalId = def.CanonicalId,
                    DisplayName = def.DisplayName,
                    Category = def.Category,
                    Unit = !string.IsNullOrEmpty(unit) ? unit : def.Unit
                };
                _records[canonicalId] = rec;
            }
            else return;
        }

        // Update record
        rec.Value = value;
        rec.RawSourceName = sourceName;
        rec.SourceType = sourceType;
        rec.UpdateCount++;
        rec.LastUpdatedUtc = DateTime.UtcNow;
        if (value < rec.MinObserved) rec.MinObserved = value;
        if (value > rec.MaxObserved) rec.MaxObserved = value;

        OnSignalUpdated?.Invoke(rec);
    }

    public void IngestDecodedSignals(IEnumerable<DecodedSignal> signals)
    {
        var updated = new List<CanonicalSignalRecord>();
        foreach (var sig in signals)
        {
            if (TryResolveCanonicalName(sig.SignalName, out var canonicalId))
            {
                if (_records.TryGetValue(canonicalId, out var rec))
                {
                    rec.Value = sig.EngineeringValue;
                    rec.RawSourceName = sig.SignalName;
                    rec.SourceType = CanonicalSourceType.Dbc;
                    rec.UpdateCount++;
                    rec.LastUpdatedUtc = DateTime.UtcNow;
                    if (sig.EngineeringValue < rec.MinObserved) rec.MinObserved = sig.EngineeringValue;
                    if (sig.EngineeringValue > rec.MaxObserved) rec.MaxObserved = sig.EngineeringValue;
                    updated.Add(rec);
                    OnSignalUpdated?.Invoke(rec);
                }
            }
        }

        if (updated.Count > 0)
        {
            OnBatchUpdated?.Invoke(updated);
        }
    }

    public CanonicalSignalRecord? GetSignal(string canonicalId)
    {
        _records.TryGetValue(canonicalId, out var rec);
        return rec;
    }

    public IReadOnlyList<CanonicalSignalRecord> GetAllSignals() => _records.Values.ToList();

    public IReadOnlyList<CanonicalSignalRecord> GetActiveSignals() => _records.Values.Where(r => r.IsActive).ToList();

    public void Clear()
    {
        foreach (var r in _records.Values)
        {
            r.Value = 0.0;
            r.UpdateCount = 0;
            r.MinObserved = double.MaxValue;
            r.MaxObserved = double.MinValue;
            r.LastUpdatedUtc = DateTime.MinValue;
        }
    }
}
