namespace AdrienCoder.Server.Features.Llm.Services;

public interface ILLMService
{
    Task<string> AskAsync(string question);
    Task<string> AskWithContextAsync(string question, string context);
    Task<string> GetModelsAsync();
    Task<bool> IsHealthyAsync();
}
