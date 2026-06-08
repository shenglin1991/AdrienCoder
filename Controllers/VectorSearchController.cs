using AdrienCoder.Api.Models;
using AdrienCoder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VectorSearchController : ControllerBase
{
    private readonly QdrantService _qdrantService;
    private readonly EmbeddingService _embeddingService;

    public VectorSearchController(
        QdrantService qdrantService,
        EmbeddingService embeddingService)
    {
        _qdrantService = qdrantService;
        _embeddingService = embeddingService;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] VectorSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required.");
        }

        var result = await _qdrantService.SearchAsync(
            request.Question,
            request.Limit,
            _embeddingService);

        return Content(result, "application/json");
    }
}