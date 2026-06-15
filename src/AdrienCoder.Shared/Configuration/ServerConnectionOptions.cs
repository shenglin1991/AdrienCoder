namespace AdrienCoder.Shared.Configuration;

public sealed class ServerConnectionOptions
{
    public const string SectionName = "Server";

    public string BaseUrl { get; set; } = "https://localhost:5001";
    public string ApiKey { get; set; } = string.Empty;
}
