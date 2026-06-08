using AdrienCoder.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AdrienCoder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VectorStatusController : ControllerBase
{
    private readonly QdrantService _qdrantService;

    public VectorStatusController(QdrantService qdrantService)
    {
        _qdrantService = qdrantService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var status = await _qdrantService.GetStatusAsync();

        return Ok(new
        {
            status
        });
    }
}