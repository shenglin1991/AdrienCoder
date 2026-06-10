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
    private readonly LlmRouterService _llmRouterService;
    private readonly OpenAiCompatibleOptions _openAiOptions;
    private readonly OllamaOptions _ollamaOptions;

    public StatusController(
        QdrantService qdrantService,
        LlmRouterService llmRouterService,
        IOptions<OpenAiCompatibleOptions> openAiOptions,
        IOptions<OllamaOptions> ollamaOptions)
    {
        _qdrantService = qdrantService;
        _llmRouterService = llmRouterService;
        _openAiOptions = openAiOptions.Value;
        _ollamaOptions = ollamaOptions.Value;
    }

    [HttpGet]
    public async Task<ActionResult<AppStatusResponse>> Get()
    {
        var qdrantOk = await _qdrantService.IsHealthyAsync();
        var activeProvider = await _llmRouterService.GetActiveProviderAsync();
        var llmOk = activeProvider != "None";

        return Ok(new AppStatusResponse
        {
            Api = "ok",
            Qdrant = qdrantOk ? "ok" : "unavailable",
            Llm = llmOk ? "ok" : "unavailable",
            ActiveProvider = activeProvider,
            Model = GetActiveModel(activeProvider),
            Time = DateTimeOffset.UtcNow
        });
    }

    private string? GetActiveModel(string activeProvider)
    {
        return activeProvider switch
        {
            LlmRouterService.OpenAiCompatibleProvider => _openAiOptions.Model,
            LlmRouterService.OllamaProvider => _ollamaOptions.Model,
            _ => null
        };
    }
}
