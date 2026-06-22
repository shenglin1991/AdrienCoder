namespace AdrienCoder.WorkerGpu.Services;

public interface ILocalLlmClient
{
    Task<string> ChatAsync(string prompt, CancellationToken cancellationToken);
    IAsyncEnumerable<string> StreamChatAsync(
        string prompt,
        CancellationToken cancellationToken);
}
