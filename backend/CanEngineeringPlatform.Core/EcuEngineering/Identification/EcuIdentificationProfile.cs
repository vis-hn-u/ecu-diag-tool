using System;
using System.Collections.Generic;
using CanEngineeringPlatform.Core.EcuEngineering.Core;

namespace CanEngineeringPlatform.Core.EcuEngineering.Identification;

/// <summary>
/// Professional, OEM-aligned ECU identification profile containing multi-field evidence and confidence ratings.
/// </summary>
public class EcuIdentificationProfile
{
    public string EcuName { get; set; } = "ECM";
    public uint RequestCanId { get; set; } = 0x7E0;
    public uint ResponseCanId { get; set; } = 0x7E8;

    // Core Manufacturer & Platform
    public FieldWithConfidence<string> Manufacturer { get; set; } = FieldWithConfidence<string>.Unknown();
    public FieldWithConfidence<string> VehiclePlatform { get; set; } = FieldWithConfidence<string>.Unknown();
    public FieldWithConfidence<string> EcuFamily { get; set; } = FieldWithConfidence<string>.Unknown();

    // Part & Hardware Numbers
    public FieldWithConfidence<string> EcuPartNumber { get; set; } = FieldWithConfidence<string>.Unknown();
    public FieldWithConfidence<string> HardwareNumber { get; set; } = FieldWithConfidence<string>.Unknown();
    public FieldWithConfidence<string> SoftwareNumber { get; set; } = FieldWithConfidence<string>.Unknown();
    public FieldWithConfidence<string> SoftwareVersion { get; set; } = FieldWithConfidence<string>.Unknown();
    public FieldWithConfidence<string> CalibrationId { get; set; } = FieldWithConfidence<string>.Unknown();
    public FieldWithConfidence<string> Vin { get; set; } = FieldWithConfidence<string>.Unknown();

    // Protocol & Network
    public FieldWithConfidence<string> DiagnosticProtocol { get; set; } = FieldWithConfidence<string>.Unknown();
    public FieldWithConfidence<uint> CanBitrate { get; set; } = FieldWithConfidence<uint>.Unknown();
    public FieldWithConfidence<string> CanAddressing { get; set; } = FieldWithConfidence<string>.Unknown();

    // Internal Architecture & Memory
    public FieldWithConfidence<string> BootloaderInfo { get; set; } = FieldWithConfidence<string>.Unknown();
    public FieldWithConfidence<string> ApplicationInfo { get; set; } = FieldWithConfidence<string>.Unknown();
    public FieldWithConfidence<string> McuArchitecture { get; set; } = FieldWithConfidence<string>.Unknown();
    public FieldWithConfidence<string> MemoryCharacteristics { get; set; } = FieldWithConfidence<string>.Unknown();

    // Service & Capability Matrix
    public List<string> SupportedServices { get; set; } = new();
    public Dictionary<string, string> ExtendedParameters { get; set; } = new();

    // Overall State
    public IdentificationConfidence OverallConfidence { get; set; } = IdentificationConfidence.Unknown;
    public DateTime IdentificationTimestampUtc { get; set; } = DateTime.UtcNow;
    public List<EvidenceSource> AllEvidence { get; set; } = new();
}
