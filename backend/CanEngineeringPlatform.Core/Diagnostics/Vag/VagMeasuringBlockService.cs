using System;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.Diagnostics.Vag;

public interface IVagMeasuringBlockService
{
    IReadOnlyList<VagMeasuringGroup> GetAllSupportedGroups();
    VagMeasuringGroup? GetGroup(int groupNumber);
    VagMeasuringGroup DecodeGroupPayload(int groupNumber, byte[] payload);
    (double value, string formatted, string unit) DecodeFormula(VagFormulaType formula, byte rawA, byte rawB);
    VagMeasuringGroup GetSimulatedGroup(int groupNumber, double rpm, double boostBar, double loadPct);
}

public class VagMeasuringBlockService : IVagMeasuringBlockService
{
    private readonly Dictionary<int, VagMeasuringGroup> _groupTemplates = new();

    public VagMeasuringBlockService()
    {
        InitializeTemplates();
    }

    public IReadOnlyList<VagMeasuringGroup> GetAllSupportedGroups() => _groupTemplates.Values.OrderBy(g => g.GroupNumber).ToList();

    public VagMeasuringGroup? GetGroup(int groupNumber)
    {
        _groupTemplates.TryGetValue(groupNumber, out var g);
        return g != null ? CloneGroup(g) : null;
    }

    public (double value, string formatted, string unit) DecodeFormula(VagFormulaType formula, byte rawA, byte rawB)
    {
        switch (formula)
        {
            case VagFormulaType.Rpm:
                double rpm = 0.2 * rawA * rawB;
                return (rpm, Math.Round(rpm).ToString("F0"), "RPM");

            case VagFormulaType.AbsoluteLoad:
                double load = rawA * 100.0 / 255.0;
                return (load, load.ToString("F1"), "%");

            case VagFormulaType.ThrottleAngle:
                double throttle = rawA * 0.39215686;
                return (throttle, throttle.ToString("F1"), "%");

            case VagFormulaType.Temperature:
                double temp = 0.75 * rawA - 48.0;
                return (temp, temp.ToString("F1"), "°C");

            case VagFormulaType.Voltage:
                double v1 = (rawA * 256 + rawB) * 0.001;
                return (v1, v1.ToString("F2"), "V");

            case VagFormulaType.Speed:
                double speed = 0.01 * rawA * rawB;
                return (speed, Math.Round(speed).ToString("F0"), "km/h");

            case VagFormulaType.TimingAngle:
                double timing = 0.75 * (rawA - 64.0);
                return (timing, timing.ToString("F1"), "°BTDC");

            case VagFormulaType.VoltageFine:
                double v2 = rawA * 0.091;
                return (v2, v2.ToString("F2"), "V");

            case VagFormulaType.PressureMbar:
                double mbar = 0.1 * rawA * rawB;
                return (mbar, Math.Round(mbar).ToString("F0"), "mbar");

            case VagFormulaType.DutyCycle:
                double duty = rawA * 100.0 / 255.0;
                return (duty, duty.ToString("F1"), "%");

            case VagFormulaType.LambdaRatio:
                double lambda = 0.001 * rawA * rawB;
                return (lambda, lambda.ToString("F3"), "λ");

            case VagFormulaType.MassAirFlow:
                double maf = 0.01 * rawA * rawB;
                return (maf, maf.ToString("F2"), "g/s");

            case VagFormulaType.Counter:
                double count = rawA * 256 + rawB;
                return (count, count.ToString("F0"), "Count");

            default:
                double raw = rawA * 256 + rawB;
                return (raw, raw.ToString("F0"), "");
        }
    }

