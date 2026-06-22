namespace AdrienCoder.Contracts.Indexing;

public sealed record IndexRepositoryBatchRequest(
    string RepositoryName,
    string RepositorySignature,
    int IndexedFiles,
    int TotalChunks,
    IReadOnlyList<CodeChunkDto> Chunks);
