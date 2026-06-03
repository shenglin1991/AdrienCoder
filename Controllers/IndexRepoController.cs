using AdrienCoder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IndexRepoController : ControllerBase
{
    private readonly RepoScannerService _repoScannerService;
    private readonly RepoIndexStore _repoIndexStore;

    public IndexRepoController(
        RepoScannerService repoScannerService,
        RepoIndexStore repoIndexStore)
    {
        _repoScannerService = repoScannerService;
        _repoIndexStore = repoIndexStore;
    }

    [HttpPost]
    public IActionResult Index([FromBody] string repoPath)
    {
        var files = _repoScannerService.IndexRepo(repoPath);

        _repoIndexStore.SetFiles(files);

        return Ok(new
        {
            indexedFiles = files.Count
        });
    }
}