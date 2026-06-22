using AdrienCoder.Server.Features.Llm.Models;
using AdrienCoder.Server.Features.Llm.Services;
using AdrienCoder.Server.Features.Monitoring.Models;
using AdrienCoder.Server.Features.Vector.Models;
using AdrienCoder.Server.Features.Vector.Services;
using AdrienCoder.Server.Features.Workers;
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
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly WorkerRegistry _workerRegistry;

    public MonitoringService(
        QdrantService qdrantService,
        EmbeddingService embeddingService,
        LlmRouterService llmRouterService,
        IOptions<OpenAiCompatibleOptions> openAiOptions,
        IOptions<OllamaOptions> ollamaOptions,
        IOptions<EmbeddingOptions> embeddingOptions,
        WorkerRegistry workerRegistry)
    {
        _qdrantService = qdrantService;
        _embeddingService = embeddingService;
        _llmRouterService = llmRouterService;
        _openAiOptions = openAiOptions.Value;
        _ollamaOptions = ollamaOptions.Value;
        _embeddingOptions = embeddingOptions.Value;
        _workerRegistry = workerRegistry;
    }

    public async Task<AppStatusResponse> GetStatusAsync()
    {
        var qdrantOk = await _qdrantService.IsHealthyAsync();
        var embeddingOk = await _embeddingService.IsHealthyAsync();
        var activeProvider = await _llmRouterService.GetActiveProviderAsync();
        var activeIndex = qdrantOk
            ? await _qdrantService.GetActiveIndexAsync()
            : null;
        var workers = _workerRegistry.GetStatuses();
        var healthyWorkers = workers
            .Where(worker => worker.Healthy)
            .ToList();

        return new AppStatusResponse
        {
            Api = "ok",
            Qdrant = qdrantOk ? "ok" : "unavailable",
            Embedding = embeddingOk ? "ok" : "unavailable",
            Llm = activeProvider != "None" ? "ok" : "unavailable",
            ActiveProvider = activeProvider,
            Model = GetActiveModel(activeProvider),
            ActiveRepository = activeIndex?.RepositoryPath,
            ActiveRepositorySignature = activeIndex?.RepositorySignature,
            ActiveRepositoryChunks = activeIndex?.ChunkCount,
            LastIndexRepository = activeIndex?.RepositoryPath,
            LastIndexSignature = activeIndex?.RepositorySignature,
            LastIndexChunks = activeIndex?.ChunkCount,
            EmbeddingBackend = _embeddingOptions.ApiFormat,
            EmbeddingModel = _embeddingOptions.Model,
            WorkersConnected = workers.Count,
            WorkersHealthy = healthyWorkers.Count,
            WorkerModel = healthyWorkers.FirstOrDefault()?.Model
                ?? workers.FirstOrDefault()?.Model,
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
