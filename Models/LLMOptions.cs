namespace AdrienCoder.Api.Models;

public class LLMOptions
{
    public string PreferredProvider { get; set; } = "OpenAICompatible";
    public string FallbackProvider { get; set; } = "Ollama";
    public string SystemPrompt { get; set; } = string.Empty;
}
