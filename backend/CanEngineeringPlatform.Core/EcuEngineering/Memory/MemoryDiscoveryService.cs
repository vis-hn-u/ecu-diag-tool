using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CanEngineeringPlatform.Core.EcuEngineering.Compatibility;
using CanEngineeringPlatform.Core.EcuEngineering.Core;
using CanEngineeringPlatform.Core.EcuEngineering.Identification;
using CanEngineeringPlatform.Core.EcuEngineering.Protocols;

namespace CanEngineeringPlatform.Core.EcuEngineering.Memory;

public interface IMemoryDiscoveryService
{
    Task<EcuMemoryMap> DiscoverMemoryMapAsync(EcuIdentificationProfile identification, EcuProtocolCapabilityMatrix? protocolMatrix = null);
    Task<EcuReadCapabilityMatrix> EvaluateReadCapabilitiesAsync(EcuMemoryMap memoryMap, EcuProtocolCapabilityMatrix? protocolMatrix = null);
    EcuMemoryMap? GetCachedMemoryMap(string ecuName);
    EcuReadCapabilityMatrix? GetCachedReadCapabilities(string ecuName);
}

public class MemoryDiscoveryService : IMemoryDiscoveryService
{
    private readonly IEcuCompatibilityManager _compatibilityManager;
    private readonly ConcurrentDictionary<string, EcuMemoryMap> _cachedMaps = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, EcuReadCapabilityMatrix> _cachedCapabilities = new(StringComparer.OrdinalIgnoreCase);

    public MemoryDiscoveryService(IEcuCompatibilityManager compatibilityManager)
    {
        _compatibilityManager = compatibilityManager ?? throw new ArgumentNullException(nameof(compatibilityManager));
    }

