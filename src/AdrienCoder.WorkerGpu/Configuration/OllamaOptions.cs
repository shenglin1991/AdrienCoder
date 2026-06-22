namespace AdrienCoder.WorkerGpu.Configuration;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int NumPredict { get; set; } = 2048;
}
