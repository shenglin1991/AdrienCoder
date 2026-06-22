using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using AdrienCoder.Server.Features.Indexing.Models;
using AdrienCoder.Server.Features.Vector.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AdrienCoder.Server.Features.Vector.Services;

/// <summary>
/// Persists code chunks and performs semantic searches against Qdrant.
/// </summary>
public class QdrantService
{
    private const string ActiveIndexMarker = "active-repository-state";

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

    public async Task<int?> GetIndexedChunkCountAsync(
        string repositoryPath,
        string repositorySignature)
    {
        await CreateCollectionIfNotExistsAsync();

        var markerId = CreatePointId(
            $"repository-state::{repositoryPath}");
        var request = new
        {
            ids = new[] { markerId },
            with_payload = true,
            with_vector = false
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"collections/{_options.CollectionName}/points",
            request);

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var points = document.RootElement.GetProperty("result");

        if (points.GetArrayLength() == 0)
        {
            return null;
        }

        var payload = points[0].GetProperty("payload");
        var storedSignature = GetString(
            payload,
            "repositorySignature",
            "RepositorySignature");

        if (!string.Equals(
            storedSignature,
            repositorySignature,
            StringComparison.Ordinal))
        {
            return null;
        }

        return GetInt(payload, "chunkCount", "ChunkCount");
    }

    public async Task<VectorIndexState?> GetActiveIndexAsync()
    {
        await CreateCollectionIfNotExistsAsync();

        var markerId = CreatePointId(ActiveIndexMarker);
        return await GetIndexStateAsync(markerId);
    }

    public async Task<VectorIndexState?> GetRepositoryIndexAsync(
        string repositoryPath)
    {
        await CreateCollectionIfNotExistsAsync();

        var markerId = CreatePointId($"repository-state::{repositoryPath}");
        return await GetIndexStateAsync(markerId);
    }

    private async Task<VectorIndexState?> GetIndexStateAsync(string markerId)
    {
        var payload = await GetPointPayloadAsync(markerId);

        if (payload is null)
        {
            return null;
        }

        var repositoryPath = GetString(
            payload.Value,
            "repositoryPath",
            "RepositoryPath");
        var repositorySignature = GetString(
            payload.Value,
            "repositorySignature",
            "RepositorySignature");

        if (string.IsNullOrWhiteSpace(repositoryPath)
            || string.IsNullOrWhiteSpace(repositorySignature))
        {
            return null;
        }

        return new VectorIndexState(
            repositoryPath,
            repositorySignature,
            GetInt(payload.Value, "chunkCount", "ChunkCount"));
    }

    public async Task SetActiveIndexAsync(
        string repositoryPath,
        string repositorySignature,
        int chunkCount)
    {
        await CreateCollectionIfNotExistsAsync();

        var response = await _httpClient.PutAsJsonAsync(
            $"collections/{_options.CollectionName}/points?wait=true",
            new
            {
                points = new[]
                {
                    CreateActiveIndexPoint(
                        repositoryPath,
                        repositorySignature,
                        chunkCount)
                }
            });

        response.EnsureSuccessStatusCode();
    }

