using AdrienCoder.Server.Features.Llm.Models;
using AdrienCoder.Server.Features.Llm.Services;
using AdrienCoder.Server.Features.Monitoring.Models;
using AdrienCoder.Server.Features.Vector.Services;
using Microsoft.Extensions.Options;

namespace AdrienCoder.Server.Features.Monitoring.Services;

/// <summary>
/// Aggregates infrastructure health and reports the active LLM provider.
/// </summary>
public class MonitoringService
{
    private readonly QdrantService _qdrantService;
    private readonly EmbeddingService _embeddingService;
    private readonly LlmRouterService _llmRouterService;
    private readonly OpenAiCompatibleOptions _openAiOptions;
    private readonly OllamaOptions _ollamaOptions;

    public MonitoringService(
        QdrantService qdrantService,
        EmbeddingService embeddingService,
        LlmRouterService llmRouterService,
        IOptions<OpenAiCompatibleOptions> openAiOptions,
        IOptions<OllamaOptions> ollamaOptions)
    {
        _qdrantService = qdrantService;
        _embeddingService = embeddingService;
        _llmRouterService = llmRouterService;
        _openAiOptions = openAiOptions.Value;
        _ollamaOptions = ollamaOptions.Value;
    }

    public async Task<AppStatusResponse> GetStatusAsync()
    {
        var qdrantOk = await _qdrantService.IsHealthyAsync();
        var embeddingOk = await _embeddingService.IsHealthyAsync();
        var activeProvider = await _llmRouterService.GetActiveProviderAsync();

        return new AppStatusResponse
        {
            Api = "ok",
            Qdrant = qdrantOk ? "ok" : "unavailable",
            Embedding = embeddingOk ? "ok" : "unavailable",
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
            LlmRouterService.WorkerGpuProvider => "GPU worker",
            LlmRouterService.OpenAiCompatibleProvider => _openAiOptions.Model,
            LlmRouterService.OllamaProvider => _ollamaOptions.Model,
            _ => null
        };
    }
}
