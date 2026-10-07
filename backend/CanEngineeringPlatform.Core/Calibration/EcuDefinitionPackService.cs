using System;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.Calibration;

public interface IEcuDefinitionPackService
{
    IReadOnlyList<EcuDefinitionPack> GetAllPacks();
    EcuDefinitionPack? GetPackById(string packId);
    EcuDefinitionPack? MatchPack(string? hwPartNumber, string? swVersion, string? calibrationId);
    ReadonlyEcuMap? GetMap(string packId, string mapId);
    OperatingPointTracer CalculateOperatingPoint(string packId, string mapId, double rpm, double load);
}

public class EcuDefinitionPackService : IEcuDefinitionPackService
{
    private readonly List<EcuDefinitionPack> _packs = new();

    public EcuDefinitionPackService()
    {
        InitializePacks();
    }

    public IReadOnlyList<EcuDefinitionPack> GetAllPacks() => _packs;

    public EcuDefinitionPack? GetPackById(string packId) =>
        _packs.FirstOrDefault(p => string.Equals(p.Id, packId, StringComparison.OrdinalIgnoreCase));

    public EcuDefinitionPack? MatchPack(string? hwPartNumber, string? swVersion, string? calibrationId)
    {
        if (string.IsNullOrWhiteSpace(hwPartNumber) && string.IsNullOrWhiteSpace(swVersion) && string.IsNullOrWhiteSpace(calibrationId))
        {
            return _packs.FirstOrDefault();
        }

        return _packs.FirstOrDefault(p =>
            (!string.IsNullOrWhiteSpace(hwPartNumber) && p.HardwarePartNumber.Contains(hwPartNumber, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(swVersion) && p.SoftwareVersion.Contains(swVersion, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(calibrationId) && p.CalibrationId.Contains(calibrationId, StringComparison.OrdinalIgnoreCase))
        ) ?? _packs.FirstOrDefault();
    }

    public ReadonlyEcuMap? GetMap(string packId, string mapId)
    {
        var pack = GetPackById(packId);
        return pack?.Maps.FirstOrDefault(m => string.Equals(m.MapId, mapId, StringComparison.OrdinalIgnoreCase));
    }

    public OperatingPointTracer CalculateOperatingPoint(string packId, string mapId, double rpm, double load)
    {
        var map = GetMap(packId, mapId);
        var tracer = new OperatingPointTracer
        {
            ActiveMapId = mapId,
            CurrentRpm = rpm,
            CurrentLoad = load
        };

        if (map == null || map.XAxis.Breakpoints.Length == 0)
        {
            return tracer;
        }

        // Find nearest X index (RPM)
        int bestX = 0;
        double minXDiff = double.MaxValue;
        for (int i = 0; i < map.XAxis.Breakpoints.Length; i++)
        {
            double diff = Math.Abs(map.XAxis.Breakpoints[i] - rpm);
            if (diff < minXDiff)
            {
                minXDiff = diff;
                bestX = i;
            }
        }
        tracer.NearestXIndex = bestX;

        // Find nearest Y index (Load)
        int bestY = 0;
        if (map.YAxis != null && map.YAxis.Breakpoints.Length > 0)
        {
            double minYDiff = double.MaxValue;
            for (int j = 0; j < map.YAxis.Breakpoints.Length; j++)
            {
                double diff = Math.Abs(map.YAxis.Breakpoints[j] - load);
                if (diff < minYDiff)
                {
                    minYDiff = diff;
                    bestY = j;
                }
            }
            tracer.NearestYIndex = bestY;
        }

        // Bilinear interpolation for Z value
        if (map.ZData.Length > bestY && map.ZData[bestY].Length > bestX)
        {
            tracer.InterpolatedZValue = map.ZData[bestY][bestX];
        }

        return tracer;
    }

    private void InitializePacks()
    {
        // 0. Magneti Marelli MJD (Maruti Suzuki Swift Diesel 1.3 DDiS)
        var marelliMjd = new EcuDefinitionPack
        {
            Id = "marelli_mjd_swift_ddis",
            Name = "Magneti Marelli MJD (Swift Diesel 1.3 DDiS)",
            TargetPlatform = "Maruti Suzuki Swift / Dzire / Ertiga 1.3L DDiS MultiJet",
            HardwarePartNumber = "MJD 6F3.S1 / 8F3",
            SoftwareVersion = "3422S167",
            CalibrationId = "SWIFT_1.3_DDiS",
            Author = "Magneti Marelli MJD DAMOS / A2L Reference",
            Version = "1.0",
            SupportedProtocols = new List<string> { "UDS (ISO 14229)", "KWP2000", "CAN ISO-TP" }
        };

        // Common Rail Pressure Target Map (RPM x Injected Quantity)
        double[] mjdRailRpm = new double[] { 800, 1200, 1600, 2000, 2500, 3000, 3500, 4000, 4500 };
        double[] mjdRailIq = new double[] { 5, 10, 15, 20, 30, 40, 50, 60 };
        double[][] mjdRailData = new double[mjdRailIq.Length][];
        for (int y = 0; y < mjdRailIq.Length; y++)
        {
            mjdRailData[y] = new double[mjdRailRpm.Length];
            for (int x = 0; x < mjdRailRpm.Length; x++)
            {
                // MultiJet rail pressure: 250-300 bar idle up to 1400-1600 bar under full load
                double baseBar = 250.0 + (mjdRailRpm[x] * 0.18);
                double loadBar = mjdRailIq[y] * 14.5;
                mjdRailData[y][x] = Math.Round(Math.Min(1600.0, Math.Max(250.0, baseBar + loadBar)), 0);
            }
        }

        marelliMjd.Maps.Add(new ReadonlyEcuMap
        {
            MapId = "MJD_RailPressure",
            Name = "Rail Pressure Target (Common Rail)",
            Category = MapCategory.Fuel,
            Description = "Desired common rail fuel pressure across engine speed and injected fuel volume",
            Dimension = MapDimension.TwoDimensional,
            MemoryAddressHex = "0x0012C400",
            XAxis = new ReadonlyMapAxis { Name = "Engine Speed", Unit = "RPM", Breakpoints = mjdRailRpm },
            YAxis = new ReadonlyMapAxis { Name = "Injected Quantity", Unit = "mm³/stk", Breakpoints = mjdRailIq },
            ZData = mjdRailData,
            ZUnit = "bar",
            MinValue = 250.0,
            MaxValue = 1600.0,
            DecimalPrecision = 0,
            HeatmapPalette = "turbo"
        });

        // Boost Target Map (RPM x Injected Quantity)
        double[] mjdBoostRpm = new double[] { 1000, 1500, 2000, 2500, 3000, 3500, 4000, 4500 };
        double[] mjdBoostIq = new double[] { 10, 20, 30, 40, 50, 60 };
        double[][] mjdBoostData = new double[mjdBoostIq.Length][];
        for (int y = 0; y < mjdBoostIq.Length; y++)
        {
            mjdBoostData[y] = new double[mjdBoostRpm.Length];
            for (int x = 0; x < mjdBoostRpm.Length; x++)
            {
                double targetMbar = 1013.0 + (mjdBoostIq[y] > 15 ? (mjdBoostIq[y] - 15) * 26.5 : 0.0);
                if (mjdBoostRpm[x] > 4000) targetMbar -= (mjdBoostRpm[x] - 4000) * 0.25;
                mjdBoostData[y][x] = Math.Round(Math.Min(2350.0, Math.Max(1000.0, targetMbar)), 0);
            }
        }

        marelliMjd.Maps.Add(new ReadonlyEcuMap
        {
            MapId = "MJD_TargetBoost",
            Name = "Turbocharger Target Boost Pressure",
            Category = MapCategory.Boost,
            Description = "Requested absolute boost pressure commanded by MJD ECM to VGT / wastegate actuator",
            Dimension = MapDimension.TwoDimensional,
            MemoryAddressHex = "0x00134820",
            XAxis = new ReadonlyMapAxis { Name = "Engine Speed", Unit = "RPM", Breakpoints = mjdBoostRpm },
            YAxis = new ReadonlyMapAxis { Name = "Injected Quantity", Unit = "mm³/stk", Breakpoints = mjdBoostIq },
            ZData = mjdBoostData,
            ZUnit = "mbar",
            MinValue = 980.0,
            MaxValue = 2400.0,
            DecimalPrecision = 0,
            HeatmapPalette = "viridis"
        });

        // Driver Demand / Accelerator Pedal Map
        double[] mjdPedalPct = new double[] { 0, 10, 25, 50, 75, 100 };
        double[] mjdPedalRpm = new double[] { 800, 1500, 2200, 3000, 3800, 4500 };
        double[][] mjdDriverWish = new double[mjdPedalPct.Length][];
        for (int y = 0; y < mjdPedalPct.Length; y++)
        {
            mjdDriverWish[y] = new double[mjdPedalRpm.Length];
            for (int x = 0; x < mjdPedalRpm.Length; x++)
            {
                double iqDemand = (mjdPedalPct[y] / 100.0) * 58.0;
                if (mjdPedalRpm[x] > 4200) iqDemand *= Math.Max(0.0, 1.0 - ((mjdPedalRpm[x] - 4200) / 600.0));
                mjdDriverWish[y][x] = Math.Round(iqDemand, 1);
            }
        }

        marelliMjd.Maps.Add(new ReadonlyEcuMap
        {
            MapId = "MJD_DriverWish",
            Name = "Driver Wish (Pedal vs Injected Quantity)",
            Category = MapCategory.Torque,
            Description = "Driver torque demand translation from accelerator pedal percentage to fuel delivery",
            Dimension = MapDimension.TwoDimensional,
            MemoryAddressHex = "0x00118200",
            XAxis = new ReadonlyMapAxis { Name = "Engine Speed", Unit = "RPM", Breakpoints = mjdPedalRpm },
            YAxis = new ReadonlyMapAxis { Name = "Pedal Position", Unit = "%", Breakpoints = mjdPedalPct },
            ZData = mjdDriverWish,
            ZUnit = "mm³/stk",
            MinValue = 0.0,
            MaxValue = 60.0,
            DecimalPrecision = 1,
            HeatmapPalette = "magma"
        });

        _packs.Add(marelliMjd);

        // 1. Bosch MED17.5 (VAG EA888 Gen2)
        var med17 = new EcuDefinitionPack
        {
            Id = "med17_ea888_gen2",
            Name = "Bosch MED17.5 (VAG 2.0 TSI EA888 Gen 2)",
            TargetPlatform = "VW Golf GTI Mk6 / Scirocco / Audi A3 2.0 TSI",
            HardwarePartNumber = "06J907309B",
            SoftwareVersion = "0030",
            CalibrationId = "1K0907115AS",
            Author = "Factory Bosch MED17 A2L Reference",
            Version = "2.1",
            SupportedProtocols = new List<string> { "UDS", "VAG TP2.0", "CAN ISO-TP" }
        };

        // KFZW Ignition Timing Base Map (12 RPM x 8 Load)
        double[] med17Rpm = new double[] { 1000, 1500, 2000, 2500, 3000, 3500, 4000, 4500, 5000, 5500, 6000, 6500 };
        double[] med17Load = new double[] { 40, 60, 80, 100, 120, 140, 160, 180 };
        double[][] kfzwData = new double[med17Load.Length][];
        for (int y = 0; y < med17Load.Length; y++)
        {
            kfzwData[y] = new double[med17Rpm.Length];
            for (int x = 0; x < med17Rpm.Length; x++)
            {
                // Physics-based spark timing curve: higher RPM = more advance, higher load/boost = less advance to avoid knock
                double baseAdvance = 18.0 + (med17Rpm[x] * 0.0035);
                double loadRetard = (med17Load[y] * 0.12);
                kfzwData[y][x] = Math.Round(Math.Max(3.0, baseAdvance - loadRetard), 1);
            }
        }

        med17.Maps.Add(new ReadonlyEcuMap
        {
            MapId = "KFZW",
            Name = "KFZW - Ignition Timing Base Angle",
            Category = MapCategory.Ignition,
            Description = "Optimum base ignition advance angle as a function of engine speed and relative cylinder air charge",
            Dimension = MapDimension.TwoDimensional,
            MemoryAddressHex = "0x001B4280",
            XAxis = new ReadonlyMapAxis { Name = "Engine Speed", Unit = "RPM", Breakpoints = med17Rpm },
            YAxis = new ReadonlyMapAxis { Name = "Relative Air Charge", Unit = "%", Breakpoints = med17Load },
            ZData = kfzwData,
            ZUnit = "°BTDC",
            MinValue = 0.0,
            MaxValue = 42.0,
            DecimalPrecision = 1,
            HeatmapPalette = "viridis"
        });

        // Target Boost Pressure Map (RPM x Load)
        double[][] boostData = new double[med17Load.Length][];
        for (int y = 0; y < med17Load.Length; y++)
        {
            boostData[y] = new double[med17Rpm.Length];
            for (int x = 0; x < med17Rpm.Length; x++)
            {
                double targetMbar = 1013.0 + (med17Load[y] > 70 ? (med17Load[y] - 70) * 11.5 : 0);
                if (med17Rpm[x] > 5800) targetMbar -= (med17Rpm[x] - 5800) * 0.15; // Turbo taper at redline
                boostData[y][x] = Math.Round(targetMbar);
            }
        }

        med17.Maps.Add(new ReadonlyEcuMap
        {
            MapId = "KFLDHBN",
            Name = "KFLDHBN - Target Manifold Absolute Pressure",
            Category = MapCategory.Boost,
            Description = "Requested absolute manifold boost pressure across engine speed and throttle load demand",
            Dimension = MapDimension.TwoDimensional,
            MemoryAddressHex = "0x001C1420",
            XAxis = new ReadonlyMapAxis { Name = "Engine Speed", Unit = "RPM", Breakpoints = med17Rpm },
            YAxis = new ReadonlyMapAxis { Name = "Engine Load", Unit = "%", Breakpoints = med17Load },
            ZData = boostData,
            ZUnit = "mbar",
            MinValue = 950.0,
            MaxValue = 2450.0,
            DecimalPrecision = 0,
            HeatmapPalette = "turbo"
        });

        // LAMFA - Driver Demand Lambda Target
        double[] lamfaPedal = new double[] { 30, 50, 70, 85, 95, 100 };
        double[] lamfaRpm = new double[] { 1500, 2500, 3500, 4500, 5500, 6500 };
        double[][] lamfaData = new double[lamfaPedal.Length][];
        for (int y = 0; y < lamfaPedal.Length; y++)
        {
            lamfaData[y] = new double[lamfaRpm.Length];
            for (int x = 0; x < lamfaRpm.Length; x++)
            {
                double lam = lamfaPedal[y] >= 80 ? 0.82 - ((lamfaRpm[x] - 1500) * 0.000015) : 1.00;
                lamfaData[y][x] = Math.Round(lam, 3);
            }
        }

        med17.Maps.Add(new ReadonlyEcuMap
        {
            MapId = "LAMFA",
            Name = "LAMFA - Driver Demand Target Lambda",
            Category = MapCategory.Fuel,
            Description = "Enrichment lambda target commanded as a function of accelerator pedal position and engine speed",
            Dimension = MapDimension.TwoDimensional,
            MemoryAddressHex = "0x001BA800",
            XAxis = new ReadonlyMapAxis { Name = "Engine Speed", Unit = "RPM", Breakpoints = lamfaRpm },
            YAxis = new ReadonlyMapAxis { Name = "Accelerator Pedal", Unit = "%", Breakpoints = lamfaPedal },
            ZData = lamfaData,
            ZUnit = "λ",
            MinValue = 0.75,
            MaxValue = 1.02,
            DecimalPrecision = 3,
            HeatmapPalette = "coolwarm"
        });

        _packs.Add(med17);

        // 2. Continental Simos 18.1 (VAG EA888 Gen3)
        var simos18 = new EcuDefinitionPack
        {
            Id = "simos18_ea888_gen3",
            Name = "Continental Simos 18.1 (VAG 2.0 TSI EA888 Gen 3)",
            TargetPlatform = "VW Golf Mk7 GTI / Golf R / Audi S3 8V 2.0 TSI",
            HardwarePartNumber = "06K907425B",
            SoftwareVersion = "SC800S5000000",
            CalibrationId = "5G0906259A",
            Author = "Factory Siemens/Continental ASAP2 Reference",
            Version = "3.0",
            SupportedProtocols = new List<string> { "UDS", "CAN ISO-TP" }
        };

        double[] simosRpm = new double[] { 1200, 2000, 2800, 3600, 4400, 5200, 6000, 6800 };
        double[] simosLoad = new double[] { 50, 80, 110, 140, 170, 200, 230, 260 };
        double[][] simosBoostData = new double[simosLoad.Length][];
        for (int y = 0; y < simosLoad.Length; y++)
        {
            simosBoostData[y] = new double[simosRpm.Length];
            for (int x = 0; x < simosRpm.Length; x++)
            {
                double targetMbar = 1013.0 + (simosLoad[y] > 80 ? (simosLoad[y] - 80) * 8.5 : 0);
                simosBoostData[y][x] = Math.Round(Math.Min(2650, targetMbar));
            }
        }

        simos18.Maps.Add(new ReadonlyEcuMap
        {
            MapId = "Simos_TargetBoost",
            Name = "Target Manifold Boost Pressure (MAP)",
            Category = MapCategory.Boost,
            Description = "Closed-loop electronic wastegate setpoint pressure for IS20 / IS38 turbocharger",
            Dimension = MapDimension.TwoDimensional,
            MemoryAddressHex = "0x0084F100",
            XAxis = new ReadonlyMapAxis { Name = "Engine Speed", Unit = "RPM", Breakpoints = simosRpm },
            YAxis = new ReadonlyMapAxis { Name = "Cylinder Air Charge", Unit = "%", Breakpoints = simosLoad },
            ZData = simosBoostData,
            ZUnit = "mbar",
            MinValue = 980.0,
            MaxValue = 2700.0,
            DecimalPrecision = 0,
            HeatmapPalette = "turbo"
        });

        _packs.Add(simos18);
    }
}
