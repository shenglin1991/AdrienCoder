namespace AdrienCoder.Shared.Authentication;

public sealed class ApiKeyOptions
{
    public const string SectionName = "Authentication";
    public const string HeaderName = "X-Api-Key";

    public string ApiKey { get; set; } = string.Empty;
}
