using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Projects;

namespace CanEngineeringPlatform.Api.Controllers;

public class CreateProjectDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = "Lead Automotive Engineer";
}

public class AddEvidenceDto
{
    public string Category { get; set; } = "CAN Capture";
    public string Description { get; set; } = string.Empty;
    public string Source { get; set; } = "Hardware VCI";
    public string? RawDataBase64 { get; set; }
}

public class AddNoteDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Author { get; set; } = "Engineer";
}

[ApiController]
[Route("api/projects")]
public class ProjectController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectController(IProjectService projectService)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<EngineeringProject>> GetAllProjects()
    {
        return Ok(_projectService.GetAllProjects());
    }

    [HttpGet("active")]
    public ActionResult<EngineeringProject> GetActiveProject()
    {
        var active = _projectService.GetActiveProject();
        if (active == null) return NotFound("No active project.");
        return Ok(active);
    }

    [HttpGet("{id}")]
    public ActionResult<EngineeringProject> GetProjectById(string id)
    {
        var proj = _projectService.GetProject(id);
        if (proj == null) return NotFound($"Project '{id}' not found.");
        return Ok(proj);
    }

    [HttpPost]
    public ActionResult<EngineeringProject> CreateProject([FromBody] CreateProjectDto dto)
    {
        var proj = _projectService.CreateProject(dto.Name, dto.Description, dto.CreatedBy);
        return Ok(proj);
    }

    [HttpPost("{id}/activate")]
    public ActionResult SetActiveProject(string id)
    {
        bool success = _projectService.SetActiveProject(id);
        if (!success) return NotFound($"Project '{id}' not found.");
        return Ok(new { success = true, activeProjectId = id });
    }

    [HttpPut("{id}")]
    public ActionResult<EngineeringProject> UpdateProject(string id, [FromBody] EngineeringProject project)
    {
        if (project == null || project.Id != id) return BadRequest("Project ID mismatch.");
        return Ok(_projectService.UpdateProject(project));
    }

    [HttpDelete("{id}")]
    public ActionResult DeleteProject(string id)
    {
        bool deleted = _projectService.DeleteProject(id);
        if (!deleted) return NotFound($"Project '{id}' not found.");
        return Ok(new { success = true });
    }

    [HttpPost("{id}/evidence")]
    public ActionResult<ProjectEvidenceItem> AddEvidence(string id, [FromBody] AddEvidenceDto dto)
    {
        byte[]? raw = null;
        if (!string.IsNullOrEmpty(dto.RawDataBase64))
        {
            try { raw = Convert.FromBase64String(dto.RawDataBase64); } catch { }
        }

        var item = _projectService.AddEvidence(id, dto.Category, dto.Description, dto.Source, raw);
        return Ok(item);
    }

    [HttpPost("{id}/notes")]
    public ActionResult<ProjectNote> AddNote(string id, [FromBody] AddNoteDto dto)
    {
        var note = _projectService.AddNote(id, dto.Title, dto.Content, dto.Category, dto.Author);
        return Ok(note);
    }

    [HttpGet("{id}/export")]
    public ActionResult<string> ExportJson(string id)
    {
        string json = _projectService.ExportProjectJson(id);
        return Content(json, "application/json");
    }

    [HttpPost("import")]
    public ActionResult<EngineeringProject> ImportJson([FromBody] string json)
    {
        var proj = _projectService.ImportProjectJson(json);
        return Ok(proj);
    }
}
