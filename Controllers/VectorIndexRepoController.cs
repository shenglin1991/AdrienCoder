using AdrienCoder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VectorIndexRepoController : ControllerBase
{
    private readonly RepoIndexStore _repoIndexStore;
    private readonly CodeChunkerService _codeChunkerService;
    private readonly QdrantService _qdrantService;
    private readonly EmbeddingService _embeddingService;

    public VectorIndexRepoController(
        RepoIndexStore repoIndexStore,
        CodeChunkerService codeChunkerService,
        QdrantService qdrantService,
        EmbeddingService embeddingService)
    {
        _repoIndexStore = repoIndexStore;
        _codeChunkerService = codeChunkerService;
        _qdrantService = qdrantService;
        _embeddingService = embeddingService;
    }

    [HttpPost]
    public async Task<IActionResult> Post()
    {
        var files = _repoIndexStore.GetFiles();

        if (files.Count == 0)
        {
            return BadRequest("No repo indexed. Call /api/IndexRepo first.");
        }

        var chunks = _codeChunkerService.ChunkFiles(files);

        await _qdrantService.UpsertChunksAsync(chunks, _embeddingService);

        return Ok(new
        {
            indexedFiles = files.Count,
            chunks = chunks.Count,
            message = "Chunks indexed in Qdrant."
        });
    }
}