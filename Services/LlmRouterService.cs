using AdrienCoder.Api.Models;
using Microsoft.Extensions.Options;

namespace AdrienCoder.Api.Services;

public class LlmRouterService : ILLMService
{
    public const string OpenAiCompatibleProvider = "OpenAICompatible";
    public const string OllamaProvider = "Ollama";

    private readonly OpenAiCompatibleService _openAiService;
    private readonly OllamaService _ollamaService;
    private readonly LLMOptions _options;
    private readonly ILogger<LlmRouterService> _logger;

    public LlmRouterService(
        OpenAiCompatibleService openAiService,
        OllamaService ollamaService,
        IOptions<LLMOptions> options,
        ILogger<LlmRouterService> logger)
    {
        _openAiService = openAiService;
        _ollamaService = ollamaService;
        _options = options.Value;
        _logger = logger;
    }

    public Task<string> AskAsync(string question)
    {
        return ExecuteWithFallbackAsync(service => service.AskAsync(question));
    }

    public Task<string> AskWithContextAsync(string question, string context)
    {
        return ExecuteWithFallbackAsync(
            service => service.AskWithContextAsync(question, context));
    }

    public Task<string> GetModelsAsync()
    {
        return ExecuteWithFallbackAsync(service => service.GetModelsAsync());
    }

    public async Task<bool> IsHealthyAsync()
    {
        return await GetActiveProviderAsync() != "None";
    }

    public async Task<string> GetActiveProviderAsync()
    {
        foreach (var provider in GetProviders())
        {
            if (await provider.Service.IsHealthyAsync())
            {
                return provider.Name;
            }
        }

        return "None";
    }

    private async Task<string> ExecuteWithFallbackAsync(
        Func<ILLMService, Task<string>> operation)
    {
        Exception? lastException = null;

        foreach (var provider in GetProviders())
        {
            if (!await provider.Service.IsHealthyAsync())
            {
                continue;
            }

            try
            {
                return await operation(provider.Service);
            }
            catch (Exception exception)
            {
                lastException = exception;
                _logger.LogWarning(
                    exception,
                    "LLM provider {Provider} failed. Trying the fallback provider.",
                    provider.Name);
            }
        }

        throw new InvalidOperationException(
            "No configured LLM provider is available.",
            lastException);
    }

    private IEnumerable<(string Name, ILLMService Service)> GetProviders()
    {
        var providers = new[]
        {
            ResolveProvider(_options.PreferredProvider),
            ResolveProvider(_options.FallbackProvider)
        };

        return providers.DistinctBy(provider => provider.Name);
    }

    private (string Name, ILLMService Service) ResolveProvider(string provider)
    {
        if (provider.Equals(
            OpenAiCompatibleProvider,
            StringComparison.OrdinalIgnoreCase))
        {
            return (OpenAiCompatibleProvider, _openAiService);
        }

        if (provider.Equals(OllamaProvider, StringComparison.OrdinalIgnoreCase))
        {
            return (OllamaProvider, _ollamaService);
        }

        throw new InvalidOperationException(
            $"Unknown LLM provider '{provider}'.");
    }
}