    public async Task<StoredVectorChunkPage?> GetActiveChunksAsync(
        int limit,
        string? offset)
    {
        var activeIndex = await GetActiveIndexAsync();

        if (activeIndex is null)
        {
            return null;
        }

        var request = new Dictionary<string, object?>
        {
            ["limit"] = limit,
            ["with_payload"] = true,
            ["with_vector"] = false,
            ["filter"] = new
            {
                must = new object[]
                {
                    new
                    {
                        key = "pointType",
                        match = new { value = "chunk" }
                    },
                    new
                    {
                        key = "repositoryPath",
                        match = new
                        {
                            value = activeIndex.RepositoryPath
                        }
                    },
                    new
                    {
                        key = "repositorySignature",
                        match = new
                        {
                            value = activeIndex.RepositorySignature
                        }
                    }
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(offset))
        {
            request["offset"] = offset;
        }

        var response = await _httpClient.PostAsJsonAsync(
            $"collections/{_options.CollectionName}/points/scroll",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                $"Qdrant scroll failed with status "
                + $"{(int)response.StatusCode}: {error}",
                null,
                response.StatusCode);
        }

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var result = document.RootElement.GetProperty("result");
        var chunks = result
            .GetProperty("points")
            .EnumerateArray()
            .Select(point =>
            {
                var payload = point.GetProperty("payload");

                return new StoredVectorChunk(
                    GetString(payload, "Id", "id"),
                    GetString(payload, "FilePath", "filePath"),
                    GetInt(payload, "ChunkIndex", "chunkIndex"),
                    GetString(payload, "Content", "content"));
            })
            .ToList();

        string? nextOffset = null;

        if (result.TryGetProperty("next_page_offset", out var nextPageOffset)
            && nextPageOffset.ValueKind is not JsonValueKind.Null)
        {
            nextOffset = nextPageOffset.ToString();
        }

        return new StoredVectorChunkPage(
            activeIndex.RepositoryPath,
            activeIndex.RepositorySignature,
            activeIndex.ChunkCount,
            chunks,
            nextOffset);
    }

    public async Task UpsertChunksAsync(
        IReadOnlyList<CodeChunk> chunks,
        string repositoryPath,
        string repositorySignature)
    {
        await CreateCollectionIfNotExistsAsync();

        var maxParallelism = Math.Max(1, _embeddingOptions.MaxParallelism);
        var batchSize = Math.Max(1, _embeddingOptions.UpsertBatchSize);
        var chunkPointIds = chunks
            .Select(chunk => CreatePointId($"{repositoryPath}::{chunk.Id}"))
            .ToArray();
        var contentHashes = chunks
            .Select(chunk => ComputeContentHash(chunk.Content))
            .ToArray();
        var reusableVectors = await GetReusableChunkVectorsAsync(
            chunkPointIds,
            batchSize);
        var chunkPoints = new object[chunks.Count];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, chunks.Count),
            new ParallelOptions { MaxDegreeOfParallelism = maxParallelism },
            async (index, _) =>
            {
                var chunk = chunks[index];
                var pointId = chunkPointIds[index];
                var contentHash = contentHashes[index];
                var vector = reusableVectors.TryGetValue(
                        pointId,
                        out var reusableVector)
                    && reusableVector.Matches(contentHash, chunk.Content)
                        ? reusableVector.Vector
                        : await _embeddingService.EmbedAsync(chunk.Content);

                chunkPoints[index] = new
                {
                    id = pointId,
                    vector,
                    payload = new
                    {
                        pointType = "chunk",
                        repositoryPath,
                        repositorySignature,
                        chunk.Id,
                        chunk.FilePath,
                        chunk.Content,
                        contentHash,
                        chunk.ChunkIndex
                    }
                };
            });

        foreach (var batch in chunkPoints.Chunk(batchSize))
        {
            await UpsertPointsAsync(batch);
        }

        // The marker persists the repository signature across API restarts.
        var repositoryStatePoint = new
        {
            id = CreatePointId($"repository-state::{repositoryPath}"),
            vector = new float[_embeddingOptions.VectorSize],
            payload = new
            {
                pointType = "repositoryState",
                repositoryPath,
                repositorySignature,
                chunkCount = chunks.Count
            }
        };

        // Ask/repo uses this stable marker to locate the latest completed index.
        var activeIndexPoint = CreateActiveIndexPoint(
            repositoryPath,
            repositorySignature,
            chunks.Count);

