using AdrienCoder.Api.Features.Llm.Models;
using AdrienCoder.Api.Features.Llm.Services;
using AdrienCoder.Api.Features.Monitoring.Models;
using AdrienCoder.Api.Features.Vector.Services;
using Microsoft.Extensions.Options;

namespace AdrienCoder.Api.Features.Monitoring.Services;

/// <summary>
/// Aggregates infrastructure health and reports the active LLM provider.
/// </summary>
public class MonitoringService
{
    private readonly QdrantService _qdrantService;
    private readonly LlmRouterService _llmRouterService;
    private readonly OpenAiCompatibleOptions _openAiOptions;
    private readonly OllamaOptions _ollamaOptions;

    public MonitoringService(
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

    public async Task<AppStatusResponse> GetStatusAsync()
    {
        var qdrantOk = await _qdrantService.IsHealthyAsync();
        var activeProvider = await _llmRouterService.GetActiveProviderAsync();

        return new AppStatusResponse
        {
            Api = "ok",
            Qdrant = qdrantOk ? "ok" : "unavailable",
            Llm = activeProvider != "None" ? "ok" : "unavailable",
            ActiveProvider = activeProvider,
            Model = GetActiveModel(activeProvider),
            Time = DateTimeOffset.UtcNow
        };
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
