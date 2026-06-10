using System.Security.Cryptography;
using System.Text;
using AdrienCoder.Api.Features.Indexing.Models;

namespace AdrienCoder.Api.Features.Indexing.Services;

/// <summary>
/// Reads source files from a repository while excluding generated and vendor folders.
/// </summary>
public class RepoScannerService
{
    private static readonly string[] AllowedExtensions =
    {
        ".cs", ".ts", ".html", ".scss", ".json", ".md", ".yml", ".yaml"
    };

    private static readonly string[] IgnoredDirectories =
    {
        "node_modules", "bin", "obj", ".git", "dist", "coverage",
        ".angular", ".nx", ".vs"
    };

    public RepositoryManifest CreateManifest(string repoPath)
    {
        var normalizedPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(repoPath));

        var files = Directory
            .EnumerateFiles(repoPath, "*.*", SearchOption.AllDirectories)
            .Where(IsAllowedFile)
            .Select(file =>
            {
                var fileInfo = new FileInfo(file);

                return new RepositoryFileMetadata(
                    fileInfo.FullName,
                    fileInfo.Length,
                    fileInfo.LastWriteTimeUtc);
            })
            .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RepositoryManifest(
            normalizedPath,
            CreateSignature(normalizedPath, files),
            files);
    }

    public List<IndexedFile> ReadFiles(RepositoryManifest manifest)
    {
        return manifest.Files
            .Select(file => new IndexedFile
            {
                Path = file.Path,
                Content = File.ReadAllText(file.Path),
                LastModified = file.LastModified
            })
            .ToList();
    }

    private static string CreateSignature(
        string repositoryPath,
        IReadOnlyList<RepositoryFileMetadata> files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(
                repositoryPath,
                file.Path);
            var metadata = $"{relativePath}|{file.Length}|{file.LastModified.Ticks}\n";

            hash.AppendData(Encoding.UTF8.GetBytes(metadata));
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static bool IsAllowedFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var pathParts = filePath.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        return !pathParts.Any(part =>
            IgnoredDirectories.Contains(part, StringComparer.OrdinalIgnoreCase));
    }
}
