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

    public RepositoryIndexingService(
        RepoScannerService repoScannerService,
        RepoIndexStore repoIndexStore,
        CodeChunkerService codeChunkerService,
        QdrantService qdrantService)
    {
        _repoScannerService = repoScannerService;
        _repoIndexStore = repoIndexStore;
        _codeChunkerService = codeChunkerService;
        _qdrantService = qdrantService;
    }

    public IReadOnlyList<IndexedFile> GetIndexedFiles()
    {
        return _repoIndexStore.GetFiles();
    }

    public IReadOnlyList<IndexedFile> IndexRepository(string repoPath)
    {
        if (!Directory.Exists(repoPath))
        {
            throw new DirectoryNotFoundException($"Repo path not found: {repoPath}");
        }

        var files = _repoScannerService.IndexRepo(repoPath);

        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                "No supported files found in the repository.");
        }

        _repoIndexStore.SetFiles(files);
        return _repoIndexStore.GetFiles();
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

    public async Task<(int IndexedFiles, int Chunks)> IndexVectorsAsync()
    {
        var files = GetIndexedFiles();
        var chunks = GetChunks();

        await _qdrantService.UpsertChunksAsync(chunks);

        return (files.Count, chunks.Count);
    }

    public async Task<(int IndexedFiles, int Chunks)> ReindexVectorsAsync(
        string repoPath)
    {
        IndexRepository(repoPath);
        return await IndexVectorsAsync();
    }

    public Task<List<VectorSearchResult>> SearchAsync(
        string question,
        int limit)
    {
        return _qdrantService.SearchAsync(question, limit);
    }

    public Task<string> GetVectorStatusAsync()
    {
        return _qdrantService.GetStatusAsync();
    }

    public async Task<string> BuildContextAsync(
        string repoPath,
        string question,
        int limit = 5)
    {
        // Ask/repo always refreshes the vectors so the answer uses current source files.
        await ReindexVectorsAsync(repoPath);

        var chunks = await SearchAsync(question, limit);

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException(
                "No relevant chunks found after indexing the repository.");
        }

        return string.Join("\n\n", chunks.Select(chunk => $"""
        --- FILE: {chunk.FilePath} | CHUNK: {chunk.ChunkIndex} | SCORE: {chunk.Score} ---
        {chunk.Content}
        """));
    }
}
