namespace AdrienCoder.Api.Features.Vector.Models;

public sealed record VectorIndexState(
    string RepositoryPath,
    string RepositorySignature,
    int ChunkCount);
