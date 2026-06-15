using System.Net.Http.Json;
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
            false);

        using var response = await _httpClient.PostAsJsonAsync(
            "api/chat",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
            cancellationToken: cancellationToken);

        return result?.Message?.Content
            ?? throw new InvalidOperationException(
                "Ollama returned a response without message content.");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private sealed record OllamaChatRequest(
        string Model,
        IReadOnlyList<OllamaChatMessage> Messages,
        bool Stream);

    private sealed record OllamaChatMessage(string Role, string Content);

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")] OllamaChatMessage? Message);
}
