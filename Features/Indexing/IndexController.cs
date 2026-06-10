using AdrienCoder.Api.Features.Indexing.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Api.Features.Indexing;

[ApiController]
[Route("api/index")]
public class IndexController : ControllerBase
{
    private readonly RepositoryIndexingService _repositoryIndexingService;

    public IndexController(RepositoryIndexingService repositoryIndexingService)
    {
        _repositoryIndexingService = repositoryIndexingService;
    }

    [HttpPost("repo")]
    public IActionResult IndexRepository([FromBody] string repoPath)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            return BadRequest("RepoPath is required.");
        }

        try
        {
            var result = _repositoryIndexingService.IndexRepository(repoPath);

            return Ok(new
            {
                indexedFiles = result.Files.Count,
                updated = result.WasUpdated,
                message = result.WasUpdated
                    ? "Repository scanned and loaded."
                    : "Repository already up to date."
            });
        }
        catch (Exception exception) when (
            exception is DirectoryNotFoundException
            or InvalidOperationException)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var files = _repositoryIndexingService.GetIndexedFiles();

        return Ok(new
        {
            indexedFiles = files.Count,
            totalCharacters = files.Sum(file => file.Content.Length),
            files = files.Select(file => new
            {
                file.Path,
                file.LastModified,
                characters = file.Content.Length
            })
        });
    }
}
