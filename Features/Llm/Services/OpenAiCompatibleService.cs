using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AdrienCoder.Api.Features.Llm.Models;
using Microsoft.Extensions.Options;

namespace AdrienCoder.Api.Features.Llm.Services;

public class OpenAiCompatibleService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiCompatibleOptions _options;
    private readonly LLMOptions _llmOptions;

    public OpenAiCompatibleService(
        HttpClient httpClient,
        IOptions<OpenAiCompatibleOptions> options,
        IOptions<LLMOptions> llmOptions)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _llmOptions = llmOptions.Value;

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("models");
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
                new { role = "system", content = _llmOptions.SystemPrompt },
                new { role = "user", content = question }
            },
            temperature = 0.2,
            max_tokens = 800
        };

        var response = await _httpClient.PostAsJsonAsync("chat/completions", payload);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"LLM error {(int)response.StatusCode}: {error}");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        return LlmResponseSanitizer.RemoveThinking(content);
    }

    public async Task<string> AskWithContextAsync(string question, string context)
    {
        var fullPrompt = $"""
        Voici le contexte du projet :

        {context}

        Question :
        {question}

        Réponds de façon structurée et concise.
        """;

        return await AskAsync(fullPrompt);
    }

    public async Task<string> GetModelsAsync()
    {
        var response = await _httpClient.GetAsync("models");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}
