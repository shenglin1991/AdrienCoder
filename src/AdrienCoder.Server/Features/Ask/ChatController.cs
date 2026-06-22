using AdrienCoder.Contracts.Chat;
using AdrienCoder.Server.Features.Ask.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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

    [HttpPost("context")]
    public async Task<ActionResult<ChatContextDebugResponse>> GetContext(
        [FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question is required.");
        }

        try
        {
            var (index, chunks) = await _askService.GetDebugContextAsync(
                request.Question,
                request.RepositoryName);

            return Ok(new ChatContextDebugResponse(
                index.RepositoryPath,
                index.RepositorySignature,
                chunks
                    .Select(chunk => new ChatContextChunk(
                        chunk.FilePath,
                        chunk.ChunkIndex,
                        chunk.Score,
                        chunk.Content))
                    .ToList()));
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
                    message = "The embedding backend rejected the request.",
                    detail = exception.Message
                });
        }
    }

    [HttpPost("stream")]
    public async Task StreamChat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync("Question is required.");
            return;
        }

        try
        {
            var stream = await _askService.StreamAskRepositoryAsync(
                request.Question,
                request.RepositoryName,
                HttpContext.RequestAborted);

            await WriteStreamAsync(stream);
        }
        catch (InvalidOperationException exception)
        {
            await WriteErrorAsync(StatusCodes.Status400BadRequest, exception);
        }
        catch (HttpRequestException exception)
        {
            await WriteErrorAsync(StatusCodes.Status502BadGateway, exception);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await WriteErrorAsync(StatusCodes.Status502BadGateway, exception);
        }
    }

    [HttpPost("ask/stream")]
    public async Task StreamAsk([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync("Question is required.");
            return;
        }

        try
        {
            await WriteStreamAsync(_askService.StreamAskAsync(
                request.Question,
                HttpContext.RequestAborted));
        }
        catch (HttpRequestException exception)
        {
            await WriteErrorAsync(StatusCodes.Status502BadGateway, exception);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await WriteErrorAsync(StatusCodes.Status502BadGateway, exception);
        }
    }

    private async Task WriteStreamAsync(IAsyncEnumerable<string> stream)
    {
        Response.ContentType = "application/x-ndjson; charset=utf-8";

        await foreach (var delta in stream.WithCancellation(
            HttpContext.RequestAborted))
        {
            await WriteChunkAsync(new ChatStreamChunk(delta));
        }

        await WriteChunkAsync(new ChatStreamChunk(string.Empty, Done: true));
    }

    private async Task WriteErrorAsync(int statusCode, Exception exception)
    {
        if (!Response.HasStarted)
        {
            Response.StatusCode = statusCode;
            Response.ContentType = "application/json; charset=utf-8";
            await Response.WriteAsJsonAsync(new
            {
                message = "The chat backend rejected the request.",
                detail = exception.Message
            });
            return;
        }

        await WriteChunkAsync(new ChatStreamChunk(
            string.Empty,
            Done: true,
            Error: exception.Message));
    }

    private async Task WriteChunkAsync(ChatStreamChunk chunk)
    {
        await Response.WriteAsync(JsonSerializer.Serialize(chunk));
        await Response.WriteAsync("\n");
        await Response.Body.FlushAsync();
    }
}
