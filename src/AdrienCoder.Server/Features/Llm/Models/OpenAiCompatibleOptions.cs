namespace AdrienCoder.Server.Features.Llm.Models;

public class OpenAiCompatibleOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
}
