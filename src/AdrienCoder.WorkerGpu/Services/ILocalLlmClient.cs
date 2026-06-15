namespace AdrienCoder.WorkerGpu.Services;

public interface ILocalLlmClient
{
    Task<string> ChatAsync(string prompt, CancellationToken cancellationToken);
}
