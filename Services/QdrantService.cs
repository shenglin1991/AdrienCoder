using System.Net.Http.Json;
using AdrienCoder.Api.Models;
using Microsoft.Extensions.Options;

namespace AdrienCoder.Api.Services;

public class QdrantService
{
    private readonly HttpClient _httpClient;
    private readonly QdrantOptions _options;

    public QdrantService(HttpClient httpClient, IOptions<QdrantOptions> options)
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
            return;

        var payload = new
        {
            vectors = new
            {
                size = 1024,
                distance = "Cosine"
            }
        };

        var createResponse = await _httpClient.PutAsJsonAsync(
            $"collections/{_options.CollectionName}",
            payload);

        createResponse.EnsureSuccessStatusCode();
    }
}