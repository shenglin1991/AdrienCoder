using AdrienCoder.Api.Features.Indexing.Models;

namespace AdrienCoder.Api.Features.Indexing.Services;

/// <summary>
/// Holds the latest repository snapshot in memory between API requests.
/// </summary>
public class RepoIndexStore
{
    private readonly Lock _lock = new();
    private readonly List<IndexedFile> _files = [];
    private string? _repositoryPath;
    private string? _contentSignature;
    private string? _vectorSignature;
    private int _vectorChunkCount;

    internal SemaphoreSlim UpdateLock { get; } = new(1, 1);

    public bool TryGetCurrentFiles(
        RepositoryManifest manifest,
        out IReadOnlyList<IndexedFile> files)
    {
        lock (_lock)
        {
            var isCurrent =
                string.Equals(
                    _repositoryPath,
                    manifest.RepositoryPath,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    _contentSignature,
                    manifest.Signature,
                    StringComparison.Ordinal);

            files = isCurrent ? _files.ToList() : [];
            return isCurrent;
        }
    }

    public void SetFiles(
        RepositoryManifest manifest,
        IEnumerable<IndexedFile> files)
    {
        lock (_lock)
        {
            _repositoryPath = manifest.RepositoryPath;
            _contentSignature = manifest.Signature;
            _vectorSignature = null;
            _vectorChunkCount = 0;
            _files.Clear();
            _files.AddRange(files);
        }
    }

    public IReadOnlyList<IndexedFile> GetFiles()
    {
        lock (_lock)
        {
            // Return a snapshot so callers cannot observe a concurrent refresh.
            return _files.ToList();
        }
    }

    public bool TryGetCurrentVectorChunkCount(out int chunkCount)
    {
        lock (_lock)
        {
            var isCurrent = _contentSignature is not null
                && string.Equals(
                    _contentSignature,
                    _vectorSignature,
                    StringComparison.Ordinal);

            chunkCount = isCurrent ? _vectorChunkCount : 0;
            return isCurrent;
        }
    }

    public void MarkVectorsCurrent(int chunkCount)
    {
        lock (_lock)
        {
            _vectorSignature = _contentSignature;
            _vectorChunkCount = chunkCount;
        }
    }

    public bool TryGetCurrentRepository(
        out string repositoryPath,
        out string contentSignature)
    {
        lock (_lock)
        {
            repositoryPath = _repositoryPath ?? string.Empty;
            contentSignature = _contentSignature ?? string.Empty;

            return _repositoryPath is not null
                && _contentSignature is not null;
        }
    }
}