        await UpsertPointsAsync(new[] { repositoryStatePoint, activeIndexPoint });
    }

    private async Task UpsertPointsAsync(IReadOnlyList<object> points)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"collections/{_options.CollectionName}/points?wait=true",
            new
            {
                points
            });

        response.EnsureSuccessStatusCode();
    }

    private async Task<Dictionary<string, StoredChunkVector>> GetReusableChunkVectorsAsync(
        IReadOnlyList<string> pointIds,
        int batchSize)
    {
        var vectors = new Dictionary<string, StoredChunkVector>(
            StringComparer.Ordinal);

        foreach (var batch in pointIds.Chunk(batchSize))
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"collections/{_options.CollectionName}/points",
                new
                {
                    ids = batch,
                    with_payload = true,
                    with_vector = true
                });

            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

            foreach (var point in document.RootElement
                .GetProperty("result")
                .EnumerateArray())
            {
                if (!point.TryGetProperty("payload", out var payload)
                    || !point.TryGetProperty("vector", out var vectorElement)
                    || vectorElement.ValueKind is not JsonValueKind.Array)
                {
                    continue;
                }

                var id = point.GetProperty("id").ToString();
                var vector = vectorElement
                    .EnumerateArray()
                    .Select(value => value.GetSingle())
                    .ToArray();

                if (vector.Length == _embeddingOptions.VectorSize)
                {
                    vectors[id] = new StoredChunkVector(
                        GetString(payload, "contentHash", "ContentHash"),
                        GetString(payload, "Content", "content"),
                        vector);
                }
            }
        }

        return vectors;
    }

    private object CreateActiveIndexPoint(
        string repositoryPath,
        string repositorySignature,
        int chunkCount)
    {
        return new
        {
            id = CreatePointId(ActiveIndexMarker),
            vector = new float[_embeddingOptions.VectorSize],
            payload = new
            {
                pointType = "activeRepositoryState",
                repositoryPath,
                repositorySignature,
                chunkCount
            }
        };
    }

    private async Task<JsonElement?> GetPointPayloadAsync(string pointId)
    {
        var request = new
        {
            ids = new[] { pointId },
            with_payload = true,
            with_vector = false
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"collections/{_options.CollectionName}/points",
            request);

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var points = document.RootElement.GetProperty("result");

        if (points.GetArrayLength() == 0)
        {
            return null;
        }

        return points[0].GetProperty("payload").Clone();
    }

    private static string CreatePointId(string chunkId)
    {
        // Stable IDs make repeated repository indexing replace points instead of duplicating them.
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(chunkId));
        return new Guid(hash.AsSpan(0, 16)).ToString();
    }

    private static string CreateChunkId(string relativePath, int chunkIndex)
    {
        var value = Encoding.UTF8.GetBytes($"{relativePath}\0{chunkIndex}");
        return Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    }

    private static string ComputeContentHash(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<List<VectorSearchResult>> SearchAsync(
        string question,
        int limit,
        string? repositoryPath = null,
        string? repositorySignature = null)
    {
        var vector = await _embeddingService.EmbedAsync(question);

        var conditions = new List<object>
        {
            new
            {
                key = "pointType",
                match = new { value = "chunk" }
            }
        };

        if (!string.IsNullOrWhiteSpace(repositoryPath)
            && !string.IsNullOrWhiteSpace(repositorySignature))
        {
            conditions.Add(new
            {
                key = "repositoryPath",
                match = new { value = repositoryPath }
            });
            conditions.Add(new
            {
                key = "repositorySignature",
                match = new { value = repositorySignature }
            });
        }

        var payload = new
        {
            vector,
            limit,
            with_payload = true,
            filter = new
            {
                must = conditions
            }
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

    public async Task<List<VectorSearchResult>> GetNeighborChunksAsync(
        IReadOnlyList<VectorSearchResult> chunks,
        int neighborWindow,
        string repositoryPath,
        string repositorySignature)
    {
        if (neighborWindow <= 0 || chunks.Count == 0)
        {
            return [];
        }

        var candidates = new Dictionary<string, NeighborCandidate>(
            StringComparer.Ordinal);

        foreach (var chunk in chunks)
        {
            for (var offset = -neighborWindow; offset <= neighborWindow; offset++)
            {
                if (offset == 0)
                {
                    continue;
                }

                var neighborIndex = chunk.ChunkIndex + offset;
                if (neighborIndex < 0)
                {
                    continue;
                }

                var id = CreatePointId(
                    $"{repositoryPath}::{CreateChunkId(chunk.FilePath, neighborIndex)}");
                var score = chunk.Score - (0.001f * Math.Abs(offset));

                if (!candidates.TryGetValue(id, out var existing)
                    || score > existing.Score)
                {
                    candidates[id] = new NeighborCandidate(score);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return [];
        }

        var results = new List<VectorSearchResult>();
        var batchSize = Math.Max(1, _embeddingOptions.UpsertBatchSize);

        foreach (var batch in candidates.Keys.Chunk(batchSize))
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"collections/{_options.CollectionName}/points",
                new
                {
                    ids = batch,
                    with_payload = true,
                    with_vector = false
                });

            response.EnsureSuccessStatusCode();

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync());

            foreach (var point in document.RootElement
                .GetProperty("result")
                .EnumerateArray())
            {
                if (!point.TryGetProperty("payload", out var payload))
                {
                    continue;
                }

                var payloadRepositoryPath = GetString(
                    payload,
                    "repositoryPath",
                    "RepositoryPath");
                var payloadRepositorySignature = GetString(
                    payload,
                    "repositorySignature",
                    "RepositorySignature");

                if (!string.Equals(
                        payloadRepositoryPath,
                        repositoryPath,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        payloadRepositorySignature,
                        repositorySignature,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var id = point.GetProperty("id").ToString();
                var score = candidates.TryGetValue(id, out var candidate)
                    ? candidate.Score
                    : 0;

                results.Add(new VectorSearchResult
                {
                    Score = score,
                    FilePath = GetString(payload, "FilePath", "filePath"),
                    Content = GetString(payload, "Content", "content"),
                    ChunkIndex = GetInt(payload, "ChunkIndex", "chunkIndex")
                });
            }
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

    private sealed record StoredChunkVector(
        string ContentHash,
        string Content,
        float[] Vector)
    {
        public bool Matches(string contentHash, string content)
        {
            if (!string.IsNullOrWhiteSpace(ContentHash))
            {
                return string.Equals(
                    ContentHash,
                    contentHash,
                    StringComparison.Ordinal);
            }

            return string.Equals(Content, content, StringComparison.Ordinal);
        }
    }

    private sealed record NeighborCandidate(float Score);
}
