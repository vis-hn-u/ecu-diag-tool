using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.Research;

public enum ResearchSourceAuthority
{
    AuthoritativeOem,
    SemiconductorVendor,
    AsamStandard,
    VerifiedCommunity,
    UnverifiedCommunity
}

public class ResearchDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "ECU Architecture"; // ECU Architecture, Protocol, Pinout, Checksum, Memory Map
    public ResearchSourceAuthority Authority { get; set; } = ResearchSourceAuthority.AuthoritativeOem;
    public string SourceName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string License { get; set; } = "OEM Technical Service Information";
    public string TargetEcuFamily { get; set; } = "Magneti Marelli MJD";
    public string TargetVehicle { get; set; } = "Maruti Suzuki Swift DDiS";
    public string ContentSummary { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public DateTime IndexedUtc { get; set; } = DateTime.UtcNow;
}

public class KnowledgeGraphNode
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "ECU"; // OEM, Vehicle, Platform, Engine, ECU, MCU, Firmware, Calibration, CAN_ID, DID, DTC
    public Dictionary<string, string> Attributes { get; set; } = new();
}

public class KnowledgeGraphEdge
{
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string Relationship { get; set; } = "CONTAINS"; // CONTAINS, COMMUNICATES_VIA, CONTROLLED_BY, RUNS_ON, COVERS
}

public class GlobalSearchResult
{
    public string Category { get; set; } = string.Empty; // ECU, CAN ID, DTC, DID, DBC, Firmware, Research
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public double RelevanceScore { get; set; } = 1.0;
}

public interface IResearchKnowledgeService
{
    IReadOnlyList<ResearchDocument> GetDocuments(string? ecuFamily = null);
    IReadOnlyList<KnowledgeGraphNode> GetGraphNodes();
    IReadOnlyList<KnowledgeGraphEdge> GetGraphEdges();
    IReadOnlyList<GlobalSearchResult> SearchGlobal(string query);
    void IndexDocument(ResearchDocument doc);
}

public class ResearchKnowledgeService : IResearchKnowledgeService
{
    private readonly ConcurrentDictionary<string, ResearchDocument> _docs = new();
    private readonly List<KnowledgeGraphNode> _nodes = new();
    private readonly List<KnowledgeGraphEdge> _edges = new();
    private readonly object _lock = new();

    public ResearchKnowledgeService()
    {
        InitializeSampleKnowledgeGraph();
        InitializeSampleResearchDocs();
    }

    private void InitializeSampleKnowledgeGraph()
    {
        // Nodes: OEM -> Vehicle -> Platform -> Engine -> ECU -> MCU
        _nodes.Add(new KnowledgeGraphNode { Id = "OEM_VAG", Label = "Volkswagen AG", Type = "OEM" });
        _nodes.Add(new KnowledgeGraphNode { Id = "PLAT_MQB", Label = "Modularer Querbaukasten (MQB)", Type = "Platform" });
        _nodes.Add(new KnowledgeGraphNode { Id = "ENG_EA888", Label = "EA888 Gen 3 2.0 TSI", Type = "Engine" });
        _nodes.Add(new KnowledgeGraphNode { Id = "ECU_MED17", Label = "Bosch MED17.5.5", Type = "ECU", Attributes = new() { ["Part"] = "06K907425B", ["Baud"] = "500kbps" } });
        _nodes.Add(new KnowledgeGraphNode { Id = "MCU_TC1793", Label = "Infineon TriCore TC1793", Type = "MCU", Attributes = new() { ["Clock"] = "260MHz", ["Flash"] = "4MB", ["RAM"] = "192KB" } });
        _nodes.Add(new KnowledgeGraphNode { Id = "CAN_0x280", Label = "CAN ID 0x280 (ECM Engine Speed/Throttle)", Type = "CAN_ID", Attributes = new() { ["Rate"] = "100Hz", ["DLC"] = "8" } });
        _nodes.Add(new KnowledgeGraphNode { Id = "DID_0xF190", Label = "DID 0xF190 (VIN)", Type = "DID", Attributes = new() { ["Service"] = "0x22", ["Length"] = "17" } });
        _nodes.Add(new KnowledgeGraphNode { Id = "DTC_P0300", Label = "DTC P0300 (Random Misfire)", Type = "DTC" });

        // Edges
        _edges.Add(new KnowledgeGraphEdge { SourceId = "OEM_VAG", TargetId = "PLAT_MQB", Relationship = "ARCHITECTS" });
        _edges.Add(new KnowledgeGraphEdge { SourceId = "PLAT_MQB", TargetId = "ENG_EA888", Relationship = "MOUNTS" });
        _edges.Add(new KnowledgeGraphEdge { SourceId = "ENG_EA888", TargetId = "ECU_MED17", Relationship = "MANAGED_BY" });
        _edges.Add(new KnowledgeGraphEdge { SourceId = "ECU_MED17", TargetId = "MCU_TC1793", Relationship = "POWERED_BY" });
        _edges.Add(new KnowledgeGraphEdge { SourceId = "ECU_MED17", TargetId = "CAN_0x280", Relationship = "BROADCASTS" });
        _edges.Add(new KnowledgeGraphEdge { SourceId = "ECU_MED17", TargetId = "DID_0xF190", Relationship = "SUPPORTS_DID" });
        _edges.Add(new KnowledgeGraphEdge { SourceId = "ECU_MED17", TargetId = "DTC_P0300", Relationship = "LOGS_FAULT" });
    }

