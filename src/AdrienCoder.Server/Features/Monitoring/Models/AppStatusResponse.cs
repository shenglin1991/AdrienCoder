namespace AdrienCoder.Server.Features.Monitoring.Models;

public class AppStatusResponse
{
    public string Api { get; set; } = "ok";
    public string Qdrant { get; set; } = "unknown";
    public string Embedding { get; set; } = "unknown";
    public string Llm { get; set; } = "unknown";
    public string ActiveProvider { get; set; } = "None";
    public string? Model { get; set; }
    public string? ActiveRepository { get; set; }
    public string? ActiveRepositorySignature { get; set; }
    public int? ActiveRepositoryChunks { get; set; }
    public string? LastIndexRepository { get; set; }
    public string? LastIndexSignature { get; set; }
    public int? LastIndexChunks { get; set; }
    public string? EmbeddingBackend { get; set; }
    public string? EmbeddingModel { get; set; }
    public int WorkersConnected { get; set; }
    public int WorkersHealthy { get; set; }
    public string? WorkerModel { get; set; }
    public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;
}
