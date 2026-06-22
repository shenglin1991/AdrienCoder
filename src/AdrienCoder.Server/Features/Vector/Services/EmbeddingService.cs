using System.Net.Http.Json;
using System.Text.Json;
using AdrienCoder.Server.Features.Vector.Models;
using Microsoft.Extensions.Options;

namespace AdrienCoder.Server.Features.Vector.Services;

/// <summary>
/// Client for the embeddings endpoint used by Qdrant indexing and search.
/// </summary>
public class EmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingOptions _options;

    public EmbeddingService(HttpClient httpClient, IOptions<EmbeddingOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<float[]> EmbedAsync(string text)
    {
        var vector = _options.ApiFormat.Equals(
            "OpenAICompatible",
            StringComparison.OrdinalIgnoreCase)
            ? await EmbedOpenAiCompatibleAsync(text)
            : await EmbedOllamaAsync(text);

        if (vector.Length != _options.VectorSize)
        {
            throw new InvalidOperationException(
                $"Embedding model '{_options.Model}' returned "
                + $"{vector.Length} dimensions, but Embedding:VectorSize "
                + $"is configured to {_options.VectorSize}.");
        }

        return vector;
    }

    private async Task<float[]> EmbedOllamaAsync(string text)
    {
        var payload = new
        {
            model = _options.Model,
            prompt = text
        };

        var response = await _httpClient.PostAsJsonAsync("api/embeddings", payload);
        await EnsureEmbeddingSuccessAsync(response);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return doc.RootElement
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(x => x.GetSingle())
            .ToArray();
    }

    private async Task<float[]> EmbedOpenAiCompatibleAsync(string text)
    {
        var payload = new
        {
            model = _options.Model,
            input = text
        };

        var response = await _httpClient.PostAsJsonAsync("embeddings", payload);
        await EnsureEmbeddingSuccessAsync(response);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return doc.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(x => x.GetSingle())
            .ToArray();
    }

    private async Task EnsureEmbeddingSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        var detail = string.IsNullOrWhiteSpace(body)
            ? response.ReasonPhrase
            : body.Trim();

        throw new HttpRequestException(
            $"Embedding endpoint returned {(int)response.StatusCode} "
            + $"for format '{_options.ApiFormat}' at "
            + $"'{_httpClient.BaseAddress}': {detail}",
            null,
            response.StatusCode);
    }
}
