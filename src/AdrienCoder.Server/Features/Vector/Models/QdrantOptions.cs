namespace AdrienCoder.Server.Features.Vector.Models;

public class QdrantOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6333;
    public string CollectionName { get; set; } = "code";
}
