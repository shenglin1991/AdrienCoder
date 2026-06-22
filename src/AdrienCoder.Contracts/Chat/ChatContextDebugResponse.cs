namespace AdrienCoder.Contracts.Chat;

public sealed record ChatContextDebugResponse(
    string RepositoryName,
    string RepositorySignature,
    IReadOnlyList<ChatContextChunk> Chunks);

public sealed record ChatContextChunk(
    string FilePath,
    int ChunkIndex,
    float Score,
    string Content);
