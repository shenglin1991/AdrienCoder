namespace AdrienCoder.Server.Features.Vector.Models;

public class EmbeddingOptions
{
    public string ApiFormat { get; set; } = "Ollama";
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "nomic-embed-text";
    public int VectorSize { get; set; } = 768;
    public int MaxParallelism { get; set; } = 2;
    public int UpsertBatchSize { get; set; } = 64;
}
