using System.Text;

namespace SaveHub.Core.Archiving;

/// <summary>
/// Builds and updates a platform's bulk memory-card index <c>README.md</c> (stored under the
/// top-sorting <c>!index</c> folder). Each row catalogs a memory card with a small cover-art
/// thumbnail, the game name, and the game id (title id where one exists). Rows are merged by id.
/// </summary>
public static class MemoryCardIndexFormatter
{
    /// <summary>File name of the memory-card index.</summary>
    public const string FileName = "README.md";

    /// <summary>Width, in pixels, of the cover-art thumbnails rendered in the index.</summary>
    private const int ThumbnailWidth = 72;

    /// <summary>
    /// Returns the index contents for <paramref name="platform"/> after adding or updating a row for
    /// each entry. Rows already present (matched by id) are replaced; the rest are preserved.
    /// </summary>
    public static string Upsert(string? existingContent, string platform, IReadOnlyList<MemoryCardIndexEntry> entries)
    {
        Dictionary<string, MemoryCardIndexEntry> rows = Parse(existingContent);
        foreach (MemoryCardIndexEntry entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.TitleId))
            {
                continue;
            }
            rows[entry.TitleId.Trim()] = new MemoryCardIndexEntry(entry.TitleId.Trim(), Clean(entry.GameName));
        }
        return Render(platform, rows.Values);
    }

    /// <summary>Reads the id → entry map from an existing memory-card index.</summary>
    public static IReadOnlyList<MemoryCardIndexEntry> ParseEntries(string? content)
    {
        return Parse(content).Values.ToList();
    }

    private static string Render(string platform, IEnumerable<MemoryCardIndexEntry> entries)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"# {platform} Memory Card Index");
        builder.AppendLine();
        builder.AppendLine($"Every memory card uploaded to this `{platform}` folder, with cover art.");
        builder.AppendLine();
        builder.AppendLine("| Cover | Game | ID |");
        builder.AppendLine("| --- | --- | --- |");
        foreach (MemoryCardIndexEntry entry in entries.OrderBy(e => e.GameName, StringComparer.OrdinalIgnoreCase))
        {
            string cover = CoverArtSource.Resolve(platform, entry.TitleId) is { } art
                ? $"<img src=\"{art.Url}\" width=\"{ThumbnailWidth}\" alt=\"{Clean(entry.GameName)}\">"
                : "—";
            builder.AppendLine($"| {cover} | {Clean(entry.GameName)} | `{entry.TitleId}` |");
        }
        builder.AppendLine();
        builder.AppendLine("_Maintained by SaveHub._");
        return builder.ToString();
    }

    private static Dictionary<string, MemoryCardIndexEntry> Parse(string? content)
    {
        Dictionary<string, MemoryCardIndexEntry> result = new Dictionary<string, MemoryCardIndexEntry>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(content))
        {
            return result;
        }

        foreach (string raw in content.Split('\n'))
        {
            string line = raw.Trim();
            if (!line.StartsWith('|'))
            {
                continue;
            }

            string[] cells = line.Trim('|').Split('|');
            if (cells.Length < 3)
            {
                continue;
            }

            string game = cells[1].Trim();
            string id = cells[2].Trim().Trim('`').Trim();
            if (id.Length == 0 || id.Equals("ID", StringComparison.OrdinalIgnoreCase) || id.StartsWith("---"))
            {
                continue;
            }

            result[id] = new MemoryCardIndexEntry(id, game);
        }
        return result;
    }

    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }
        return value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
}

/// <summary>A single memory-card row in a platform's bulk index: the game id and its display name.</summary>
public readonly record struct MemoryCardIndexEntry(string TitleId, string GameName);
