namespace AdrienCoder.Contracts.Indexing;

public sealed record IndexRepositoryCheckRequest(
    string RepositoryName,
    string RepositorySignature,
    int IndexedFiles,
    int Chunks);