    public VagMeasuringGroup DecodeGroupPayload(int groupNumber, byte[] payload)
    {
        var group = GetGroup(groupNumber) ?? new VagMeasuringGroup
        {
            GroupNumber = groupNumber,
            Name = $"Measuring Block Group {groupNumber:D3}",
            Zones = new List<VagMeasuringBlockZone>
            {
                new() { ZoneNumber = 1, Label = "Field 1" },
                new() { ZoneNumber = 2, Label = "Field 2" },
                new() { ZoneNumber = 3, Label = "Field 3" },
                new() { ZoneNumber = 4, Label = "Field 4" }
            }
        };

        // Standard KWP1281 / KWP2000 Group format: 4 zones x 3 bytes = 12 bytes
        // Each zone: [FormulaType] [RawA] [RawB]
        if (payload != null && payload.Length >= 12)
        {
            for (int z = 0; z < 4; z++)
            {
                int offset = z * 3;
                byte formulaByte = payload[offset];
                byte rawA = payload[offset + 1];
                byte rawB = payload[offset + 2];

                var (val, formatted, unit) = DecodeFormula((VagFormulaType)formulaByte, rawA, rawB);
                if (z < group.Zones.Count)
                {
                    group.Zones[z].RawA = rawA;
                    group.Zones[z].RawB = rawB;
                    group.Zones[z].FormulaType = (VagFormulaType)formulaByte;
                    group.Zones[z].Value = Math.Round(val, 2);
                    group.Zones[z].FormattedValue = formatted;
                    if (!string.IsNullOrEmpty(unit)) group.Zones[z].Unit = unit;
                }
            }
        }

        group.LastUpdatedUtc = DateTime.UtcNow;
        return group;
    }

    public VagMeasuringGroup GetSimulatedGroup(int groupNumber, double rpm, double boostBar, double loadPct)
    {
        var group = GetGroup(groupNumber) ?? new VagMeasuringGroup { GroupNumber = groupNumber, Name = $"Group {groupNumber:D3}" };

        switch (groupNumber)
        {
            case 1: // Group 001: Idle & Base
                SetZone(group, 1, rpm, $"{Math.Round(rpm)}", "RPM");
                SetZone(group, 2, 88.5, "88.5", "°C");
                SetZone(group, 3, (loadPct > 80 ? 4.5 : 0.2), $"{(loadPct > 80 ? "+4.5" : "+0.2")}", "%");
                SetZone(group, 4, 1.0, "01000000", "State");
                break;

            case 2: // Group 002: Load & Airflow
                SetZone(group, 1, rpm, $"{Math.Round(rpm)}", "RPM");
                SetZone(group, 2, loadPct, $"{loadPct:F1}", "%");
                SetZone(group, 3, 2.1 + (loadPct * 0.12), $"{(2.1 + (loadPct * 0.12)):F2}", "ms");
                SetZone(group, 4, 3.5 + (rpm * loadPct * 0.00035), $"{(3.5 + (rpm * loadPct * 0.00035)):F1}", "g/s");
                break;

            case 3: // Group 003: MAF & EGR
                SetZone(group, 1, rpm, $"{Math.Round(rpm)}", "RPM");
                SetZone(group, 2, 4.0 + (rpm * loadPct * 0.00034), $"{(4.0 + (rpm * loadPct * 0.00034)):F1}", "g/s");
                SetZone(group, 3, 3.8 + (rpm * loadPct * 0.00035), $"{(3.8 + (rpm * loadPct * 0.00035)):F1}", "g/s");
                SetZone(group, 4, loadPct > 50 ? 0.0 : 45.0, loadPct > 50 ? "0.0" : "45.0", "%");
                break;

            case 15: // Group 015: Misfire Cyl 1-3
                SetZone(group, 1, 0.0, "0", "Count");
                SetZone(group, 2, 0.0, "0", "Count");
                SetZone(group, 3, 0.0, "0", "Count");
                SetZone(group, 4, 1.0, "Enabled", "State");
                break;

            case 20: // Group 020: Knock Retard Cyl 1-4
                double retard1 = (loadPct > 85 && boostBar > 1.2) ? 1.5 : 0.0;
                double retard2 = (loadPct > 85 && boostBar > 1.2) ? 0.8 : 0.0;
                SetZone(group, 1, retard1, $"{retard1:F1}", "°KW");
                SetZone(group, 2, retard2, $"{retard2:F1}", "°KW");
                SetZone(group, 3, 0.0, "0.0", "°KW");
                SetZone(group, 4, 0.0, "0.0", "°KW");
                break;

            case 31: // Group 031: Lambda
                double actLam = loadPct > 75 ? 0.82 : 1.00;
                double specLam = loadPct > 75 ? 0.82 : 1.00;
                SetZone(group, 1, actLam, $"{actLam:F3}", "λ");
                SetZone(group, 2, specLam, $"{specLam:F3}", "λ");
                SetZone(group, 3, 14.1, "14.1", "V");
                SetZone(group, 4, 1.0, "Closed Loop", "");
                break;

            case 115: // Group 115: Boost Specified vs Actual
                double specMbar = 1013.0 + (boostBar * 1000.0);
                double actMbar = 1013.0 + (boostBar * 1000.0) + (Math.Sin(rpm / 400.0) * 25.0);
                SetZone(group, 1, rpm, $"{Math.Round(rpm)}", "RPM");
                SetZone(group, 2, loadPct, $"{loadPct:F1}", "%");
                SetZone(group, 3, specMbar, $"{Math.Round(specMbar)}", "mbar");
                SetZone(group, 4, actMbar, $"{Math.Round(actMbar)}", "mbar");
                break;

            case 118: // Group 118: Intake Temp & Wastegate N75
                double n75Duty = Math.Clamp(15.0 + (boostBar * 45.0), 5.0, 95.0);
                SetZone(group, 1, rpm, $"{Math.Round(rpm)}", "RPM");
                SetZone(group, 2, 28.5 + (boostBar * 10.0), $"{(28.5 + (boostBar * 10.0)):F1}", "°C");
                SetZone(group, 3, n75Duty, $"{n75Duty:F1}", "%");
                SetZone(group, 4, 1013.0 + (boostBar * 1000.0), $"{Math.Round(1013.0 + (boostBar * 1000.0))}", "mbar");
                break;

            case 120: // Group 120: Traction & Torque
                double torque = 120.0 + (boostBar * 180.0) + (loadPct * 0.5);
                SetZone(group, 1, rpm, $"{Math.Round(rpm)}", "RPM");
                SetZone(group, 2, torque, $"{Math.Round(torque)}", "Nm");
                SetZone(group, 3, torque * 0.98, $"{Math.Round(torque * 0.98)}", "Nm");
                SetZone(group, 4, 0.0, "No Interv.", "");
                break;

            default:
                SetZone(group, 1, rpm, $"{Math.Round(rpm)}", "RPM");
                SetZone(group, 2, loadPct, $"{loadPct:F1}", "%");
                SetZone(group, 3, 0.0, "0.0", "");
                SetZone(group, 4, 0.0, "0.0", "");
                break;
        }

        group.LastUpdatedUtc = DateTime.UtcNow;
        return group;
    }

