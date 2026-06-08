namespace AdrienCoder.Api.Models;

public class LLMOptions
{
    public string Provider { get; set; } = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "qwen2.5-coder:7b";
    public string SystemPrompt { get; set; } = string.Empty;
}