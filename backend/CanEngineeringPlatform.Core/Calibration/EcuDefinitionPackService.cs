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

        // Find nearest Y index (Load / IQ / Throttle / Pressure)
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

        // True Bilinear Interpolation for smooth continuous Z value
        if (map.YAxis != null && map.YAxis.Breakpoints.Length > 1 && map.XAxis.Breakpoints.Length > 1)
        {
            // Find bounding X indices [x0, x1]
            int x0 = 0;
            for (int i = 0; i < map.XAxis.Breakpoints.Length - 1; i++)
            {
                if (rpm >= map.XAxis.Breakpoints[i] && rpm <= map.XAxis.Breakpoints[i + 1])
                {
                    x0 = i;
                    break;
                }
                if (rpm > map.XAxis.Breakpoints[i + 1]) x0 = i + 1;
            }
            int x1 = Math.Min(x0 + 1, map.XAxis.Breakpoints.Length - 1);
            if (x0 == x1 && x0 > 0) x0--;

            // Find bounding Y indices [y0, y1]
            int y0 = 0;
            for (int j = 0; j < map.YAxis.Breakpoints.Length - 1; j++)
            {
                if (load >= map.YAxis.Breakpoints[j] && load <= map.YAxis.Breakpoints[j + 1])
                {
                    y0 = j;
                    break;
                }
                if (load > map.YAxis.Breakpoints[j + 1]) y0 = j + 1;
            }
            int y1 = Math.Min(y0 + 1, map.YAxis.Breakpoints.Length - 1);
            if (y0 == y1 && y0 > 0) y0--;

            double xSpan = map.XAxis.Breakpoints[x1] - map.XAxis.Breakpoints[x0];
            double ySpan = map.YAxis.Breakpoints[y1] - map.YAxis.Breakpoints[y0];

            double tx = xSpan > 0.0001 ? Math.Clamp((rpm - map.XAxis.Breakpoints[x0]) / xSpan, 0.0, 1.0) : 0.0;
            double ty = ySpan > 0.0001 ? Math.Clamp((load - map.YAxis.Breakpoints[y0]) / ySpan, 0.0, 1.0) : 0.0;

            if (map.ZData.Length > y1 && map.ZData[y0].Length > x1 && map.ZData[y1].Length > x1)
            {
                double z00 = map.ZData[y0][x0];
                double z01 = map.ZData[y0][x1];
                double z10 = map.ZData[y1][x0];
                double z11 = map.ZData[y1][x1];

                double interpZ = (1.0 - tx) * (1.0 - ty) * z00 +
                                 tx * (1.0 - ty) * z01 +
                                 (1.0 - tx) * ty * z10 +
                                 tx * ty * z11;

                tracer.InterpolatedZValue = Math.Round(interpZ, map.DecimalPrecision);
            }
            else if (map.ZData.Length > bestY && map.ZData[bestY].Length > bestX)
            {
                tracer.InterpolatedZValue = map.ZData[bestY][bestX];
            }
        }
        else if (map.ZData.Length > bestY && map.ZData[bestY].Length > bestX)
        {
            tracer.InterpolatedZValue = map.ZData[bestY][bestX];
        }

        return tracer;
    }

    private void InitializePacks()
    {
        // 0. Bosch EDC17C55 / EDC17C53 (TriCore TC1767 - C04 / C63 Diesel)
        var edc17 = new EcuDefinitionPack
        {
            Id = "edc17c55_c04_c63",
            Name = "Bosch EDC17C55 / EDC17C53 (Mahindra 2.2 mHawk - C04 / C63)",
            TargetPlatform = "Mahindra Scorpio / Thar / XUV500 2.2 mHawk & Tata 2.2 Dicor (EDC17C55 TC1767)",
            HardwarePartNumber = "0281017832 / 0281031388",
            SoftwareVersion = "1037521876 / C04_C63",
            CalibrationId = "EDC17C55_C04_C63",
            Author = "Bosch Diesel Systems A2L / DAMOS Calibration Reference",
            Version = "2.4",
            SupportedProtocols = new List<string> { "UDS (ISO 14229)", "KWP2000 (ISO 14230)", "CAN ISO-TP" }
        };

        // Map 1: Driver Wish (RPM x Pedal %) -> Injected Quantity (mm³/stk)
        double[] edc17PedalRpm = { 800, 1200, 1600, 2000, 2500, 3000, 3500, 4000, 4400 };
        double[] edc17PedalPct = { 0, 10, 20, 35, 50, 70, 85, 100 };
        double[][] edc17DriverWish = new double[edc17PedalPct.Length][];
        for (int y = 0; y < edc17PedalPct.Length; y++)
        {
            edc17DriverWish[y] = new double[edc17PedalRpm.Length];
            for (int x = 0; x < edc17PedalRpm.Length; x++)
            {
                double baseIq = (edc17PedalPct[y] / 100.0) * 64.0;
                if (edc17PedalRpm[x] > 4000) baseIq *= Math.Max(0.0, 1.0 - ((edc17PedalRpm[x] - 4000) / 500.0));
                edc17DriverWish[y][x] = Math.Round(baseIq, 1);
            }
        }
        edc17.Maps.Add(new ReadonlyEcuMap
        {
            MapId = "EDC17_DriverWish",
            Name = "Driver Wish (Pedal % vs Injected Fuel Quantity)",
            Category = MapCategory.Torque,
            Description = "Translates driver accelerator pedal position demand into requested injected fuel volume per cylinder stroke",
            Dimension = MapDimension.TwoDimensional,
            MemoryAddressHex = "0x8004A200",
            XAxis = new ReadonlyMapAxis { Name = "Engine Speed", Unit = "RPM", Breakpoints = edc17PedalRpm },
            YAxis = new ReadonlyMapAxis { Name = "Pedal Position", Unit = "%", Breakpoints = edc17PedalPct },
            ZData = edc17DriverWish,
            ZUnit = "mm³/stk",
            MinValue = 0.0,
            MaxValue = 65.0,
            DecimalPrecision = 1,
            HeatmapPalette = "magma"
        });

        // Map 2: Common Rail Target Fuel Pressure (RPM x Injected Quantity) -> Rail Pressure bar
        double[] edc17RailRpm = { 800, 1200, 1600, 2000, 2400, 2800, 3200, 3600, 4000, 4400 };
        double[] edc17RailIq = { 5, 10, 15, 20, 30, 40, 50, 60 };
        double[][] edc17RailData = new double[edc17RailIq.Length][];
        for (int y = 0; y < edc17RailIq.Length; y++)
        {
            edc17RailData[y] = new double[edc17RailRpm.Length];
            for (int x = 0; x < edc17RailRpm.Length; x++)
            {
                double basePressure = 280.0 + (edc17RailRpm[x] * 0.16);
                double loadPressure = edc17RailIq[y] * 16.5;
                edc17RailData[y][x] = Math.Round(Math.Min(1800.0, Math.Max(260.0, basePressure + loadPressure)), 0);
            }
        }
        edc17.Maps.Add(new ReadonlyEcuMap
        {
            MapId = "EDC17_RailPressure",
            Name = "Common Rail Fuel Pressure Target",
            Category = MapCategory.Fuel,
            Description = "Target high-pressure common rail fuel setpoint commanded to DRV and CP4 metering unit",
            Dimension = MapDimension.TwoDimensional,
            MemoryAddressHex = "0x8006E100",
            XAxis = new ReadonlyMapAxis { Name = "Engine Speed", Unit = "RPM", Breakpoints = edc17RailRpm },
            YAxis = new ReadonlyMapAxis { Name = "Injected Quantity", Unit = "mm³/stk", Breakpoints = edc17RailIq },
            ZData = edc17RailData,
            ZUnit = "bar",
            MinValue = 250.0,
            MaxValue = 1800.0,
            DecimalPrecision = 0,
            HeatmapPalette = "turbo"
        });

        // Map 3: Target Turbocharger Boost Pressure (RPM x Injected Quantity) -> Absolute Boost mbar
        double[] edc17BoostRpm = { 1000, 1400, 1750, 2000, 2400, 2800, 3200, 3600, 4000, 4400 };
        double[] edc17BoostIq = { 10, 20, 30, 40, 50, 60 };
        double[][] edc17BoostData = new double[edc17BoostIq.Length][];
        for (int y = 0; y < edc17BoostIq.Length; y++)
        {
            edc17BoostData[y] = new double[edc17BoostRpm.Length];
            for (int x = 0; x < edc17BoostRpm.Length; x++)
            {
                double targetMbar = 1013.0 + (edc17BoostIq[y] > 15 ? (edc17BoostIq[y] - 15) * 31.0 : 0.0);
                if (edc17BoostRpm[x] > 3800) targetMbar -= (edc17BoostRpm[x] - 3800) * 0.22;
                edc17BoostData[y][x] = Math.Round(Math.Min(2550.0, Math.Max(1000.0, targetMbar)), 0);
            }
        }
        edc17.Maps.Add(new ReadonlyEcuMap
        {
            MapId = "EDC17_TargetBoost",
            Name = "Variable VGT Turbocharger Boost Target",
            Category = MapCategory.Boost,
            Description = "Desired absolute intake manifold charge pressure commanded to Garrett/Honeywell electronic VGT actuator",
            Dimension = MapDimension.TwoDimensional,
            MemoryAddressHex = "0x8008F440",
            XAxis = new ReadonlyMapAxis { Name = "Engine Speed", Unit = "RPM", Breakpoints = edc17BoostRpm },
            YAxis = new ReadonlyMapAxis { Name = "Injected Quantity", Unit = "mm³/stk", Breakpoints = edc17BoostIq },
            ZData = edc17BoostData,
            ZUnit = "mbar",
            MinValue = 980.0,
            MaxValue = 2600.0,
            DecimalPrecision = 0,
            HeatmapPalette = "viridis"
        });

        // Map 4: Smoke Limiter / AFR Map (RPM x Air Mass) -> Max Injected Quantity mm³/stk
        double[] edc17SmokeRpm = { 1000, 1400, 1800, 2200, 2600, 3000, 3500, 4000, 4400 };
        double[] edc17AirMass = { 300, 400, 500, 650, 800, 950, 1100 };
        double[][] edc17SmokeData = new double[edc17AirMass.Length][];
        for (int y = 0; y < edc17AirMass.Length; y++)
        {
            edc17SmokeData[y] = new double[edc17SmokeRpm.Length];
            for (int x = 0; x < edc17SmokeRpm.Length; x++)
            {
                double maxIq = (edc17AirMass[y] / 17.0);
                if (edc17SmokeRpm[x] < 1400) maxIq *= 0.82;
                edc17SmokeData[y][x] = Math.Round(Math.Min(65.0, Math.Max(12.0, maxIq)), 1);
            }
        }
        edc17.Maps.Add(new ReadonlyEcuMap
        {
            MapId = "EDC17_SmokeLimiter",
            Name = "Smoke Limiter (MAF Air Mass vs Max Fuel)",
            Category = MapCategory.Airflow,
            Description = "Soot prevention limit: calculates maximum allowable injected fuel quantity based on measured hot-film MAF airflow",
            Dimension = MapDimension.TwoDimensional,
            MemoryAddressHex = "0x800A4100",
            XAxis = new ReadonlyMapAxis { Name = "Engine Speed", Unit = "RPM", Breakpoints = edc17SmokeRpm },
            YAxis = new ReadonlyMapAxis { Name = "Air Mass", Unit = "mg/hub", Breakpoints = edc17AirMass },
            ZData = edc17SmokeData,
            ZUnit = "mm³/stk",
            MinValue = 10.0,
            MaxValue = 65.0,
            DecimalPrecision = 1,
            HeatmapPalette = "coolwarm"
        });

        // Map 5: Engine Torque Limiter (RPM x Atmospheric Pressure) -> Torque Nm
        double[] edc17TorqueRpm = { 800, 1200, 1600, 2000, 2400, 2800, 3200, 3600, 4000, 4400 };
        double[] edc17Atmosphere = { 700, 800, 900, 1000 };
        double[][] edc17TorqueData = new double[edc17Atmosphere.Length][];
        for (int y = 0; y < edc17Atmosphere.Length; y++)
        {
            edc17TorqueData[y] = new double[edc17TorqueRpm.Length];
            for (int x = 0; x < edc17TorqueRpm.Length; x++)
            {
                double rpm = edc17TorqueRpm[x];
                double peakNm = (rpm >= 1600 && rpm <= 2600) ? 320.0 : (rpm < 1600 ? 210.0 + ((rpm - 800) * 0.137) : 320.0 - ((rpm - 2600) * 0.065));
                double altitudeFactor = edc17Atmosphere[y] / 1000.0;
                edc17TorqueData[y][x] = Math.Round(peakNm * altitudeFactor, 0);
            }
        }
        edc17.Maps.Add(new ReadonlyEcuMap
        {
            MapId = "EDC17_TorqueLimiter",
            Name = "Engine Torque Limiter (Atmospheric Altitude Protection)",
            Category = MapCategory.Torque,
            Description = "Maximum engine crank torque allowed by drivetrain, de-rated at higher altitudes to prevent turbo overspeed",
            Dimension = MapDimension.TwoDimensional,
            MemoryAddressHex = "0x800C3300",
            XAxis = new ReadonlyMapAxis { Name = "Engine Speed", Unit = "RPM", Breakpoints = edc17TorqueRpm },
            YAxis = new ReadonlyMapAxis { Name = "Ambient Pressure", Unit = "hPa", Breakpoints = edc17Atmosphere },
            ZData = edc17TorqueData,
            ZUnit = "Nm",
            MinValue = 140.0,
            MaxValue = 330.0,
            DecimalPrecision = 0,
            HeatmapPalette = "viridis"
        });

        // Map 6: Start of Injection (SOI) Main Angle (RPM x Injected Quantity) -> °BTDC
        double[] edc17SoiRpm = { 800, 1200, 1600, 2000, 2500, 3000, 3500, 4000, 4400 };
        double[] edc17SoiIq = { 5, 10, 20, 30, 40, 50, 60 };
        double[][] edc17SoiData = new double[edc17SoiIq.Length][];
        for (int y = 0; y < edc17SoiIq.Length; y++)
        {
            edc17SoiData[y] = new double[edc17SoiRpm.Length];
            for (int x = 0; x < edc17SoiRpm.Length; x++)
            {
                double rpmAdvance = (edc17SoiRpm[x] - 800) * 0.0042;
                double loadAdvance = edc17SoiIq[y] * 0.07;
                edc17SoiData[y][x] = Math.Round(-1.5 + rpmAdvance + loadAdvance, 1);
            }
        }
        edc17.Maps.Add(new ReadonlyEcuMap
        {
            MapId = "EDC17_StartOfInjection",
            Name = "Start of Injection (SOI Base Advance)",
            Category = MapCategory.Ignition,
            Description = "Main start of injection timing angle relative to Top Dead Center (negative = after TDC, positive = before TDC)",
            Dimension = MapDimension.TwoDimensional,
            MemoryAddressHex = "0x800D6040",
            XAxis = new ReadonlyMapAxis { Name = "Engine Speed", Unit = "RPM", Breakpoints = edc17SoiRpm },
            YAxis = new ReadonlyMapAxis { Name = "Injected Quantity", Unit = "mm³/stk", Breakpoints = edc17SoiIq },
            ZData = edc17SoiData,
            ZUnit = "°BTDC",
            MinValue = -3.0,
            MaxValue = 22.0,
            DecimalPrecision = 1,
            HeatmapPalette = "coolwarm"
        });

        _packs.Add(edc17);

        // 1. Magneti Marelli MJD (Maruti Suzuki Swift Diesel 1.3 DDiS)
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
