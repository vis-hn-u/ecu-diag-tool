using System;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.Diagnostics.Manufacturer;

public interface IManufacturerLiveDataService
{
    IReadOnlyList<ManufacturerLiveDataDefinition> GetAllDefinitions();
    IReadOnlyList<ManufacturerLiveDataDefinition> GetDefinitionsByOem(ManufacturerOem oem);
    ManufacturerLiveDataDefinition? GetDefinitionById(string id);
    DecodedManufacturerValue? DecodePayload(string definitionId, byte[] payload);
    DecodedManufacturerValue DecodeValue(ManufacturerLiveDataDefinition def, byte[] payload);
    IReadOnlyList<DecodedManufacturerValue> GetSimulatedSnapshot(ManufacturerOem oem, double rpm, double boostBar, double loadPct);
}

public class ManufacturerLiveDataService : IManufacturerLiveDataService
{
    private readonly List<ManufacturerLiveDataDefinition> _definitions = new();

    public ManufacturerLiveDataService()
    {
        InitializeDefinitions();
    }

    public IReadOnlyList<ManufacturerLiveDataDefinition> GetAllDefinitions() => _definitions;

    public IReadOnlyList<ManufacturerLiveDataDefinition> GetDefinitionsByOem(ManufacturerOem oem) =>
        _definitions.Where(d => d.Oem == oem || d.Oem == ManufacturerOem.Generic).ToList();