    private void InitializeSampleResearchDocs()
    {
        IndexDocument(new ResearchDocument
        {
            Title = "Bosch MED17.5 Engine Management Function Sheet & Calibration Guide",
            Category = "ECU Architecture",
            Authority = ResearchSourceAuthority.AuthoritativeOem,
            SourceName = "Bosch Automotive Aftermarket Technical Service",
            TargetEcuFamily = "Bosch MED17",
            TargetVehicle = "Volkswagen AG MQB",
            ContentSummary = "Comprehensive pinout, CAN communication matrix, torque monitoring architecture, and A2L characteristic mapping for MED17.5.x engine controllers.",
            Keywords = new() { "MED17", "Bosch", "Pinout", "Torque Model", "TriCore", "TC1793", "CAN 500k" }
        });

        IndexDocument(new ResearchDocument
        {
            Title = "Infineon TriCore TC1793 32-bit Microcontroller Hardware User's Manual",
            Category = "Semiconductor Manual",
            Authority = ResearchSourceAuthority.SemiconductorVendor,
            SourceName = "Infineon Technologies AG",
            TargetEcuFamily = "TriCore TC17xx",
            ContentSummary = "Internal flash memory organization, bootstrap loader (BSL) protocol, JTAG/DAP debug interfaces, and hardware CRC-32 accelerator architecture.",
            Keywords = new() { "Infineon", "TriCore", "TC1793", "Bootloader", "Flash", "DAP", "Memory Map" }
        });

        IndexDocument(new ResearchDocument
        {
            Title = "ASAM MCD-2 MC (ASAP2 / A2L) Specification Version 1.7.1",
            Category = "ASAM Standards",
            Authority = ResearchSourceAuthority.AsamStandard,
            SourceName = "Association for Standardisation of Automation and Measuring Systems",
            ContentSummary = "Formal syntax and grammar definition for ECU measurement and calibration data exchange files, record layouts, and computation rules.",
            Keywords = new() { "ASAM", "A2L", "MCD-2 MC", "Calibration", "XCP", "CompuMethod" }
        });
    }

    public IReadOnlyList<ResearchDocument> GetDocuments(string? ecuFamily = null)
    {
        if (string.IsNullOrWhiteSpace(ecuFamily))
            return _docs.Values.OrderBy(d => d.Title).ToList();

        return _docs.Values
            .Where(d => d.TargetEcuFamily.Contains(ecuFamily, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Title)
            .ToList();
    }

    public IReadOnlyList<KnowledgeGraphNode> GetGraphNodes() => _nodes;
    public IReadOnlyList<KnowledgeGraphEdge> GetGraphEdges() => _edges;

    public void IndexDocument(ResearchDocument doc)
    {
        if (doc == null) return;
        _docs[doc.Id] = doc;
    }

    public IReadOnlyList<GlobalSearchResult> SearchGlobal(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<GlobalSearchResult>();

        var q = query.Trim();
        var results = new List<GlobalSearchResult>();

        // Search Research Docs
        foreach (var d in _docs.Values)
        {
            if (d.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.ContentSummary.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.Keywords.Any(k => k.Contains(q, StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(new GlobalSearchResult
                {
                    Category = "Research & OEM Documentation",
                    Title = d.Title,
                    Subtitle = $"{d.Authority} • {d.Category}",
                    Id = d.Id,
                    RelevanceScore = 0.9
                });
            }
        }

        // Search Knowledge Graph
        foreach (var n in _nodes)
        {
            if (n.Label.Contains(q, StringComparison.OrdinalIgnoreCase) || n.Id.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new GlobalSearchResult
                {
                    Category = $"Knowledge Graph ({n.Type})",
                    Title = n.Label,
                    Subtitle = $"Node ID: {n.Id}",
                    Id = n.Id,
                    RelevanceScore = 0.95
                });
            }
        }

        return results.OrderByDescending(r => r.RelevanceScore).Take(50).ToList();
    }
}
