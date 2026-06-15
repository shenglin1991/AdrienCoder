using AdrienCoder.Server.Features.Indexing.Services;
using AdrienCoder.Server.Features.Llm.Services;

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

    public async Task<string> AskRepositoryAsync(string question)
    {
        var context = await _repositoryIndexingService
            .BuildContextFromExistingIndexAsync(question);

        return await _llmService.AskWithContextAsync(question, context);
    }
}