    public ManufacturerLiveDataDefinition? GetDefinitionById(string id) =>
        _definitions.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));

    public DecodedManufacturerValue? DecodePayload(string definitionId, byte[] payload)
    {
        var def = GetDefinitionById(definitionId);
        if (def == null || payload == null || payload.Length < def.ByteOffset + def.ByteLength)
            return null;

        return DecodeValue(def, payload);
    }

    public DecodedManufacturerValue DecodeValue(ManufacturerLiveDataDefinition def, byte[] payload)
    {
        double raw = 0;
        int offset = def.ByteOffset;

        if (def.ByteLength == 1)
        {
            raw = def.IsSigned ? (sbyte)payload[offset] : payload[offset];
        }
        else if (def.ByteLength == 2)
        {
            ushort u16 = def.IsBigEndian 
                ? (ushort)((payload[offset] << 8) | payload[offset + 1])
                : (ushort)(payload[offset] | (payload[offset + 1] << 8));
            raw = def.IsSigned ? (short)u16 : u16;
        }
        else if (def.ByteLength == 4)
        {
            uint u32 = def.IsBigEndian
                ? (uint)((payload[offset] << 24) | (payload[offset + 1] << 16) | (payload[offset + 2] << 8) | payload[offset + 3])
                : (uint)(payload[offset] | (payload[offset + 1] << 8) | (payload[offset + 2] << 16) | (payload[offset + 3] << 24));
            raw = def.IsSigned ? (int)u32 : u32;
        }

        double scaled = (raw * def.Factor) + def.Offset;
        string? stateLabel = null;
        if (def.StateLabels != null && def.StateLabels.TryGetValue((int)Math.Round(scaled), out var lbl))
        {
            stateLabel = lbl;
        }

        bool isWarning = (def.MinValue != def.MaxValue) && (scaled < def.MinValue || scaled > def.MaxValue);

        return new DecodedManufacturerValue
        {
            DefinitionId = def.Id,
            Oem = def.Oem,
            Name = def.Name,
            Category = def.Category,
            RawValue = raw,
            ScaledValue = Math.Round(scaled, 2),
            FormattedValue = stateLabel ?? scaled.ToString(def.DecimalFormat),
            Unit = def.Unit,
            StateLabel = stateLabel,
            TimestampUtc = DateTime.UtcNow,
            IsWarning = isWarning
        };
    }

    public IReadOnlyList<DecodedManufacturerValue> GetSimulatedSnapshot(ManufacturerOem oem, double rpm, double boostBar, double loadPct)
    {
        var list = new List<DecodedManufacturerValue>();
        var defs = GetDefinitionsByOem(oem);

        foreach (var def in defs)
        {
            double simVal = def.Id switch
            {
                "vag_cam_actual" => 12.0 + (loadPct * 0.15) + Math.Sin(rpm / 500.0) * 1.5,
                "vag_cam_spec" => 12.0 + (loadPct * 0.15),
                "vag_fuel_rail_act" => 40.0 + (boostBar * 60.0) + (loadPct * 0.8),
                "vag_fuel_rail_spec" => 40.0 + (boostBar * 60.0) + (loadPct * 0.8),
                "vag_wastegate_n75" => Math.Clamp(15.0 + (boostBar * 40.0), 5.0, 95.0),
                "vag_dsg_slip" => Math.Max(0, (loadPct > 80 ? 15.0 : 2.0) + (rpm > 5000 ? 10.0 : 0.0)),
                "vag_dsg_temp" => 78.5 + (loadPct * 0.1),
                "bmw_valvetronic_deg" => 35.0 + (loadPct * 1.4),
                "bmw_vanos_intake" => 85.0 + (rpm / 100.0),
                "bmw_charge_air_temp" => 32.0 + (boostBar * 12.0),
                "ford_boost_desired" => 101.3 + (boostBar * 100.0),
                "ford_cylinder_head_temp" => 92.0 + (loadPct * 0.08),
                "gm_knock_retard_cyl1" => loadPct > 85 && boostBar > 1.2 ? 1.5 : 0.0,
                "gm_commanded_lambda" => loadPct > 70 ? 0.85 : 1.00,
                "toyota_vvti_intake" => 10.0 + (rpm / 250.0),
                "merc_dpf_diff_press" => 12.5 + (loadPct * 0.15),
                _ => (def.MinValue + def.MaxValue) / 2.0
            };

            list.Add(new DecodedManufacturerValue
            {
                DefinitionId = def.Id,
                Oem = def.Oem,
                Name = def.Name,
                Category = def.Category,
                RawValue = simVal,
                ScaledValue = Math.Round(simVal, 2),
                FormattedValue = simVal.ToString(def.DecimalFormat),
                Unit = def.Unit,
                TimestampUtc = DateTime.UtcNow,
                IsWarning = (def.MinValue != def.MaxValue) && (simVal < def.MinValue || simVal > def.MaxValue)
            });
        }

        return list;
    }

    private void InitializeDefinitions()
    {
        // VAG (Volkswagen / Audi / Seat / Skoda)
        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "vag_cam_actual",
            Oem = ManufacturerOem.VAG,
            Category = "Engine Timing",
            Name = "Camshaft Intake Angle (Actual)",
            Description = "Variable valve timing intake camshaft position angle",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0x2211,
            ByteOffset = 0,
            ByteLength = 2,
            Factor = 0.5,
            Offset = -40.0,
            Unit = "°KW",
            MinValue = -10.0,
            MaxValue = 60.0
        });

        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "vag_cam_spec",
            Oem = ManufacturerOem.VAG,
            Category = "Engine Timing",
            Name = "Camshaft Intake Angle (Specified)",
            Description = "Target variable valve timing intake camshaft position",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0x2212,
            ByteOffset = 0,
            ByteLength = 2,
            Factor = 0.5,
            Offset = -40.0,
            Unit = "°KW",
            MinValue = -10.0,
            MaxValue = 60.0
        });

        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "vag_fuel_rail_act",
            Oem = ManufacturerOem.VAG,
            Category = "Fuel High Pressure",
            Name = "Fuel Rail Pressure (Actual)",
            Description = "Common rail or direct injection fuel rail pressure sensor readout",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0xF40C,
            ByteOffset = 0,
            ByteLength = 2,
            Factor = 0.1,
            Offset = 0.0,
            Unit = "bar",
            MinValue = 20.0,
            MaxValue = 220.0
        });

        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "vag_fuel_rail_spec",
            Oem = ManufacturerOem.VAG,
            Category = "Fuel High Pressure",
            Name = "Fuel Rail Pressure (Specified)",
            Description = "Target high pressure fuel rail setpoint computed by ECM",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0xF40B,
            ByteOffset = 0,
            ByteLength = 2,
            Factor = 0.1,
            Offset = 0.0,
            Unit = "bar",
            MinValue = 20.0,
            MaxValue = 220.0
        });

        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "vag_wastegate_n75",
            Oem = ManufacturerOem.VAG,
            Category = "Turbo Boost",
            Name = "Wastegate Solenoid N75 Duty",
            Description = "Pulse width duty cycle of turbocharger boost pressure control valve",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0x11B0,
            ByteOffset = 0,
            ByteLength = 1,
            Factor = 0.39215686, // 100 / 255
            Offset = 0.0,
            Unit = "%",
            MinValue = 0.0,
            MaxValue = 100.0
        });

        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "vag_dsg_slip",
            Oem = ManufacturerOem.VAG,
            Category = "Transmission",
            Name = "DSG Clutch 1 Slip Speed",
            Description = "Speed differential across dual clutch pack 1",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0x2401,
            ByteOffset = 0,
            ByteLength = 2,
            Factor = 1.0,
            Offset = 0.0,
            Unit = "RPM",
            MinValue = 0.0,
            MaxValue = 250.0
        });

        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "vag_dsg_temp",
            Oem = ManufacturerOem.VAG,
            Category = "Transmission",
            Name = "DSG Fluid Temperature",
            Description = "Mechatronic transmission fluid temperature sensor",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0x2405,
            ByteOffset = 0,
            ByteLength = 1,
            Factor = 1.0,
            Offset = -40.0,
            Unit = "°C",
            MinValue = -10.0,
            MaxValue = 135.0
        });

        // BMW
        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "bmw_valvetronic_deg",
            Oem = ManufacturerOem.BMW,
            Category = "Engine Induction",
            Name = "Valvetronic Eccentric Shaft Angle",
            Description = "Continuous valve lift control eccentric shaft position angle",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0x3101,
            ByteOffset = 0,
            ByteLength = 2,
            Factor = 0.1,
            Offset = 0.0,
            Unit = "°",
            MinValue = 0.0,
            MaxValue = 180.0
        });

        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "bmw_vanos_intake",
            Oem = ManufacturerOem.BMW,
            Category = "Engine Timing",
            Name = "VANOS Intake Position",
            Description = "Variable camshaft spread angle intake",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0x3105,
            ByteOffset = 0,
            ByteLength = 2,
            Factor = 0.1,
            Offset = 0.0,
            Unit = "°CRK",
            MinValue = 70.0,
            MaxValue = 140.0
        });

        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "bmw_charge_air_temp",
            Oem = ManufacturerOem.BMW,
            Category = "Thermals",
            Name = "Charge-Air Intercooler Out Temp",
            Description = "Air charge temperature after intercooler",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0x3302,
            ByteOffset = 0,
            ByteLength = 1,
            Factor = 1.0,
            Offset = -40.0,
            Unit = "°C",
            MinValue = -20.0,
            MaxValue = 80.0
        });

        // Ford
        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "ford_boost_desired",
            Oem = ManufacturerOem.Ford,
            Category = "Turbo Boost",
            Name = "Manifold Pressure Desired",
            Description = "EcoBoost ECM calculated target MAP",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0x0359,
            ByteOffset = 0,
            ByteLength = 2,
            Factor = 0.03125,
            Offset = 0.0,
            Unit = "kPa",
            MinValue = 20.0,
            MaxValue = 300.0
        });

        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "ford_cylinder_head_temp",
            Oem = ManufacturerOem.Ford,
            Category = "Thermals",
            Name = "Cylinder Head Temp (CHT)",
            Description = "Cylinder head metal temperature sensor",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0x1624,
            ByteOffset = 0,
            ByteLength = 2,
            Factor = 0.1,
            Offset = -40.0,
            Unit = "°C",
            MinValue = -20.0,
            MaxValue = 130.0
        });

        // GM
        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "gm_knock_retard_cyl1",
            Oem = ManufacturerOem.GM,
            Category = "Ignition Protection",
            Name = "Knock Retard Cylinder 1",
            Description = "Dynamic spark timing retard triggered by knock acoustic sensor",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0x11A1,
            ByteOffset = 0,
            ByteLength = 1,
            Factor = 0.1,
            Offset = 0.0,
            Unit = "°",
            MinValue = 0.0,
            MaxValue = 12.0
        });

        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "gm_commanded_lambda",
            Oem = ManufacturerOem.GM,
            Category = "Fuel Metering",
            Name = "Commanded Equivalence Ratio (Lambda)",
            Description = "Target air-fuel equivalence ratio commanded by ECM",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0x1240,
            ByteOffset = 0,
            ByteLength = 2,
            Factor = 0.000030517578,
            Offset = 0.0,
            Unit = "λ",
            MinValue = 0.65,
            MaxValue = 1.30
        });

        // Toyota
        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "toyota_vvti_intake",
            Oem = ManufacturerOem.Toyota,
            Category = "Engine Timing",
            Name = "VVT-i Intake Cam Advance",
            Description = "Toyota Variable Valve Timing with intelligence advance angle",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0x011A,
            ByteOffset = 0,
            ByteLength = 2,
            Factor = 0.5,
            Offset = 0.0,
            Unit = "°",
            MinValue = 0.0,
            MaxValue = 60.0
        });

        // Mercedes
        _definitions.Add(new ManufacturerLiveDataDefinition
        {
            Id = "merc_dpf_diff_press",
            Oem = ManufacturerOem.Mercedes,
            Category = "Emissions",
            Name = "DPF Differential Pressure",
            Description = "Exhaust gas pressure drop across Diesel Particulate Filter",
            IdentifierType = LiveDataIdentifierType.UdsDid,
            Identifier = 0x2502,
            ByteOffset = 0,
            ByteLength = 2,
            Factor = 1.0,
            Offset = 0.0,
            Unit = "hPa",
            MinValue = 0.0,
            MaxValue = 350.0
        });
    }
}
