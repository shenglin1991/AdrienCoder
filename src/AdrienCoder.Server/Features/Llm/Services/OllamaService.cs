using System.Net.Http.Json;
using System.Runtime.CompilerServices;
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
            stream = false,
            options = new
            {
                num_predict = _llmOptions.MaxOutputTokens
            }
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

    public async IAsyncEnumerable<string> StreamAskAsync(
        string question,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
            stream = true,
            options = new
            {
                num_predict = _llmOptions.MaxOutputTokens
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = JsonContent.Create(payload)
        };
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            if (document.RootElement.TryGetProperty("done", out var done)
                && done.GetBoolean())
            {
                yield break;
            }

            if (!document.RootElement.TryGetProperty("message", out var message)
                || !message.TryGetProperty("content", out var contentElement))
            {
                continue;
            }

            var content = contentElement.GetString();
            if (!string.IsNullOrEmpty(content))
            {
                yield return content;
            }
        }
    }

    public IAsyncEnumerable<string> StreamAskWithContextAsync(
        string question,
        string context,
        CancellationToken cancellationToken = default)
    {
        var fullPrompt = $"""
        Tu vas repondre a une question sur un projet de code.

        Voici les fichiers du projet :

        {context}

        Question :
        {question}

        Reponds de facon structuree et concise.
        """;

        return StreamAskAsync(fullPrompt, cancellationToken);
    }
}
