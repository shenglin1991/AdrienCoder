namespace AdrienCoder.Contracts.Indexing;

public sealed record CodeChunkDto(
    string Id,
    string FilePath,
    string Content,
    int ChunkIndex);
