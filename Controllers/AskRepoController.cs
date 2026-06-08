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

    public AskRepoController(
    RepoScannerService repoScannerService,
    ILLMService llmService,
    RepoIndexStore repoIndexStore)
    {
        _repoScannerService = repoScannerService;
        _llmService = llmService;
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

        var answer = await _llmService.AskWithContextAsync(request.Question, context);

        return Ok(new AskResponse
        {
            Answer = answer
        });
    }
}