using AdrienCoder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IndexStatusController : ControllerBase
{
    private readonly RepoIndexStore _repoIndexStore;

    public IndexStatusController(RepoIndexStore repoIndexStore)
    {
        _repoIndexStore = repoIndexStore;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var files = _repoIndexStore.GetFiles();

        return Ok(new
        {
            indexedFiles = files.Count,
            totalCharacters = files.Sum(f => f.Content.Length),
            files = files.Select(f => new
            {
                f.Path,
                f.LastModified,
                characters = f.Content.Length
            })
        });
    }
}