namespace AdrienCoder.Server.Features.Vector.Models;

public sealed record StoredVectorChunkPage(
    string RepositoryPath,
    string RepositorySignature,
    int TotalChunks,
    IReadOnlyList<StoredVectorChunk> Chunks,
    string? NextOffset);
