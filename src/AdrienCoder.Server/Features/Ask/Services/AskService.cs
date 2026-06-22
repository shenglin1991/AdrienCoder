using AdrienCoder.Server.Features.Indexing.Services;
using AdrienCoder.Server.Features.Llm.Services;
using AdrienCoder.Server.Features.Vector.Models;

namespace AdrienCoder.Server.Features.Ask.Services;

/// <summary>
/// Coordinates LLM calls with or without repository context.
/// </summary>
public class AskService
{
    private readonly ILLMService _llmService;
    private readonly RepositoryIndexingService _repositoryIndexingService;

    public AskService(
        ILLMService llmService,
        RepositoryIndexingService repositoryIndexingService)
    {
        _llmService = llmService;
        _repositoryIndexingService = repositoryIndexingService;
    }

    public Task<string> AskAsync(string question)
    {
        return _llmService.AskAsync(question);
    }

    public IAsyncEnumerable<string> StreamAskAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        return _llmService.StreamAskAsync(question, cancellationToken);
    }

    public async Task<string> AskRepositoryAsync(
        string question,
        string? repositoryName = null)
    {
        var context = await _repositoryIndexingService
            .BuildContextFromExistingIndexAsync(question, repositoryName);

        return await _llmService.AskWithContextAsync(question, context);
    }

    public async Task<IAsyncEnumerable<string>> StreamAskRepositoryAsync(
        string question,
        string? repositoryName = null,
        CancellationToken cancellationToken = default)
    {
        var context = await _repositoryIndexingService
            .BuildContextFromExistingIndexAsync(question, repositoryName);

        return _llmService.StreamAskWithContextAsync(
            question,
            context,
            cancellationToken);
    }

    public Task<(VectorIndexState Index, IReadOnlyList<VectorSearchResult> Chunks)>
        GetDebugContextAsync(
            string question,
            string? repositoryName = null)
    {
        return _repositoryIndexingService.GetDebugContextAsync(
            question,
            repositoryName);
    }
}
