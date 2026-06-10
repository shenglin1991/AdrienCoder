namespace AdrienCoder.Api.Features.Indexing.Models;

public class IndexedFile
{
    public string Path { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime LastModified { get; set; }
}
