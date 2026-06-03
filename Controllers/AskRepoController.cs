using AdrienCoder.Api.Models;
using AdrienCoder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AskRepoController : ControllerBase
{
    private readonly RepoScannerService _repoScannerService;
    private readonly OllamaService _ollamaService;
    private readonly RepoIndexStore _repoIndexStore;

    public AskRepoController(
    RepoScannerService repoScannerService,
    OllamaService ollamaService,
    RepoIndexStore repoIndexStore)
    {
        _repoScannerService = repoScannerService;
        _ollamaService = ollamaService;
        _repoIndexStore = repoIndexStore;
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

        var indexedFiles = _repoIndexStore.GetFiles();

        if (indexedFiles.Count == 0)
        {
            return BadRequest("No repo indexed. Call /api/IndexRepo first.");
        }

        var context = _repoScannerService.BuildContextFromIndex(indexedFiles, request.Question);

        var answer = await _ollamaService.AskWithContextAsync(request.Question, context);

        return Ok(new AskResponse
        {
            Answer = answer
        });
    }
}