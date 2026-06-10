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

    public List<IndexedFile> IndexRepo(string repoPath)
    {
        return Directory
            .EnumerateFiles(repoPath, "*.*", SearchOption.AllDirectories)
            .Where(IsAllowedFile)
            .Select(file => new IndexedFile
            {
                Path = file,
                Content = File.ReadAllText(file),
                LastModified = File.GetLastWriteTimeUtc(file)
            })
            .ToList();
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
