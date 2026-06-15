namespace AdrienCoder.Server.Features.Llm.Models;

public class LLMOptions
{
    public string PreferredProvider { get; set; } = "WorkerGpu";
    public string FallbackProvider { get; set; } = "OpenAICompatible";
    public string SystemPrompt { get; set; } = string.Empty;
}
