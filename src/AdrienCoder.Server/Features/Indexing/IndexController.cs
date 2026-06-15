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
    [HttpPost("repo")]
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

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var activeIndex = await _repositoryIndexingService
            .GetActiveIndexAsync();

        return activeIndex is null
            ? NotFound(new { message = "No active repository index." })
            : Ok(activeIndex);
    }
}
