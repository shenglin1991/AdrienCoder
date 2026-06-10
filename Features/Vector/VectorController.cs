using AdrienCoder.Api.Features.Indexing.Services;
using AdrienCoder.Api.Features.Vector.Models;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Api.Features.Vector;

[ApiController]
[Route("api/vector")]
public class VectorController : ControllerBase
{
    private readonly RepositoryIndexingService _repositoryIndexingService;

    public VectorController(RepositoryIndexingService repositoryIndexingService)
    {
        _repositoryIndexingService = repositoryIndexingService;
    }

    [HttpGet("chunks")]
    public IActionResult GetChunks()
    {
        try
        {
            var files = _repositoryIndexingService.GetIndexedFiles();
            var chunks = _repositoryIndexingService.GetChunks();

            return Ok(new
            {
                indexedFiles = files.Count,
                chunks = chunks.Count,
                preview = chunks.Take(5).Select(chunk => new
                {
                    chunk.Id,
                    chunk.FilePath,
                    chunk.ChunkIndex,
                    characters = chunk.Content.Length,
                    preview = chunk.Content[..Math.Min(200, chunk.Content.Length)]
                })
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("index")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var result = await _repositoryIndexingService.IndexVectorsAsync();

            return Ok(new
            {
                indexedFiles = result.IndexedFiles,
                chunks = result.Chunks,
                message = "Chunks indexed in Qdrant."
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search(
        [FromBody] VectorSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required.");
        }

        var result = await _repositoryIndexingService.SearchAsync(
            request.Question,
            request.Limit);

        return Ok(result);
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await _repositoryIndexingService.GetVectorStatusAsync();
        return Ok(new { status });
    }
}
