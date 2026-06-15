namespace AdrienCoder.Server.Features.Vector.Models;

public sealed record StoredVectorChunk(
    string Id,
    string FilePath,
    int ChunkIndex,
    string Content);
