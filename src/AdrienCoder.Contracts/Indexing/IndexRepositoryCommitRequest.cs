namespace AdrienCoder.Contracts.Indexing;

public sealed record IndexRepositoryCommitRequest(
    string RepositoryName,
    string RepositorySignature,
    int IndexedFiles,
    int Chunks);
