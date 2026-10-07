using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanEngineeringPlatform.Core.Projects;

public interface IProjectService
{
    EngineeringProject CreateProject(string name, string description = "", string createdBy = "Automotive Engineer");
    EngineeringProject? GetProject(string id);
    IReadOnlyList<EngineeringProject> GetAllProjects();
    EngineeringProject? GetActiveProject();
    bool SetActiveProject(string id);
    EngineeringProject UpdateProject(EngineeringProject project);
    bool DeleteProject(string id);
    ProjectEvidenceItem AddEvidence(string projectId, string category, string description, string source, byte[]? rawData = null);
    ProjectNote AddNote(string projectId, string title, string content, string category = "General", string author = "Engineer");
    string ExportProjectJson(string projectId);
    EngineeringProject ImportProjectJson(string json);
    string ComputeProjectHash(string projectId);
}

public class ProjectService : IProjectService
{
    private readonly ConcurrentDictionary<string, EngineeringProject> _projects = new();
    private string? _activeProjectId;
    private readonly object _lock = new();

    public ProjectService()
    {
        // Seed default OEM project
        var defaultProject = new EngineeringProject
        {
            Id = "proj-default-oem",
            Name = "Maruti Suzuki Swift DDiS Calibration & Diagnostics",
            Description = "Full bench and vehicle engineering workspace for Magneti Marelli MJD 6F3 / 8F3 ECM (Swift 1.3L DDiS MultiJet).",
            CreatedBy = "Antigravity Engineering System",
            Vehicle = new ProjectVehicleInfo
            {
                Vin = "MA3EFA12S00123456",
                Make = "Maruti Suzuki",
                Model = "Swift 1.3 DDiS (MJD 6F3/8F3)",
                Year = 2015,
                EngineCode = "D13A / MultiJet",
                Transmission = "5-Speed Manual",
                Architecture = "High-Speed CAN Gateway (500 kbps)"
            },
            EcuTarget = new ProjectEcuTarget
            {
                EcuName = "ECM",
                Family = "Magneti Marelli MJD",
                HardwareNumber = "MJD 6F3.S1 / 8F3",
                SoftwareNumber = "3422S167",
                RequestCanId = 0x7E0,
                ResponseCanId = 0x7E8,
                Protocol = "UDS on ISO-TP (11-bit)",
                McuFamily = "PowerPC MPC5565 / MPC5634",
                FlashSizeBytes = 2097152
            },
            Hardware = new ProjectHardwareConfig
            {
                PreferredTransportId = "simulation",
                NominalBaudRate = 500000,
                ListenOnly = false
            },
            Tags = new List<string> { "Maruti", "Swift", "Magneti Marelli", "MJD", "DDiS", "UDS" }
        };

        _projects[defaultProject.Id] = defaultProject;
        _activeProjectId = defaultProject.Id;
    }

    public EngineeringProject CreateProject(string name, string description = "", string createdBy = "Automotive Engineer")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name cannot be empty.", nameof(name));

        var project = new EngineeringProject
        {
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            CreatedBy = createdBy?.Trim() ?? "Automotive Engineer"
        };

        _projects[project.Id] = project;
        lock (_lock)
        {
            _activeProjectId = project.Id;
        }
        return project;
    }

    public EngineeringProject? GetProject(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        _projects.TryGetValue(id, out var proj);
        return proj;
    }

    public IReadOnlyList<EngineeringProject> GetAllProjects()
    {
        return _projects.Values.OrderByDescending(p => p.LastModifiedUtc).ToList();
    }

    public EngineeringProject? GetActiveProject()
    {
        lock (_lock)
        {
            if (_activeProjectId != null && _projects.TryGetValue(_activeProjectId, out var proj))
            {
                return proj;
            }
            return _projects.Values.FirstOrDefault();
        }
    }

    public bool SetActiveProject(string id)
    {
        if (!_projects.ContainsKey(id)) return false;
        lock (_lock)
        {
            _activeProjectId = id;
            return true;
        }
    }

    public EngineeringProject UpdateProject(EngineeringProject project)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));
        project.LastModifiedUtc = DateTime.UtcNow;
        _projects[project.Id] = project;
        return project;
    }

    public bool DeleteProject(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        bool removed = _projects.TryRemove(id, out _);
        lock (_lock)
        {
            if (_activeProjectId == id)
            {
                _activeProjectId = _projects.Keys.FirstOrDefault();
            }
        }
        return removed;
    }

    public ProjectEvidenceItem AddEvidence(string projectId, string category, string description, string source, byte[]? rawData = null)
    {
        var proj = GetProject(projectId) ?? throw new KeyNotFoundException($"Project '{projectId}' not found.");
        
        string sha256Hex = string.Empty;
        if (rawData != null && rawData.Length > 0)
        {
            using var sha = SHA256.Create();
            sha256Hex = BitConverter.ToString(sha.ComputeHash(rawData)).Replace("-", "").ToLowerInvariant();
        }

        var item = new ProjectEvidenceItem
        {
            Category = category ?? "General Evidence",
            Description = description ?? string.Empty,
            Source = source ?? "Hardware VCI",
            Sha256Hash = sha256Hex
        };

        proj.EvidenceRecords.Add(item);
        proj.LastModifiedUtc = DateTime.UtcNow;
        return item;
    }

    public ProjectNote AddNote(string projectId, string title, string content, string category = "General", string author = "Engineer")
    {
        var proj = GetProject(projectId) ?? throw new KeyNotFoundException($"Project '{projectId}' not found.");
        var note = new ProjectNote
        {
            Title = title ?? "Untitled Note",
            Content = content ?? string.Empty,
            Category = category ?? "General",
            Author = author ?? "Engineer"
        };
        proj.Notes.Add(note);
        proj.LastModifiedUtc = DateTime.UtcNow;
        return note;
    }

    public string ExportProjectJson(string projectId)
    {
        var proj = GetProject(projectId) ?? throw new KeyNotFoundException($"Project '{projectId}' not found.");
        proj.ProjectHash = ComputeProjectHash(projectId);
        return JsonSerializer.Serialize(proj, new JsonSerializerOptions { WriteIndented = true });
    }

    public EngineeringProject ImportProjectJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON cannot be empty.", nameof(json));
        var proj = JsonSerializer.Deserialize<EngineeringProject>(json) ?? throw new InvalidOperationException("Failed to deserialize project JSON.");
        proj.Id = Guid.NewGuid().ToString("N");
        proj.LastModifiedUtc = DateTime.UtcNow;
        _projects[proj.Id] = proj;
        return proj;
    }

    public string ComputeProjectHash(string projectId)
    {
        var proj = GetProject(projectId) ?? throw new KeyNotFoundException($"Project '{projectId}' not found.");
        var sb = new StringBuilder();
        sb.Append(proj.Id).Append('|')
          .Append(proj.Name).Append('|')
          .Append(proj.Vehicle.Vin).Append('|')
          .Append(proj.EcuTarget.HardwareNumber).Append('|')
          .Append(proj.EcuTarget.SoftwareNumber).Append('|')
          .Append(proj.EvidenceRecords.Count).Append('|')
          .Append(proj.Notes.Count);

        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
