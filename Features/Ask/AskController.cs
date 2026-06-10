using AdrienCoder.Api.Features.Ask.Models;
using AdrienCoder.Api.Features.Ask.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Api.Features.Ask;

[ApiController]
[Route("api/ask")]
public class AskController : ControllerBase
{
    private readonly AskService _askService;

    public AskController(AskService askService)
    {
        _askService = askService;
    }

    [HttpPost]
    public async Task<ActionResult<AskResponse>> Ask(
        [FromBody] AskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required.");
        }

        var answer = await _askService.AskAsync(request.Question);
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

        try
        {
            var answer = await _askService.AskRepositoryAsync(request.Question);

            return Ok(new AskResponse { Answer = answer });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
