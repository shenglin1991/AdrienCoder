namespace AdrienCoder.Server.Features.Llm.Services;

public interface ILLMService
{
    Task<string> AskAsync(string question);
    Task<string> AskWithContextAsync(string question, string context);
    IAsyncEnumerable<string> StreamAskAsync(
        string question,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> StreamAskWithContextAsync(
        string question,
        string context,
        CancellationToken cancellationToken = default);
    Task<string> GetModelsAsync();
    Task<bool> IsHealthyAsync();
}