    private static void SetZone(VagMeasuringGroup g, int zoneNum, double val, string formatted, string unit)
    {
        var z = g.Zones.FirstOrDefault(x => x.ZoneNumber == zoneNum);
        if (z != null)
        {
            z.Value = Math.Round(val, 2);
            z.FormattedValue = formatted;
            if (!string.IsNullOrEmpty(unit)) z.Unit = unit;
        }
    }

    private static VagMeasuringGroup CloneGroup(VagMeasuringGroup src)
    {
        return new VagMeasuringGroup
        {
            GroupNumber = src.GroupNumber,
            Name = src.Name,
            Description = src.Description,
            EcuSubsystem = src.EcuSubsystem,
            LastUpdatedUtc = src.LastUpdatedUtc,
            Zones = src.Zones.Select(z => new VagMeasuringBlockZone
            {
                ZoneNumber = z.ZoneNumber,
                Label = z.Label,
                FormulaType = z.FormulaType,
                RawA = z.RawA,
                RawB = z.RawB,
                Value = z.Value,
                FormattedValue = z.FormattedValue,
                Unit = z.Unit,
                MinNominal = z.MinNominal,
                MaxNominal = z.MaxNominal
            }).ToList()
        };
    }

    private void InitializeTemplates()
    {
        _groupTemplates[1] = new VagMeasuringGroup
        {
            GroupNumber = 1,
            Name = "Base Engine Data & Idle",
            Description = "Engine speed, coolant temperature, lambda regulator, and basic settings condition",
            Zones = new List<VagMeasuringBlockZone>
            {
                new() { ZoneNumber = 1, Label = "Engine Speed", FormulaType = VagFormulaType.Rpm, Unit = "RPM", MinNominal = 650, MaxNominal = 900 },
                new() { ZoneNumber = 2, Label = "Coolant Temp", FormulaType = VagFormulaType.Temperature, Unit = "°C", MinNominal = 80, MaxNominal = 105 },
                new() { ZoneNumber = 3, Label = "Lambda Control", FormulaType = VagFormulaType.DutyCycle, Unit = "%", MinNominal = -10, MaxNominal = 10 },
                new() { ZoneNumber = 4, Label = "Basic Settings", FormulaType = VagFormulaType.BinaryStatus, Unit = "State" }
            }
        };

        _groupTemplates[2] = new VagMeasuringGroup
        {
            GroupNumber = 2,
            Name = "Engine Load & Mass Air Flow",
            Description = "Engine speed, calculated engine load, fuel injection duration, and mass air flow",
            Zones = new List<VagMeasuringBlockZone>
            {
                new() { ZoneNumber = 1, Label = "Engine Speed", FormulaType = VagFormulaType.Rpm, Unit = "RPM" },
                new() { ZoneNumber = 2, Label = "Engine Load", FormulaType = VagFormulaType.AbsoluteLoad, Unit = "%", MinNominal = 10, MaxNominal = 100 },
                new() { ZoneNumber = 3, Label = "Injection Time", FormulaType = VagFormulaType.Custom, Unit = "ms", MinNominal = 1.0, MaxNominal = 25.0 },
                new() { ZoneNumber = 4, Label = "Mass Air Flow", FormulaType = VagFormulaType.MassAirFlow, Unit = "g/s", MinNominal = 2.0, MaxNominal = 300.0 }
            }
        };

        _groupTemplates[3] = new VagMeasuringGroup
        {
            GroupNumber = 3,
            Name = "Air Mass & EGR Regulation",
            Description = "Specified vs actual air mass flow rate and EGR duty cycle",
            Zones = new List<VagMeasuringBlockZone>
            {
                new() { ZoneNumber = 1, Label = "Engine Speed", FormulaType = VagFormulaType.Rpm, Unit = "RPM" },
                new() { ZoneNumber = 2, Label = "MAF Specified", FormulaType = VagFormulaType.MassAirFlow, Unit = "g/s" },
                new() { ZoneNumber = 3, Label = "MAF Actual", FormulaType = VagFormulaType.MassAirFlow, Unit = "g/s" },
                new() { ZoneNumber = 4, Label = "EGR Duty Cycle", FormulaType = VagFormulaType.DutyCycle, Unit = "%", MinNominal = 0, MaxNominal = 100 }
            }
        };

        _groupTemplates[15] = new VagMeasuringGroup
        {
            GroupNumber = 15,
            Name = "Misfire Recognition Cylinders 1-3",
            Description = "Individual combustion misfire counts for cylinders 1, 2, and 3",
            Zones = new List<VagMeasuringBlockZone>
            {
                new() { ZoneNumber = 1, Label = "Misfires Cyl 1", FormulaType = VagFormulaType.Counter, Unit = "Count", MinNominal = 0, MaxNominal = 0 },
                new() { ZoneNumber = 2, Label = "Misfires Cyl 2", FormulaType = VagFormulaType.Counter, Unit = "Count", MinNominal = 0, MaxNominal = 0 },
                new() { ZoneNumber = 3, Label = "Misfires Cyl 3", FormulaType = VagFormulaType.Counter, Unit = "Count", MinNominal = 0, MaxNominal = 0 },
                new() { ZoneNumber = 4, Label = "Recognition State", FormulaType = VagFormulaType.BinaryStatus, Unit = "State" }
            }
        };

        _groupTemplates[20] = new VagMeasuringGroup
        {
            GroupNumber = 20,
            Name = "Knock Control Timing Retard Cyl 1-4",
            Description = "Dynamic individual cylinder ignition retard angle from knock regulation",
            Zones = new List<VagMeasuringBlockZone>
            {
                new() { ZoneNumber = 1, Label = "Timing Retard Cyl 1", FormulaType = VagFormulaType.TimingAngle, Unit = "°KW", MinNominal = 0, MaxNominal = 3.0 },
                new() { ZoneNumber = 2, Label = "Timing Retard Cyl 2", FormulaType = VagFormulaType.TimingAngle, Unit = "°KW", MinNominal = 0, MaxNominal = 3.0 },
                new() { ZoneNumber = 3, Label = "Timing Retard Cyl 3", FormulaType = VagFormulaType.TimingAngle, Unit = "°KW", MinNominal = 0, MaxNominal = 3.0 },
                new() { ZoneNumber = 4, Label = "Timing Retard Cyl 4", FormulaType = VagFormulaType.TimingAngle, Unit = "°KW", MinNominal = 0, MaxNominal = 3.0 }
            }
        };

        _groupTemplates[31] = new VagMeasuringGroup
        {
            GroupNumber = 31,
            Name = "Lambda Control Bank 1",
            Description = "Pre-catalyst wideband oxygen sensor actual lambda vs ECM target lambda",
            Zones = new List<VagMeasuringBlockZone>
            {
                new() { ZoneNumber = 1, Label = "Lambda Actual", FormulaType = VagFormulaType.LambdaRatio, Unit = "λ", MinNominal = 0.75, MaxNominal = 1.15 },
                new() { ZoneNumber = 2, Label = "Lambda Specified", FormulaType = VagFormulaType.LambdaRatio, Unit = "λ", MinNominal = 0.75, MaxNominal = 1.05 },
                new() { ZoneNumber = 3, Label = "Battery Voltage", FormulaType = VagFormulaType.VoltageFine, Unit = "V", MinNominal = 12.0, MaxNominal = 14.8 },
                new() { ZoneNumber = 4, Label = "Loop State", FormulaType = VagFormulaType.BinaryStatus, Unit = "" }
            }
        };

        _groupTemplates[115] = new VagMeasuringGroup
        {
            GroupNumber = 115,
            Name = "Turbocharger Boost Pressure Control",
            Description = "Specified vs actual manifold absolute pressure (MAP) across engine RPM and load",
            Zones = new List<VagMeasuringBlockZone>
            {
                new() { ZoneNumber = 1, Label = "Engine Speed", FormulaType = VagFormulaType.Rpm, Unit = "RPM" },
                new() { ZoneNumber = 2, Label = "Engine Load", FormulaType = VagFormulaType.AbsoluteLoad, Unit = "%" },
                new() { ZoneNumber = 3, Label = "Boost Specified", FormulaType = VagFormulaType.PressureMbar, Unit = "mbar", MinNominal = 950, MaxNominal = 2600 },
                new() { ZoneNumber = 4, Label = "Boost Actual", FormulaType = VagFormulaType.PressureMbar, Unit = "mbar", MinNominal = 950, MaxNominal = 2600 }
            }
        };

        _groupTemplates[118] = new VagMeasuringGroup
        {
            GroupNumber = 118,
            Name = "Charge Air Temperature & N75 Wastegate",
            Description = "Intake charge air temperature after intercooler, wastegate N75 duty cycle, and boost",
            Zones = new List<VagMeasuringBlockZone>
            {
                new() { ZoneNumber = 1, Label = "Engine Speed", FormulaType = VagFormulaType.Rpm, Unit = "RPM" },
                new() { ZoneNumber = 2, Label = "Intake Air Temp", FormulaType = VagFormulaType.Temperature, Unit = "°C", MinNominal = -10, MaxNominal = 70 },
                new() { ZoneNumber = 3, Label = "Wastegate N75 Duty", FormulaType = VagFormulaType.DutyCycle, Unit = "%", MinNominal = 0, MaxNominal = 95 },
                new() { ZoneNumber = 4, Label = "Boost Actual", FormulaType = VagFormulaType.PressureMbar, Unit = "mbar" }
            }
        };

        _groupTemplates[120] = new VagMeasuringGroup
        {
            GroupNumber = 120,
            Name = "ASR / Traction Control & Torque",
            Description = "Engine torque request and delivered actual flywheel torque",
            Zones = new List<VagMeasuringBlockZone>
            {
                new() { ZoneNumber = 1, Label = "Engine Speed", FormulaType = VagFormulaType.Rpm, Unit = "RPM" },
                new() { ZoneNumber = 2, Label = "Specified Torque", FormulaType = VagFormulaType.Custom, Unit = "Nm" },
                new() { ZoneNumber = 3, Label = "Actual Torque", FormulaType = VagFormulaType.Custom, Unit = "Nm" },
                new() { ZoneNumber = 4, Label = "ASR Intervention", FormulaType = VagFormulaType.BinaryStatus, Unit = "" }
            }
        };
    }
}
