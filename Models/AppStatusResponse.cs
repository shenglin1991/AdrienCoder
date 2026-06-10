namespace AdrienCoder.Api.Models;

public class AppStatusResponse
{
    public string Api { get; set; } = "ok";
    public string Qdrant { get; set; } = "unknown";
    public string Llm { get; set; } = "unknown";
    public string ActiveProvider { get; set; } = "None";
    public string? Model { get; set; }
    public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;
}
