using System.Text;
using SaveHub.Core.Models;

namespace SaveHub.Core.Archiving;

/// <summary>
/// Builds and updates the per-game <c>README.md</c> that lists every save in a game folder with its
/// archive file name (e.g. <c>01.zip</c>), type, and description, so a downloader can tell at a
/// glance what each save is for. This replaces the older per-save side-car <c>.txt</c> files and the
/// <c>saves.txt</c> index.
/// </summary>
public static class GameReadmeFormatter
{
    /// <summary>File name of the per-game saves index.</summary>
    public const string FileName = "README.md";

    /// <summary>
    /// Returns the README contents after adding or updating the row for the given archive. Rows are
    /// ordered by save type then archive name. When <paramref name="iconFileName"/> is provided, the
    /// cover image is embedded at the top.
    /// </summary>
    public static string Upsert(
        string? existingContent,
        string platform,
        string gameId,
        string? gameTitle,
        int index,
        SaveType saveType,
        string description,
        string? iconFileName = null)
    {
        Dictionary<string, Row> rows = Parse(existingContent);
        string archive = SaveNaming.ArchiveName(index, saveType);
        rows[archive] = new Row(archive, saveType, Clean(description));
        return Render(platform, gameId, gameTitle, rows.Values, iconFileName);
    }

    /// <summary>Reads the archive-name → description map from an existing game README.</summary>
    public static IReadOnlyDictionary<string, string> ParseDescriptions(string? content)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Row row in Parse(content).Values)
        {
            result[row.Archive] = row.Description;
        }
        return result;
    }

    private static string Render(string platform, string gameId, string? gameTitle, IEnumerable<Row> rows, string? iconFileName)
    {
        string title = string.IsNullOrWhiteSpace(gameTitle) ? gameId : $"{gameTitle.Trim()} ({gameId})";
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"# {title}");
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(iconFileName))
        {
            builder.AppendLine($"![Cover]({iconFileName})");
            builder.AppendLine();
        }
        builder.AppendLine($"Platform: {platform}");
        builder.AppendLine();
        builder.AppendLine("Saves stored in this folder:");
        builder.AppendLine();
        builder.AppendLine("| Save | Type | Description |");
        builder.AppendLine("| --- | --- | --- |");
        foreach (Row row in rows.OrderBy(r => r.SaveType).ThenBy(r => r.Archive, StringComparer.OrdinalIgnoreCase))
        {
            string typeLabel = SaveNaming.Label(row.SaveType);
            builder.AppendLine($"| [{row.Archive}]({row.Archive}) | {typeLabel} | {row.Description} |");
        }
        builder.AppendLine();
        builder.AppendLine("_Maintained by SaveHub._");
        return builder.ToString();
    }

    private static Dictionary<string, Row> Parse(string? content)
    {
        Dictionary<string, Row> result = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
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

            string archive = StripLink(cells[0].Trim());
            if (archive.Length == 0 ||
                archive.Equals("Save", StringComparison.OrdinalIgnoreCase) ||
                archive.StartsWith("---"))
            {
                continue;
            }

            if (!SaveNaming.TryParseArchiveName(archive, out _, out SaveType type))
            {
                continue;
            }

            result[archive] = new Row(archive, type, cells[2].Trim());
        }

        return result;
    }

    private static string StripLink(string value)
    {
        if (value.StartsWith('[') && value.Contains(']'))
        {
            return value[1..value.IndexOf(']')];
        }
        return value;
    }

    /// <summary>Returns the README with the table row for <paramref name="archiveName"/> removed.</summary>
    public static string RemoveRow(string? content, string archiveName)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content ?? string.Empty;
        }

        List<string> kept = new List<string>();
        foreach (string raw in content.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            string trimmed = line.Trim();
            if (trimmed.StartsWith('|'))
            {
                string[] cells = trimmed.Trim('|').Split('|');
                if (cells.Length >= 1 &&
                    string.Equals(StripLink(cells[0].Trim()), archiveName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }
            kept.Add(line);
        }
        return string.Join('\n', kept);
    }

    private static string Clean(string description)
    {
        return (description ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Replace("|", "\\|").Trim();
    }

    private readonly record struct Row(string Archive, SaveType SaveType, string Description);
}
