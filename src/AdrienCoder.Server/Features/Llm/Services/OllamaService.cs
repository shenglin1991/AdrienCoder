using System.Net.Http.Json;
using System.Text.Json;
using AdrienCoder.Server.Features.Llm.Models;
using Microsoft.Extensions.Options;

namespace AdrienCoder.Server.Features.Llm.Services;

public class OllamaService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly LLMOptions _llmOptions;

    public OllamaService(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        IOptions<LLMOptions> llmOptions)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _llmOptions = llmOptions.Value;
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> AskAsync(string question)
    {
        var payload = new
        {
            model = _options.Model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = _llmOptions.SystemPrompt
                },
                new
                {
                    role = "user",
                    content = question
                }
            },
            stream = false
        };

        var response = await _httpClient.PostAsJsonAsync("api/chat", payload);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var content = doc.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        return LlmResponseSanitizer.RemoveThinking(content);
    }

    public async Task<string> GetModelsAsync()
    {
        var response = await _httpClient.GetAsync("api/tags");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<string> AskWithContextAsync(string question, string context)
    {
        var fullPrompt = $"""
        Tu vas repondre a une question sur un projet de code.

        Voici les fichiers du projet :

        {context}

        Question :
        {question}

        Reponds de facon structuree et concise.
        """;

        return await AskAsync(fullPrompt);
    }
}
