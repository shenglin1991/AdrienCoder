namespace AdrienCoder.Server.Features.Vector.Models;

public class VectorSearchRequest
{
    public string Question { get; set; } = string.Empty;
    public int Limit { get; set; } = 5;
}
