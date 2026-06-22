using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdrienCoder.WorkerGpu.Configuration;
using Microsoft.Extensions.Options;

namespace AdrienCoder.WorkerGpu.Services;

public sealed class OllamaClient : ILocalLlmClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;

    public OllamaClient(IOptions<OllamaOptions> options)
    {
        _options = options.Value;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_options.BaseUrl, UriKind.Absolute),
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public async Task<string> ChatAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        var request = new OllamaChatRequest(
            _options.Model,
            [new OllamaChatMessage("user", prompt)],
            false,
            new OllamaChatOptions(_options.NumPredict));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = JsonContent.Create(request)
        };
        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
            cancellationToken: cancellationToken);

        return result?.Message?.Content
            ?? throw new InvalidOperationException(
                "Ollama returned a response without message content.");
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new OllamaChatRequest(
            _options.Model,
            [new OllamaChatMessage("user", prompt)],
            true,
            new OllamaChatOptions(_options.NumPredict));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = JsonContent.Create(request)
        };
        using var response = await _httpClient.SendAsync(
            httpRequest,
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

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private sealed record OllamaChatRequest(
        string Model,
        IReadOnlyList<OllamaChatMessage> Messages,
        bool Stream,
        OllamaChatOptions Options);

    private sealed record OllamaChatOptions(
        [property: JsonPropertyName("num_predict")] int NumPredict);

    private sealed record OllamaChatMessage(string Role, string Content);

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")] OllamaChatMessage? Message);
}
