using AdrienCoder.Contracts.Indexing;
using AdrienCoder.Server.Features.Indexing.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Server.Features.Indexing;

[ApiController]
[Route("api/index")]
public sealed class IndexController : ControllerBase
{
    private readonly RepositoryIndexingService _repositoryIndexingService;

    public IndexController(RepositoryIndexingService repositoryIndexingService)
    {
        _repositoryIndexingService = repositoryIndexingService;
    }

    [HttpPost]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<ActionResult<IndexRepositoryResponse>> IndexRepository(
        [FromBody] IndexRepositoryRequest request)
    {
        try
        {
            return Ok(await _repositoryIndexingService.IndexAsync(request));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("batch")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<ActionResult<IndexRepositoryResponse>> IndexRepositoryBatch(
        [FromBody] IndexRepositoryBatchRequest request)
    {
        try
        {
            return Ok(await _repositoryIndexingService.IndexBatchAsync(request));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("commit")]
    public async Task<ActionResult<IndexRepositoryResponse>> CommitRepository(
        [FromBody] IndexRepositoryCommitRequest request)
    {
        try
        {
            return Ok(await _repositoryIndexingService.CommitIndexAsync(request));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("check")]
    public async Task<ActionResult<IndexRepositoryResponse>> CheckRepository(
        [FromBody] IndexRepositoryCheckRequest request)
    {
        try
        {
            return Ok(await _repositoryIndexingService.CheckIndexAsync(request));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var activeIndex = await _repositoryIndexingService
            .GetActiveIndexAsync();

        return activeIndex is null
            ? NotFound(new { message = "No active repository index." })
            : Ok(activeIndex);
    }

    [HttpGet("chunks")]
    public async Task<IActionResult> GetChunks(
        [FromQuery] int limit = 50,
        [FromQuery] string? offset = null)
    {
        if (limit is < 1 or > 200)
        {
            return BadRequest("Limit must be between 1 and 200.");
        }

        try
        {
            var page = await _repositoryIndexingService.GetStoredChunksAsync(
                limit,
                offset);

            return page is null
                ? NotFound(new { message = "No active repository index." })
                : Ok(page);
        }
        catch (HttpRequestException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new
                {
                    message = "Qdrant rejected the chunk page request.",
                    detail = exception.Message
                });
        }
    }
}
