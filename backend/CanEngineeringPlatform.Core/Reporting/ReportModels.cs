using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Reporting;

public enum ReportType
{
    Diagnostic,
    Dyno,
    EcuFlashAnalysis,
    SessionLog,
    VehicleHealth
}

public enum ReportSectionType
{
    Header,
    VehicleSummary,
    KeyMetrics,
    Table,
    Chart,
    AlertNotice,
    KeyValueList,
    VisualMap,
    Notes
}

public class ReportMetadata
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Automotive Diagnostic Report";
    public string Subtitle { get; set; } = string.Empty;
    public string Organization { get; set; } = "CAN Engineering Platform";
    public string TechnicianName { get; set; } = "Diagnostic Specialist";
    public string VehicleVin { get; set; } = "N/A";
    public string VehicleDescription { get; set; } = "N/A";
    public string SoftwareVersion { get; set; } = "v2.5.0-PRO";
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ReportMetricItem
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Status { get; set; } = "Normal"; // Normal, Warning, Danger, Success, Info
}

public class ReportTableData
{
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
    public List<double>? ColumnWidthRatios { get; set; }
}

public class ReportChartSeries
{
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#0284c7"; // Default automotive blue
    public List<(double X, double Y)> Points { get; set; } = new();
    public bool IsDashed { get; set; }
    public double LineWidth { get; set; } = 1.5;
}

public class ReportChartData
{
    public string Title { get; set; } = string.Empty;
    public string XAxisLabel { get; set; } = string.Empty;
    public string YAxisLabel { get; set; } = string.Empty;
    public List<ReportChartSeries> Series { get; set; } = new();
    public double? MinX { get; set; }
    public double? MaxX { get; set; }
    public double? MinY { get; set; }
    public double? MaxY { get; set; }
}

public class ReportHeatmapData
{
    public string Title { get; set; } = string.Empty;
    public string XLabel { get; set; } = string.Empty;
    public string YLabel { get; set; } = string.Empty;
    public string ZUnit { get; set; } = string.Empty;
    public double[] XBreakpoints { get; set; } = Array.Empty<double>();
    public double[] YBreakpoints { get; set; } = Array.Empty<double>();
    public double[][] ZValues { get; set; } = Array.Empty<double[]>();
}

public class ReportSection
{
    public string SectionId { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public ReportSectionType Type { get; set; } = ReportSectionType.Table;
    public int Order { get; set; }

    // Payload properties (populated according to SectionType)
    public List<ReportMetricItem>? Metrics { get; set; }
    public ReportTableData? Table { get; set; }
    public ReportChartData? Chart { get; set; }
    public ReportHeatmapData? Heatmap { get; set; }
    public Dictionary<string, string>? KeyValues { get; set; }
    public string? AlertMessage { get; set; }
    public string? AlertSeverity { get; set; } // Info, Warning, Danger, Success
    public string? NotesContent { get; set; }
}

public class ReportDefinition
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString("N");
    public ReportType Type { get; set; } = ReportType.Diagnostic;
    public ReportMetadata Metadata { get; set; } = new();
    public List<ReportSection> Sections { get; set; } = new();
}
