using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Calibration;

public enum MapDimension
{
    Scalar,
    OneDimensional,  // 1D Curve (X -> Z)
    TwoDimensional   // 2D/3D Surface Matrix (X, Y -> Z)
}

public enum MapCategory
{
    Fuel,
    Ignition,
    Boost,
    Airflow,
    Torque,
    Protection,
    VariableValveTiming,
    Idle
}

public class ReadonlyMapAxis
{
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double[] Breakpoints { get; set; } = Array.Empty<double>();
}

public class ReadonlyEcuMap
{
    public string MapId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MapCategory Category { get; set; } = MapCategory.Fuel;
    public MapDimension Dimension { get; set; } = MapDimension.TwoDimensional;
    public string MemoryAddressHex { get; set; } = "0x00000000"; // Informative read-only metadata
    public ReadonlyMapAxis XAxis { get; set; } = new();
    public ReadonlyMapAxis? YAxis { get; set; }
    public double[][] ZData { get; set; } = Array.Empty<double[]>();
    public string ZUnit { get; set; } = string.Empty;
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public int DecimalPrecision { get; set; } = 1;
    public string HeatmapPalette { get; set; } = "viridis";
    public bool IsReadOnly => true; // Strict safety constraint: absolutely no write/flash capability
}

public class EcuDefinitionPack
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TargetPlatform { get; set; } = string.Empty;
    public string HardwarePartNumber { get; set; } = string.Empty;
    public string SoftwareVersion { get; set; } = string.Empty;
    public string CalibrationId { get; set; } = string.Empty;
    public string Author { get; set; } = "Factory Reference / Reverse Engineering Community";
    public string Version { get; set; } = "1.0";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<string> SupportedProtocols { get; set; } = new() { "UDS", "VAG TP2.0", "CAN ISO-TP" };
    public List<ReadonlyEcuMap> Maps { get; set; } = new();
    public bool IsReadOnly => true;
}

public class OperatingPointTracer
{
    public double CurrentRpm { get; set; }
    public double CurrentLoad { get; set; }
    public int NearestXIndex { get; set; }
    public int NearestYIndex { get; set; }
    public double InterpolatedZValue { get; set; }
    public string ActiveMapId { get; set; } = string.Empty;
}
