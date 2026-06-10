using AdrienCoder.Api.Models;
using AdrienCoder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Api.Controllers;

[ApiController]
[Route("api/ask")]
public class AskController : ControllerBase
{
    private readonly ILLMService _llmService;
    private readonly RepositoryIndexingService _repositoryIndexingService;

    public AskController(
        ILLMService llmService,
        RepositoryIndexingService repositoryIndexingService)
    {
        _llmService = llmService;
        _repositoryIndexingService = repositoryIndexingService;
    }

    [HttpPost]
    public async Task<ActionResult<AskResponse>> Ask(
        [FromBody] AskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required.");
        }

        var answer = await _llmService.AskAsync(request.Question);
        return Ok(new AskResponse { Answer = answer });
    }

    [HttpPost("repo")]
    public async Task<ActionResult<AskResponse>> AskRepository(
        [FromBody] AskRepoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RepoPath))
        {
            return BadRequest("RepoPath is required.");
        }

        try
        {
            var context = await _repositoryIndexingService.BuildContextAsync(
                request.RepoPath,
                request.Question);

            var answer = await _llmService.AskWithContextAsync(
                request.Question,
                context);

            return Ok(new AskResponse { Answer = answer });
        }
        catch (Exception exception) when (
            exception is DirectoryNotFoundException
            or InvalidOperationException)
        {
            return BadRequest(exception.Message);
        }
    }
}
