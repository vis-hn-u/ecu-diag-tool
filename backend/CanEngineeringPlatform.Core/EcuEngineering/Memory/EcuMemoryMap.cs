using System;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.EcuEngineering.Memory;

/// <summary>
/// Address gap representing unmapped space between declared memory regions.
/// </summary>
public record MemoryAddressGap(long StartAddress, long EndAddress, long LengthBytes);

/// <summary>
/// Complete synthesized memory map of an ECU, containing bounded regions,
/// read capabilities, and gap analysis.
/// </summary>
public class EcuMemoryMap
{
    public string EcuName { get; set; } = "ECM";
    public string? HardwareNumber { get; set; }
    public string? SoftwareNumber { get; set; }
    public string? EcuFamily { get; set; }
    public long TotalAddressableBytes { get; set; }
    public List<MemoryRegion> Regions { get; set; } = new();
    public List<MemoryAddressGap> Gaps { get; set; } = new();
    public DateTime DiscoveryTimestampUtc { get; set; } = DateTime.UtcNow;

    public MemoryRegion? FindRegionForAddress(long address)
    {
        return Regions.FirstOrDefault(r => r.ContainsAddress(address));
    }

    /// <summary>
    /// Computes unmapped gaps between regions in ascending address order.
    /// </summary>
    public void RecalculateGaps()
    {
        Gaps.Clear();
        if (Regions.Count <= 1) return;

        var sorted = Regions.OrderBy(r => r.StartAddress).ToList();
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            long currentEnd = sorted[i].EndAddress;
            long nextStart = sorted[i + 1].StartAddress;

            if (nextStart > currentEnd + 1)
            {
                long gapLength = nextStart - currentEnd - 1;
                Gaps.Add(new MemoryAddressGap(currentEnd + 1, nextStart - 1, gapLength));
            }
        }
    }
}
