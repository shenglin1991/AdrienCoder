using AdrienCoder.Server.Features.Ask.Services;
using AdrienCoder.Server.Features.Indexing.Services;
using AdrienCoder.Server.Features.Llm.Models;
using AdrienCoder.Server.Features.Llm.Services;
using AdrienCoder.Server.Features.Monitoring.Services;
using AdrienCoder.Server.Features.Vector.Models;
using AdrienCoder.Server.Features.Vector.Services;
using AdrienCoder.Server.Features.Workers;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace AdrienCoder.Server.Infrastructure;

/// <summary>
/// Keeps feature-specific configuration out of the application bootstrap file.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddLlmFeature(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LLMOptions>(configuration.GetSection("LLM"));
        services.Configure<OpenAiCompatibleOptions>(
            configuration.GetSection("OpenAICompatible"));
        services.Configure<OllamaOptions>(
            configuration.GetSection("Ollama"));

        services.AddHttpClient<OpenAiCompatibleService>((serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<OpenAiCompatibleOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<OllamaService>((serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<OllamaOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromMinutes(10);
        });

        services.AddSingleton<WorkerRegistry>();
        services.AddSingleton<GpuJobDispatcher>();
        services.AddSingleton<WorkerGpuLlmService>();
        services.AddScoped<LlmRouterService>();
        services.AddScoped<ILLMService>(serviceProvider =>
            serviceProvider.GetRequiredService<LlmRouterService>());

        return services;
    }

    public static IServiceCollection AddVectorFeature(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<QdrantOptions>(
            configuration.GetSection("Qdrant"));
        services.Configure<EmbeddingOptions>(
            configuration.GetSection("Embedding"));

        services.AddHttpClient<QdrantService>((serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<QdrantOptions>>()
                .Value;

            client.BaseAddress = new Uri(
                $"http://{options.Host}:{options.Port}/");
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            UseProxy = false
        });

        services.AddHttpClient<EmbeddingService>((serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<EmbeddingOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromMinutes(5);

            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", options.ApiKey);
            }
        });

        return services;
    }

    public static IServiceCollection AddIndexingFeature(
        this IServiceCollection services)
    {
        services.AddScoped<RepositoryIndexingService>();

        return services;
    }

    public static IServiceCollection AddAskFeature(
        this IServiceCollection services)
    {
        services.AddScoped<AskService>();
        return services;
    }

    public static IServiceCollection AddMonitoringFeature(
        this IServiceCollection services)
    {
        services.AddScoped<MonitoringService>();
        return services;
    }
}
