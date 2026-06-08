using System.Net.Http.Json;
using AdrienCoder.Api.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AdrienCoder.Api.Services;

public class QdrantService
{
    private readonly HttpClient _httpClient;
    private readonly QdrantOptions _options;

    public QdrantService(
        HttpClient httpClient,
        IOptions<QdrantOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
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
                size = 768,
                distance = "Cosine"
            }
        };

        var createResponse = await _httpClient.PutAsJsonAsync(
            $"collections/{_options.CollectionName}",
            payload);

        createResponse.EnsureSuccessStatusCode();
    }

    public async Task UpsertChunksAsync(
        IReadOnlyList<CodeChunk> chunks,
        EmbeddingService embeddingService)
    {
        await CreateCollectionIfNotExistsAsync();

        var points = new List<object>();

        foreach (var chunk in chunks)
        {
            var vector = await embeddingService.EmbedAsync(chunk.Content);

            points.Add(new
            {
                id = Guid.NewGuid().ToString(),
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

    public async Task<string> SearchAsync(
    string question,
    int limit,
    EmbeddingService embeddingService)
    {
        var vector = await embeddingService.EmbedAsync(question);

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

        return await response.Content.ReadAsStringAsync();
    }
}