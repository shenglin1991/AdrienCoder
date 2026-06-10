using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using AdrienCoder.Api.Features.Indexing.Models;
using AdrienCoder.Api.Features.Vector.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AdrienCoder.Api.Features.Vector.Services;

/// <summary>
/// Persists code chunks and performs semantic searches against Qdrant.
/// </summary>
public class QdrantService
{
    private readonly HttpClient _httpClient;
    private readonly QdrantOptions _options;
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly EmbeddingService _embeddingService;

    public QdrantService(
        HttpClient httpClient,
        IOptions<QdrantOptions> options,
        IOptions<EmbeddingOptions> embeddingOptions,
        EmbeddingService embeddingService)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _embeddingOptions = embeddingOptions.Value;
        _embeddingService = embeddingService;
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("collections");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetStatusAsync()
    {
        await CreateCollectionIfNotExistsAsync();

        var response = await _httpClient.GetAsync("collections");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    public async Task CreateCollectionIfNotExistsAsync()
    {
        var response = await _httpClient.GetAsync(
            $"collections/{_options.CollectionName}");

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var payload = new
        {
            vectors = new
            {
                size = _embeddingOptions.VectorSize,
                distance = "Cosine"
            }
        };

        var createResponse = await _httpClient.PutAsJsonAsync(
            $"collections/{_options.CollectionName}",
            payload);

        createResponse.EnsureSuccessStatusCode();
    }

    public async Task UpsertChunksAsync(IReadOnlyList<CodeChunk> chunks)
    {
        await CreateCollectionIfNotExistsAsync();

        var points = new List<object>();

        foreach (var chunk in chunks)
        {
            var vector = await _embeddingService.EmbedAsync(chunk.Content);

            points.Add(new
            {
                id = CreatePointId(chunk.Id),
                vector,
                payload = new
                {
                    chunk.Id,
                    chunk.FilePath,
                    chunk.Content,
                    chunk.ChunkIndex
                }
            });
        }

        var body = new
        {
            points
        };

        var response = await _httpClient.PutAsJsonAsync(
            $"collections/{_options.CollectionName}/points?wait=true",
            body);

        response.EnsureSuccessStatusCode();
    }

    private static string CreatePointId(string chunkId)
    {
        // Stable IDs make repeated repository indexing replace points instead of duplicating them.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(chunkId));
        return new Guid(hash.AsSpan(0, 16)).ToString();
    }

    public async Task<List<VectorSearchResult>> SearchAsync(
        string question,
        int limit)
    {
        var vector = await _embeddingService.EmbedAsync(question);

        var payload = new
        {
            vector,
            limit,
            with_payload = true
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"collections/{_options.CollectionName}/points/search",
            payload);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        var results = new List<VectorSearchResult>();

        foreach (var item in doc.RootElement.GetProperty("result").EnumerateArray())
        {
            var payloadElement = item.GetProperty("payload");

            results.Add(new VectorSearchResult
            {
                Score = item.GetProperty("score").GetSingle(),
                FilePath = GetString(payloadElement, "FilePath", "filePath"),
                Content = GetString(payloadElement, "Content", "content"),
                ChunkIndex = GetInt(payloadElement, "ChunkIndex", "chunkIndex")
            });
        }

        return results;
    }

    private static string GetString(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.TryGetProperty(key, out var value))
            {
                return value.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static int GetInt(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.TryGetProperty(key, out var value))
            {
                return value.GetInt32();
            }
        }

        return 0;
    }
}