    public Task<EcuMemoryMap> DiscoverMemoryMapAsync(EcuIdentificationProfile identification, EcuProtocolCapabilityMatrix? protocolMatrix = null)
    {
        if (identification == null) throw new ArgumentNullException(nameof(identification));

        var memoryMap = new EcuMemoryMap
        {
            EcuName = identification.EcuName,
            HardwareNumber = identification.HardwareNumber?.Value,
            SoftwareNumber = identification.SoftwareNumber?.Value,
            DiscoveryTimestampUtc = DateTime.UtcNow
        };

        // Step 1: Look for an authoritative match in the Compatibility Catalog
        EcuFamilyProfile? matchedProfile = null;
        if (!string.IsNullOrWhiteSpace(identification.HardwareNumber?.Value) || !string.IsNullOrWhiteSpace(identification.SoftwareNumber?.Value))
        {
            matchedProfile = _compatibilityManager.MatchProfile(
                identification.HardwareNumber?.Value,
                identification.SoftwareNumber?.Value,
                identification.EcuName);
        }

        if (matchedProfile != null && matchedProfile.MemoryRegions.Count > 0)
        {
            memoryMap.EcuFamily = matchedProfile.FamilyId;
            memoryMap.TotalAddressableBytes = matchedProfile.FlashSizeBytes + matchedProfile.EepromSizeBytes;

            foreach (var regionDef in matchedProfile.MemoryRegions)
            {
                var region = new MemoryRegion
                {
                    RegionName = regionDef.RegionName,
                    StartAddress = regionDef.StartAddress,
                    EndAddress = regionDef.StartAddress + regionDef.LengthBytes - 1,
                    Type = ClassifyMemoryType(regionDef.RegionName),
                    AddressWidth = AddressWidth.Width32Bit,
                    ReadCapability = regionDef.RequiresSecurityAccess ? ReadPermission.RequiresSecurityAccess : ReadPermission.ReadSupported,
                    WriteCapability = regionDef.IsWritable ? WritePermission.RequiresSecurityAccess : WritePermission.ReadOnly,
                    EraseCapability = regionDef.IsWritable ? ErasePermission.RequiresSecurityAccess : ErasePermission.EraseNotSupported,
                    PreferredReadMethod = DiagnosticReadMethod.ReadMemoryByAddress_0x23,
                    RequiredSession = regionDef.RequiresSecurityAccess ? (byte)0x03 : (byte)0x01,
                    RequiredSecurityLevel = regionDef.RequiresSecurityAccess ? (byte)0x01 : null,
                    MaxReadLengthBytes = 256,
                    AddressConstraints = "32-bit linear address aligned",
                    KnownRestrictions = regionDef.RequiresSecurityAccess ? "Security Level 0x01 Seed/Key exchange required" : "Standard diagnostic read allowed",
                    Confidence = identification.HardwareNumber?.Confidence == IdentificationConfidence.Confirmed
                        ? IdentificationConfidence.Confirmed
                        : IdentificationConfidence.Detected,
                    Provenance = MemoryRegionProvenance.CatalogDerived
                };

                var evidence = EvidenceSource.Create(
                    "OemDatabase",
                    matchedProfile.FamilyId,
                    $"Catalog-derived region '{regionDef.RegionName}' for {matchedProfile.Name} ({regionDef.Description})");
                region.Citations.Add(evidence);

                memoryMap.Regions.Add(region);
            }
        }
        else
        {
            // Step 2: Inferred baseline memory map for unknown ECU architectures
            memoryMap.EcuFamily = "GENERIC_AUTOMOTIVE_ECU";
            memoryMap.TotalAddressableBytes = 2097152; // 2 MB

            // Standard generic automotive regions
            var bootRegion = new MemoryRegion
            {
                RegionName = "Bootloader (Inferred)",
                StartAddress = 0x00000000,
                EndAddress = 0x0001FFFF, // 128 KB
                Type = MemoryType.Bootloader,
                AddressWidth = AddressWidth.Width32Bit,
                ReadCapability = ReadPermission.RequiresSecurityAccess,
                WriteCapability = WritePermission.ReadOnly,
                EraseCapability = ErasePermission.Forbidden,
                PreferredReadMethod = DiagnosticReadMethod.ReadMemoryByAddress_0x23,
                RequiredSession = 0x03,
                Confidence = IdentificationConfidence.Inferred,
                Provenance = MemoryRegionProvenance.Inferred
            };
            bootRegion.Citations.Add(EvidenceSource.Create("InferredArchitecture", "Baseline", "Standard automotive 128KB bootloader lower flash sector"));

            var appRegion = new MemoryRegion
            {
                RegionName = "Application Software (Inferred)",
                StartAddress = 0x00020000,
                EndAddress = 0x001BFFFF, // 1.625 MB
                Type = MemoryType.Application,
                AddressWidth = AddressWidth.Width32Bit,
                ReadCapability = ReadPermission.ReadSupported,
                WriteCapability = WritePermission.ReadOnly,
                EraseCapability = ErasePermission.EraseNotSupported,
                PreferredReadMethod = DiagnosticReadMethod.ReadMemoryByAddress_0x23,
                RequiredSession = 0x01,
                Confidence = IdentificationConfidence.Inferred,
                Provenance = MemoryRegionProvenance.Inferred
            };
            appRegion.Citations.Add(EvidenceSource.Create("InferredArchitecture", "Baseline", "Standard automotive main application program sector"));

            var calRegion = new MemoryRegion
            {
                RegionName = "Calibration Data (Inferred)",
                StartAddress = 0x001C0000,
                EndAddress = 0x001FFFFF, // 256 KB
                Type = MemoryType.Calibration,
                AddressWidth = AddressWidth.Width32Bit,
                ReadCapability = ReadPermission.RequiresDiagnosticSession,
                WriteCapability = WritePermission.RequiresSecurityAccess,
                EraseCapability = ErasePermission.RequiresSecurityAccess,
                PreferredReadMethod = DiagnosticReadMethod.ReadMemoryByAddress_0x23,
                RequiredSession = 0x03,
                Confidence = IdentificationConfidence.Inferred,
                Provenance = MemoryRegionProvenance.Inferred
            };
            calRegion.Citations.Add(EvidenceSource.Create("InferredArchitecture", "Baseline", "Standard upper flash calibration sector"));

            memoryMap.Regions.AddRange(new[] { bootRegion, appRegion, calRegion });
        }

        memoryMap.RecalculateGaps();
        _cachedMaps[identification.EcuName] = memoryMap;

        return Task.FromResult(memoryMap);
    }

