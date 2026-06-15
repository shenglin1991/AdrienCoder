namespace AdrienCoder.WorkerGpu.Configuration;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int ReconnectDelaySeconds { get; set; } = 5;
}
