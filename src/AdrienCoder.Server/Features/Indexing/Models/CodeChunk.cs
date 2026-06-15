namespace AdrienCoder.Server.Features.Indexing.Models;

public class CodeChunk
{
    public string Id { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
}
