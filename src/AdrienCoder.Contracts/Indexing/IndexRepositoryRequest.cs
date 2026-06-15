namespace AdrienCoder.Contracts.Indexing;

public sealed record IndexRepositoryRequest(
    string RepositoryName,
    string RepositorySignature,
    IReadOnlyList<CodeChunkDto> Chunks);
