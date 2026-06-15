namespace AdrienCoder.WorkerGpu.Configuration;

public sealed class ServerOptions
{
    public const string SectionName = "Server";

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}
