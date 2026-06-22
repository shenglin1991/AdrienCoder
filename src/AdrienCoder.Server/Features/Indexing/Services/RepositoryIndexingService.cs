using AdrienCoder.Contracts.Indexing;
using AdrienCoder.Server.Features.Indexing.Models;
using AdrienCoder.Server.Features.Vector.Models;
using AdrienCoder.Server.Features.Vector.Services;

namespace AdrienCoder.Server.Features.Indexing.Services;

/// <summary>
/// Persists chunks uploaded by clients and retrieves repository context.
/// </summary>
public sealed class RepositoryIndexingService
{
    private readonly QdrantService _qdrantService;
    private readonly ILogger<RepositoryIndexingService> _logger;

    public RepositoryIndexingService(
        QdrantService qdrantService,
        ILogger<RepositoryIndexingService> logger)
    {
        _qdrantService = qdrantService;
        _logger = logger;
    }

    public async Task<IndexRepositoryResponse> IndexAsync(
        IndexRepositoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryName))
        {
            throw new InvalidOperationException("RepositoryName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RepositorySignature))
        {
            throw new InvalidOperationException(
                "RepositorySignature is required.");
        }

        if (request.Chunks.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one code chunk is required.");
        }

        var existingChunkCount = await _qdrantService
            .GetIndexedChunkCountAsync(
                request.RepositoryName,
                request.RepositorySignature);

        var indexedFiles = request.Chunks
            .Select(chunk => chunk.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (existingChunkCount == request.Chunks.Count)
        {
            await _qdrantService.SetActiveIndexAsync(
                request.RepositoryName,
                request.RepositorySignature,
                existingChunkCount.Value);

            return new IndexRepositoryResponse(
                indexedFiles,
                existingChunkCount.Value,
                false);
        }

        var chunks = request.Chunks
            .Select(chunk => new CodeChunk
            {
                Id = chunk.Id,
                FilePath = chunk.FilePath,
                Content = chunk.Content,
                ChunkIndex = chunk.ChunkIndex
            })
            .ToList();

        await _qdrantService.UpsertChunksAsync(
            chunks,
            request.RepositoryName,
            request.RepositorySignature);

        _logger.LogInformation(
            "Indexed repository {RepositoryName} with {ChunkCount} chunks.",
            request.RepositoryName,
            chunks.Count);

        return new IndexRepositoryResponse(indexedFiles, chunks.Count, true);
    }

    public async Task<IndexRepositoryResponse> CheckIndexAsync(
        IndexRepositoryCheckRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryName))
        {
            throw new InvalidOperationException("RepositoryName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.RepositorySignature))
        {
            throw new InvalidOperationException(
                "RepositorySignature is required.");
        }

        if (request.Chunks <= 0)
        {
            throw new InvalidOperationException(
                "At least one code chunk is required.");
        }

        var existingChunkCount = await _qdrantService
            .GetIndexedChunkCountAsync(
                request.RepositoryName,
                request.RepositorySignature);

        if (existingChunkCount == request.Chunks)
        {
            await _qdrantService.SetActiveIndexAsync(
                request.RepositoryName,
                request.RepositorySignature,
                existingChunkCount.Value);

            return new IndexRepositoryResponse(
                request.IndexedFiles,
                existingChunkCount.Value,
                false);
        }

        return new IndexRepositoryResponse(
            request.IndexedFiles,
            request.Chunks,
            true);
    }

    public async Task<List<VectorSearchResult>> SearchAsync(
        string question,
        int limit)
    {
        var activeIndex = await _qdrantService.GetActiveIndexAsync();

        if (activeIndex is null)
        {
            return [];
        }

        return await _qdrantService.SearchAsync(
            question,
            limit,
            activeIndex.RepositoryPath,
            activeIndex.RepositorySignature);
    }

    public Task<string> GetVectorStatusAsync()
    {
        return _qdrantService.GetStatusAsync();
    }

    public Task<StoredVectorChunkPage?> GetStoredChunksAsync(
        int limit,
        string? offset)
    {
        return _qdrantService.GetActiveChunksAsync(limit, offset);
    }

    public async Task<VectorIndexState?> GetActiveIndexAsync()
    {
        return await _qdrantService.GetActiveIndexAsync();
    }

    public async Task<string> BuildContextFromExistingIndexAsync(
        string question,
        string? repositoryName = null,
        int limit = 5,
        int neighborWindow = 1)
    {
        var index = string.IsNullOrWhiteSpace(repositoryName)
            ? await _qdrantService.GetActiveIndexAsync()
            : await _qdrantService.GetRepositoryIndexAsync(repositoryName);

        if (index is null)
        {
            var message = string.IsNullOrWhiteSpace(repositoryName)
                ? "No vector index found. Upload a repository first."
                : $"No vector index found for repository '{repositoryName}'.";

            throw new InvalidOperationException(message);
        }

        var chunks = await _qdrantService.SearchAsync(
            question,
            limit,
            index.RepositoryPath,
            index.RepositorySignature);

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException(
                "No relevant chunks found. Reindex the repository first.");
        }

        var neighborChunks = await _qdrantService.GetNeighborChunksAsync(
            chunks,
            neighborWindow,
            index.RepositoryPath,
            index.RepositorySignature);

        var contextChunks = chunks
            .Concat(neighborChunks)
            .GroupBy(
                chunk => $"{chunk.FilePath}\0{chunk.ChunkIndex}",
                StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(chunk => chunk.Score)
                .First())
            .OrderByDescending(chunk => chunk.Score)
            .ThenBy(chunk => chunk.FilePath, StringComparer.Ordinal)
            .ThenBy(chunk => chunk.ChunkIndex)
            .ToList();

        return string.Join("\n\n", contextChunks.Select(chunk => $"""
        --- FILE: {chunk.FilePath} | CHUNK: {chunk.ChunkIndex} | SCORE: {chunk.Score} ---
        {chunk.Content}
        """));
    }
}
