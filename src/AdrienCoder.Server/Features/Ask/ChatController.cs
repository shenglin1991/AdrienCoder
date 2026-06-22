using AdrienCoder.Contracts.Chat;
using AdrienCoder.Server.Features.Ask.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Server.Features.Ask;

[ApiController]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
    private readonly AskService _askService;

    public ChatController(AskService askService)
    {
        _askService = askService;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Chat(
        [FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required.");
        }

        try
        {
            var answer = await _askService.AskRepositoryAsync(
                request.Question,
                request.RepositoryName);
            return Ok(new ChatResponse(answer));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (HttpRequestException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    message = "The embedding or LLM backend rejected the request.",
                    detail = exception.Message
                });
        }
    }

    [HttpPost("ask")]
    public async Task<ActionResult<ChatResponse>> Ask(
        [FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required.");
        }

        try
        {
            var answer = await _askService.AskAsync(request.Question);
            return Ok(new ChatResponse(answer));
        }
        catch (HttpRequestException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    message = "The LLM backend rejected the request.",
                    detail = exception.Message
                });
        }
    }
}
