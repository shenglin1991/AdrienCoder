using AdrienCoder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChunkRepoController : ControllerBase
{
    private readonly RepoIndexStore _repoIndexStore;
    private readonly CodeChunkerService _codeChunkerService;

    public ChunkRepoController(
        RepoIndexStore repoIndexStore,
        CodeChunkerService codeChunkerService)
    {
        _repoIndexStore = repoIndexStore;
        _codeChunkerService = codeChunkerService;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var files = _repoIndexStore.GetFiles();

        if (files.Count == 0)
        {
            return BadRequest("No repo indexed. Call /api/IndexRepo first.");
        }

        var chunks = _codeChunkerService.ChunkFiles(files);

        return Ok(new
        {
            indexedFiles = files.Count,
            chunks = chunks.Count,
            preview = chunks.Take(5).Select(c => new
            {
                c.Id,
                c.FilePath,
                c.ChunkIndex,
                characters = c.Content.Length,
                preview = c.Content[..Math.Min(200, c.Content.Length)]
            })
        });
    }
}