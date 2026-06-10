using AdrienCoder.Api.Features.Indexing.Models;

namespace AdrienCoder.Api.Features.Indexing.Services;

/// <summary>
/// Splits source files into overlapping text windows suitable for embeddings.
/// </summary>
public class CodeChunkerService
{
    public List<CodeChunk> ChunkFiles(
        IReadOnlyList<IndexedFile> files,
        int chunkSize = 1200,
        int overlap = 200)
    {
        var chunks = new List<CodeChunk>();

        foreach (var file in files)
        {
            var content = file.Content;

            if (string.IsNullOrWhiteSpace(content))
                continue;

            var start = 0;
            var chunkIndex = 0;

            while (start < content.Length)
            {
                var length = Math.Min(chunkSize, content.Length - start);
                var chunkContent = content.Substring(start, length);

                chunks.Add(new CodeChunk
                {
                    Id = $"{file.Path}::{chunkIndex}",
                    FilePath = file.Path,
                    Content = chunkContent,
                    ChunkIndex = chunkIndex
                });

                chunkIndex++;

                if (start + chunkSize >= content.Length)
                {
                    break;
                }

                // Overlap keeps context that would otherwise be cut at chunk boundaries.
                start += chunkSize - overlap;
            }
        }

        return chunks;
    }
}
