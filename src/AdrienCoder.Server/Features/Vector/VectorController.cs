using AdrienCoder.Server.Features.Indexing.Services;
using AdrienCoder.Server.Features.Vector.Models;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Server.Features.Vector;

[ApiController]
[Route("api/vector")]
public sealed class VectorController : ControllerBase
{
    private readonly RepositoryIndexingService _repositoryIndexingService;

    public VectorController(RepositoryIndexingService repositoryIndexingService)
    {
        _repositoryIndexingService = repositoryIndexingService;
    }

    [HttpGet("chunks/qdrant")]
    public async Task<IActionResult> GetQdrantChunks(
        [FromQuery] int limit = 50,
        [FromQuery] string? offset = null)
    {
        if (limit is < 1 or > 200)
        {
            return BadRequest("Limit must be between 1 and 200.");
        }

        var page = await _repositoryIndexingService.GetStoredChunksAsync(
            limit,
            offset);

        return page is null
            ? NotFound("No active vector index found in Qdrant.")
            : Ok(page);
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search(
        [FromBody] VectorSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required.");
        }

        return Ok(await _repositoryIndexingService.SearchAsync(
            request.Question,
            request.Limit));
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await _repositoryIndexingService.GetVectorStatusAsync();
        return Ok(new { status });
    }
}
