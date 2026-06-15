namespace AdrienCoder.Server.Features.Vector.Models;

public sealed record VectorIndexState(
    string RepositoryPath,
    string RepositorySignature,
    int ChunkCount);
