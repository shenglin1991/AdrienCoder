using System.Net.Http.Json;
using System.Text.Json;
using AdrienCoder.Server.Features.Vector.Models;
using Microsoft.Extensions.Options;

namespace AdrienCoder.Server.Features.Vector.Services;

/// <summary>
/// Client for the Ollama embeddings endpoint used by Qdrant indexing and search.
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
        var payload = new
        {
            model = _options.Model,
            prompt = text
        };

        var response = await _httpClient.PostAsJsonAsync("api/embeddings", payload);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return doc.RootElement
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(x => x.GetSingle())
            .ToArray();
    }
}
