using AdrienCoder.Api.Features.Indexing.Models;

namespace AdrienCoder.Api.Features.Indexing.Services;

/// <summary>
/// Holds the latest repository snapshot in memory between API requests.
/// </summary>
public class RepoIndexStore
{
    private readonly Lock _lock = new();
    private readonly List<IndexedFile> _files = [];

    public void SetFiles(IEnumerable<IndexedFile> files)
    {
        lock (_lock)
        {
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
}
