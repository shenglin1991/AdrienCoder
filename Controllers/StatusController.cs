using AdrienCoder.Api.Models;
using AdrienCoder.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AdrienCoder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly QdrantService _qdrantService;
    private readonly ILLMService _llmService;
    private readonly LLMOptions _llmOptions;

    public StatusController(
        QdrantService qdrantService,
        ILLMService llmService,
        IOptions<LLMOptions> llmOptions)
    {
        _qdrantService = qdrantService;
        _llmService = llmService;
        _llmOptions = llmOptions.Value;
    }

    [HttpGet]
    public async Task<ActionResult<AppStatusResponse>> Get()
    {
        return Ok(new
        {
            Api = "ok",
            Test = "controller"
        });
    }
}