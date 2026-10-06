using System;
using System.Collections.Generic;

namespace CanEngineeringPlatform.Core.Projects;

public class EngineeringProject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled Automotive Project";
    public string Description { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = "Lead Automotive Engineer";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
    public string ProjectHash { get; set; } = string.Empty;

    // Associated Entities & Evidence Containers
    public ProjectVehicleInfo Vehicle { get; set; } = new();
    public ProjectEcuTarget EcuTarget { get; set; } = new();
    public ProjectHardwareConfig Hardware { get; set; } = new();
    public List<string> AssociatedDbcFiles { get; set; } = new();
    public List<string> AssociatedA2lFiles { get; set; } = new();
    public List<string> AssociatedOdxFiles { get; set; } = new();
    public List<string> FirmwareImages { get; set; } = new();
    public List<string> CalibrationFiles { get; set; } = new();
    public List<string> LogCaptures { get; set; } = new();
    public List<string> DiagnosticReports { get; set; } = new();
    public List<ProjectNote> Notes { get; set; } = new();
    public List<ProjectEvidenceItem> EvidenceRecords { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class ProjectVehicleInfo
{
    public string Vin { get; set; } = string.Empty;
    public string Make { get; set; } = "Generic OEM";
    public string Model { get; set; } = "Generic Platform";
    public int Year { get; set; } = 2024;
    public string EngineCode { get; set; } = "2.0T";
    public string Transmission { get; set; } = "Automatic";
    public string Architecture { get; set; } = "CAN / CAN FD Gateway";
}

public class ProjectEcuTarget
{
    public string EcuName { get; set; } = "ECM";
    public string Family { get; set; } = "Bosch MED17.5";
    public string HardwareNumber { get; set; } = "0261S04555";
    public string SoftwareNumber { get; set; } = "1037505555";
    public uint RequestCanId { get; set; } = 0x7E0;
    public uint ResponseCanId { get; set; } = 0x7E8;
    public string Protocol { get; set; } = "UDS on ISO-TP";
    public string McuFamily { get; set; } = "Infineon TriCore TC1766 / TC1796";
    public long FlashSizeBytes { get; set; } = 2097152; // 2MB
}

public class ProjectHardwareConfig
{
    public string PreferredTransportId { get; set; } = "simulation";
    public int NominalBaudRate { get; set; } = 500000;
    public int? DataBaudRate { get; set; } = 2000000;
    public bool ListenOnly { get; set; } = false;
    public string SerialPort { get; set; } = string.Empty;
}

public class ProjectNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Author { get; set; } = "Engineer";
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = "General"; // Calibration, Diagnostics, Flash, Reversing
}

public class ProjectEvidenceItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Category { get; set; } = "CAN Capture";
    public string Description { get; set; } = string.Empty;
    public string Source { get; set; } = "Hardware VCI";
    public string Sha256Hash { get; set; } = string.Empty;
    public string RawUri { get; set; } = string.Empty;
}
