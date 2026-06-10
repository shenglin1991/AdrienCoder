namespace AdrienCoder.Api.Features.Indexing.Models;

public sealed record RepositoryManifest(
    string RepositoryPath,
    string Signature,
    IReadOnlyList<RepositoryFileMetadata> Files);

public sealed record RepositoryFileMetadata(
    string Path,
    long Length,
    DateTime LastModified);
