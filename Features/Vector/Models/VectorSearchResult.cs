namespace AdrienCoder.Api.Features.Vector.Models;

public class VectorSearchResult
{
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public float Score { get; set; }
}
