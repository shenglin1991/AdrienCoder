using AdrienCoder.Api.Features.Indexing.Models;
using AdrienCoder.Api.Features.Vector.Models;
using AdrienCoder.Api.Features.Vector.Services;

namespace AdrienCoder.Api.Features.Indexing.Services;

/// <summary>
/// Coordinates the RAG pipeline: scan, chunk, vectorize, search, then build LLM context.
/// </summary>
public class RepositoryIndexingService
{
    private readonly RepoScannerService _repoScannerService;
    private readonly RepoIndexStore _repoIndexStore;
    private readonly CodeChunkerService _codeChunkerService;
    private readonly QdrantService _qdrantService;
    private readonly ILogger<RepositoryIndexingService> _logger;

    public RepositoryIndexingService(
        RepoScannerService repoScannerService,
        RepoIndexStore repoIndexStore,
        CodeChunkerService codeChunkerService,
        QdrantService qdrantService,
        ILogger<RepositoryIndexingService> logger)
    {
        _repoScannerService = repoScannerService;
        _repoIndexStore = repoIndexStore;
        _codeChunkerService = codeChunkerService;
        _qdrantService = qdrantService;
        _logger = logger;
    }

    public IReadOnlyList<IndexedFile> GetIndexedFiles()
    {
        return _repoIndexStore.GetFiles();
    }

    public (IReadOnlyList<IndexedFile> Files, bool WasUpdated) IndexRepository(
        string repoPath)
    {
        _repoIndexStore.UpdateLock.Wait();

        try
        {
            return IndexRepositoryCore(repoPath);
        }
        finally
        {
            _repoIndexStore.UpdateLock.Release();
        }
    }

    private (
        IReadOnlyList<IndexedFile> Files,
        bool WasUpdated) IndexRepositoryCore(string repoPath)
    {
        if (!Directory.Exists(repoPath))
        {
            throw new DirectoryNotFoundException($"Repo path not found: {repoPath}");
        }

        // The manifest reads only filesystem metadata. File contents are loaded
        // only when the path, size or modification date changed.
        var manifest = _repoScannerService.CreateManifest(repoPath);

        if (manifest.Files.Count == 0)
        {
            throw new InvalidOperationException(
                "No supported files found in the repository.");
        }

        if (_repoIndexStore.TryGetCurrentFiles(manifest, out var currentFiles))
        {
            _logger.LogInformation(
                "Repository {RepositoryPath} is unchanged. Skipping file reads.",
                manifest.RepositoryPath);

            return (currentFiles, false);
        }

        var files = _repoScannerService.ReadFiles(manifest);
        _repoIndexStore.SetFiles(manifest, files);

        return (_repoIndexStore.GetFiles(), true);
    }

    public List<CodeChunk> GetChunks()
    {
        var files = GetIndexedFiles();

        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                "No repository indexed. Call POST /api/index/repo first.");
        }

        var chunks = _codeChunkerService.ChunkFiles(files);

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException(
                "No content found to index in the repository.");
        }

        return chunks;
    }

    public async Task<(
        int IndexedFiles,
        int Chunks,
        bool WasUpdated)> IndexVectorsAsync()
    {
        await _repoIndexStore.UpdateLock.WaitAsync();

        try
        {
            return await IndexVectorsCoreAsync();
        }
        finally
        {
            _repoIndexStore.UpdateLock.Release();
        }
    }

    private async Task<(
        int IndexedFiles,
        int Chunks,
        bool WasUpdated)> IndexVectorsCoreAsync()
    {
        var files = GetIndexedFiles();

        if (_repoIndexStore.TryGetCurrentVectorChunkCount(out var chunkCount))
        {
            _logger.LogInformation(
                "Vector index is current in memory. Skipping embeddings.");

            return (files.Count, chunkCount, false);
        }

        if (!_repoIndexStore.TryGetCurrentRepository(
            out var repositoryPath,
            out var contentSignature))
        {
            throw new InvalidOperationException(
                "No repository indexed. Call POST /api/index/repo first.");
        }

        var persistedChunkCount = await _qdrantService
            .GetIndexedChunkCountAsync(repositoryPath, contentSignature);

        if (persistedChunkCount is not null)
        {
            await _qdrantService.SetActiveIndexAsync(
                repositoryPath,
                contentSignature,
                persistedChunkCount.Value);

            _repoIndexStore.MarkVectorsCurrent(persistedChunkCount.Value);

            _logger.LogInformation(
                "Vector index is already persisted in Qdrant. Skipping embeddings.");

            return (files.Count, persistedChunkCount.Value, false);
        }

        var chunks = GetChunks();

        await _qdrantService.UpsertChunksAsync(
            chunks,
            repositoryPath,
            contentSignature);

        _repoIndexStore.MarkVectorsCurrent(chunks.Count);

        return (files.Count, chunks.Count, true);
    }

    public async Task<(
        int IndexedFiles,
        int Chunks,
        bool WasUpdated)> ReindexVectorsAsync(string repoPath)
    {
        await _repoIndexStore.UpdateLock.WaitAsync();

        try
        {
            IndexRepositoryCore(repoPath);
            return await IndexVectorsCoreAsync();
        }
        finally
        {
            _repoIndexStore.UpdateLock.Release();
        }
    }

    public Task<List<VectorSearchResult>> SearchAsync(
        string question,
        int limit)
    {
        if (!_repoIndexStore.TryGetCurrentRepository(
            out var repositoryPath,
            out var contentSignature))
        {
            return _qdrantService.SearchAsync(question, limit);
        }

        return _qdrantService.SearchAsync(
            question,
            limit,
            repositoryPath,
            contentSignature);
    }

    public Task<string> GetVectorStatusAsync()
    {
        return _qdrantService.GetStatusAsync();
    }

    public async Task<string> BuildContextFromExistingIndexAsync(
        string question,
        int limit = 5)
    {
        var indexState = await GetAvailableIndexAsync();

        var chunks = await _qdrantService.SearchAsync(
            question,
            limit,
            indexState.RepositoryPath,
            indexState.RepositorySignature);

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException(
                "No relevant chunks found. Reindex the repository first.");
        }

        return string.Join("\n\n", chunks.Select(chunk => $"""
        --- FILE: {chunk.FilePath} | CHUNK: {chunk.ChunkIndex} | SCORE: {chunk.Score} ---
        {chunk.Content}
        """));
    }

    private async Task<VectorIndexState> GetAvailableIndexAsync()
    {
        if (_repoIndexStore.TryGetCurrentRepository(
            out var repositoryPath,
            out var contentSignature))
        {
            var chunkCount = await _qdrantService.GetIndexedChunkCountAsync(
                repositoryPath,
                contentSignature);

            if (chunkCount is null)
            {
                throw new InvalidOperationException(
                    "The repository has changed or is not vectorized. "
                    + "Call POST /api/vector/index first.");
            }

            return new VectorIndexState(
                repositoryPath,
                contentSignature,
                chunkCount.Value);
        }

        return await _qdrantService.GetActiveIndexAsync()
            ?? throw new InvalidOperationException(
                "No vector index found. Index a repository first.");
    }
}