    public Task<EcuReadCapabilityMatrix> EvaluateReadCapabilitiesAsync(EcuMemoryMap memoryMap, EcuProtocolCapabilityMatrix? protocolMatrix = null)
    {
        if (memoryMap == null) throw new ArgumentNullException(nameof(memoryMap));

        var matrix = new EcuReadCapabilityMatrix
        {
            EcuName = memoryMap.EcuName,
            EvaluatedTimestampUtc = DateTime.UtcNow
        };

        if (protocolMatrix != null)
        {
            matrix.SupportsReadMemoryByAddress0x23 = protocolMatrix.Services.TryGetValue(0x23, out var s23) && s23.IsSupported;
            matrix.SupportsRequestUpload0x35 = protocolMatrix.Services.TryGetValue(0x35, out var s35) && s35.IsSupported;
            matrix.SupportsReadDataByIdentifier0x22 = !protocolMatrix.Services.TryGetValue(0x22, out var s22) || s22.IsSupported;
            matrix.SecurityAccessRequiredOverall = protocolMatrix.SecurityAccessRequiredForCalibrations;
        }
        else
        {
            matrix.SupportsReadMemoryByAddress0x23 = true;
            matrix.SupportsRequestUpload0x35 = false;
            matrix.SupportsReadDataByIdentifier0x22 = true;
            matrix.SecurityAccessRequiredOverall = true;
        }

        foreach (var region in memoryMap.Regions)
        {
            var cap = new RegionReadCapability
            {
                RegionName = region.RegionName,
                StartAddress = region.StartAddress,
                LengthBytes = region.Length,
                ReadMethod = region.PreferredReadMethod,
                Permission = region.ReadCapability,
                RequiredSession = region.RequiredSession,
                SessionName = GetSessionName(region.RequiredSession),
                RequiresSecurityAccess = region.RequiredSecurityLevel.HasValue,
                SecurityLevel = region.RequiredSecurityLevel,
                RequiresAuthorization = true, // By design, all safe ECU reading requires operator authorization
                MaxChunkLengthBytes = region.MaxReadLengthBytes,
                AddressConstraints = region.AddressConstraints,
                KnownRestrictions = region.KnownRestrictions
            };
            matrix.RegionCapabilities.Add(cap);
        }

        _cachedCapabilities[memoryMap.EcuName] = matrix;
        return Task.FromResult(matrix);
    }

    public EcuMemoryMap? GetCachedMemoryMap(string ecuName)
    {
        if (string.IsNullOrWhiteSpace(ecuName)) return null;
        _cachedMaps.TryGetValue(ecuName, out var map);
        return map;
    }

    public EcuReadCapabilityMatrix? GetCachedReadCapabilities(string ecuName)
    {
        if (string.IsNullOrWhiteSpace(ecuName)) return null;
        _cachedCapabilities.TryGetValue(ecuName, out var cap);
        return cap;
    }

    private static MemoryType ClassifyMemoryType(string regionName)
    {
        string n = regionName.ToLowerInvariant();
        if (n.Contains("boot")) return MemoryType.Bootloader;
        if (n.Contains("calib")) return MemoryType.Calibration;
        if (n.Contains("eeprom") || n.Contains("data flash") || n.Contains("dflash") || n.Contains("nvm")) return MemoryType.Eeprom;
        if (n.Contains("config") || n.Contains("otp")) return MemoryType.Configuration;
        if (n.Contains("data")) return MemoryType.Data;
        if (n.Contains("reserv")) return MemoryType.Reserved;
        return MemoryType.Application;
    }

    private static string GetSessionName(byte session) => session switch
    {
        0x01 => "Default Session (0x01)",
        0x02 => "Programming Session (0x02)",
        0x03 => "Extended Diagnostic Session (0x03)",
        0x04 => "Safety System Diagnostic Session (0x04)",
        _ => $"Diagnostic Session (0x{session:X2})"
    };
}
