using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AdrienCoder.Api.Models;
using Microsoft.Extensions.Options;

namespace AdrienCoder.Api.Services;

public class OpenAiCompatibleService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly LLMOptions _options;

    public OpenAiCompatibleService(HttpClient httpClient, IOptions<LLMOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    public async Task<string> AskAsync(string question)
    {
        var payload = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = _options.SystemPrompt },
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

        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
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