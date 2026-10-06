using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using CanEngineeringPlatform.Core.Research;

namespace CanEngineeringPlatform.Api.Controllers;

[ApiController]
[Route("api/research")]
public class ResearchKnowledgeController : ControllerBase
{
    private readonly IResearchKnowledgeService _researchService;

    public ResearchKnowledgeController(IResearchKnowledgeService researchService)
    {
        _researchService = researchService ?? throw new ArgumentNullException(nameof(researchService));
    }

    [HttpGet("documents")]
    public ActionResult<IReadOnlyList<ResearchDocument>> GetDocuments([FromQuery] string? ecuFamily = null)
    {
        return Ok(_researchService.GetDocuments(ecuFamily));
    }

    [HttpGet("graph/nodes")]
    public ActionResult<IReadOnlyList<KnowledgeGraphNode>> GetGraphNodes()
    {
        return Ok(_researchService.GetGraphNodes());
    }

    [HttpGet("graph/edges")]
    public ActionResult<IReadOnlyList<KnowledgeGraphEdge>> GetGraphEdges()
    {
        return Ok(_researchService.GetGraphEdges());
    }

    [HttpGet("search")]
    public ActionResult<IReadOnlyList<GlobalSearchResult>> GlobalSearch([FromQuery] string query)
    {
        return Ok(_researchService.SearchGlobal(query));
    }

    [HttpPost("documents")]
    public ActionResult AddDocument([FromBody] ResearchDocument doc)
    {
        _researchService.IndexDocument(doc);
        return Ok(new { success = true, documentId = doc.Id });
    }
}
