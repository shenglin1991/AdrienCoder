using AdrienCoder.Api.Models;

namespace AdrienCoder.Api.Services;

public class RepoIndexStore
{
    private readonly List<IndexedFile> _files = [];

    public void SetFiles(IEnumerable<IndexedFile> files)
    {
        _files.Clear();
        _files.AddRange(files);
    }

    public IReadOnlyList<IndexedFile> GetFiles()
    {
        return _files;
    }

    public int Count => _files.Count;
}