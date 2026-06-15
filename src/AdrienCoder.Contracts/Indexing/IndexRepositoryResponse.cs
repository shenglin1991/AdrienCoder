namespace AdrienCoder.Contracts.Indexing;

public sealed record IndexRepositoryResponse(
    int IndexedFiles,
    int Chunks,
    bool Updated);
