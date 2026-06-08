using AdrienCoder.Api.Models;
using AdrienCoder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AskRepoController : ControllerBase
{
    private readonly RepoScannerService _repoScannerService;
    private readonly ILLMService _llmService;
    private readonly RepoIndexStore _repoIndexStore;
    private readonly QdrantService _qdrantService;
    private readonly EmbeddingService _embeddingService;

    public AskRepoController(
    RepoScannerService repoScannerService,
    ILLMService llmService,
    RepoIndexStore repoIndexStore,
    QdrantService qdrantService,
    EmbeddingService embeddingService)
    {
        _repoScannerService = repoScannerService;
        _llmService = llmService;
        _repoIndexStore = repoIndexStore;
        _qdrantService = qdrantService;
        _embeddingService = embeddingService;
    }

    [HttpPost]
    public async Task<ActionResult<AskResponse>> AskRepo([FromBody] AskRepoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RepoPath))
        {
            return BadRequest("RepoPath is required.");
        }

        var chunks = await _qdrantService.SearchAsync(request.Question, 5, _embeddingService);

        if (chunks.Count == 0)
        {
            return BadRequest("No relevant chunks found. Call /api/VectorIndexRepo first.");
        }

        var context = string.Join("\n\n", chunks.Select(c => $"""
        --- FILE: {c.FilePath} | CHUNK: {c.ChunkIndex} | SCORE: {c.Score} ---
        {c.Content}
        """));

        var answer = await _llmService.AskWithContextAsync(request.Question, context);

        return Ok(new AskResponse
        {
            Answer = answer
        });
    }
}