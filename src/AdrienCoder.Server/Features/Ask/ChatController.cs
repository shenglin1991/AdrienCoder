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
    }
}
