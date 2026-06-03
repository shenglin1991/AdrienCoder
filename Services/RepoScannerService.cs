using System.Text;

namespace AdrienCoder.Api.Services;

using AdrienCoder.Api.Models;
using System.Text;

public class RepoScannerService
{
    private static readonly string[] AllowedExtensions =
    {
        ".cs", ".ts", ".html", ".scss", ".json", ".md", ".yml", ".yaml"
    };

    private static readonly string[] IgnoredDirectories =
    {
        "node_modules", "bin", "obj", ".git", "dist", "coverage", ".angular", ".nx", ".vs"
    };

    private static readonly string[] ImportantFiles =
    {
        "Program.cs", "appsettings.json", ".csproj", "package.json", "angular.json", "nx.json"
    };

    public string BuildContext(string repoPath, string question, int maxFiles = 20)
    {
        if (!Directory.Exists(repoPath))
            throw new DirectoryNotFoundException($"Repo path not found: {repoPath}");

        var questionWords = question
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => w.ToLowerInvariant())
            .Where(w => w.Length >= 3)
            .ToList();

        var files = Directory
            .EnumerateFiles(repoPath, "*.*", SearchOption.AllDirectories)
            .Where(IsAllowedFile)
            .Select(file => new
            {
                File = file,
                Score = ScoreFile(file, repoPath, questionWords)
            })
            .OrderByDescending(x => x.Score)
            .Take(maxFiles)
            .Select(x => x.File)
            .ToList();

        var sb = new StringBuilder();

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(repoPath, file);
            var content = File.ReadAllText(file);

            if (content.Length > 6000)
                content = content[..6000];

            sb.AppendLine($"--- FILE: {relativePath} ---");
            sb.AppendLine(content);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public List<IndexedFile> IndexRepo(string repoPath)
    {
        var files = Directory
            .EnumerateFiles(repoPath, "*.*", SearchOption.AllDirectories)
            .Where(IsAllowedFile)
            .Select(file => new IndexedFile
            {
                Path = file,
                Content = File.ReadAllText(file),
                LastModified = File.GetLastWriteTimeUtc(file)
            })
            .ToList();

        return files;
    }

    public string BuildContextFromIndex(
    IReadOnlyList<IndexedFile> indexedFiles,
    string question,
    int maxFiles = 20)
    {
        var questionWords = question
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => w.ToLowerInvariant())
            .Where(w => w.Length >= 3)
            .ToList();

        var selectedFiles = indexedFiles
            .Select(file => new
            {
                File = file,
                Score = ScoreIndexedFile(file, questionWords)
            })
            .OrderByDescending(x => x.Score)
            .Take(maxFiles)
            .Select(x => x.File)
            .ToList();

        var sb = new StringBuilder();

        foreach (var file in selectedFiles)
        {
            var content = file.Content;

            if (content.Length > 6000)
                content = content[..6000];

            sb.AppendLine($"--- FILE: {file.Path} ---");
            sb.AppendLine(content);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static int ScoreIndexedFile(IndexedFile file, List<string> questionWords)
    {
        var score = 0;
        var path = file.Path.ToLowerInvariant();
        var content = file.Content.ToLowerInvariant();

        foreach (var word in questionWords)
        {
            if (path.Contains(word))
                score += 5;

            if (content.Contains(word))
                score += 2;
        }

        return score;
    }

    private static int ScoreFile(string filePath, string repoPath, List<string> questionWords)
    {
        var score = 0;
        var relativePath = Path.GetRelativePath(repoPath, filePath).ToLowerInvariant();
        var fileName = Path.GetFileName(filePath);

        if (ImportantFiles.Any(f => fileName.Contains(f, StringComparison.OrdinalIgnoreCase)))
            score += 10;

        foreach (var word in questionWords)
        {
            if (relativePath.Contains(word))
                score += 5;
        }

        var content = File.ReadAllText(filePath).ToLowerInvariant();

        foreach (var word in questionWords)
        {
            if (content.Contains(word))
                score += 2;
        }

        return score;
    }

    private static bool IsAllowedFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return false;

        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return !parts.Any(part =>
            IgnoredDirectories.Contains(part, StringComparer.OrdinalIgnoreCase));
    }
}