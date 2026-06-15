using AdrienCoder.Server.Features.Llm.Models;
using AdrienCoder.Server.Features.Workers;
using Microsoft.Extensions.Options;

namespace AdrienCoder.Server.Features.Llm.Services;

public sealed class WorkerGpuLlmService : ILLMService
{
    private readonly GpuJobDispatcher _dispatcher;
    private readonly LLMOptions _options;

    public WorkerGpuLlmService(
        GpuJobDispatcher dispatcher,
        IOptions<LLMOptions> options)
    {
        _dispatcher = dispatcher;
        _options = options.Value;
    }

    public Task<bool> IsHealthyAsync()
    {
        return Task.FromResult(_dispatcher.IsAvailable);
    }

    public async Task<string> AskAsync(string question)
    {
        var prompt = $"""
        {_options.SystemPrompt}

        {question}
        """;

        var response = await _dispatcher.DispatchAsync(prompt);
        return LlmResponseSanitizer.RemoveThinking(response);
    }

    public Task<string> AskWithContextAsync(string question, string context)
    {
        return AskAsync($"""
        Reponds a la question en utilisant uniquement le contexte de code utile.

        <repository_context>
        {context}
        </repository_context>

        Question:
        {question}
        """);
    }

    public Task<string> GetModelsAsync()
    {
        return Task.FromResult("""{"provider":"WorkerGpu"}""");
    }
}
