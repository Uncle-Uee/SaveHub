using System.Text;

namespace SaveHub.Core.Archiving;

/// <summary>
/// Builds and updates the per-platform <c>README.md</c> that lists every game with a save in that
/// platform folder (title id + game name), similar to the Apollo save database's games index.
/// </summary>
public static class PlatformReadmeFormatter
{
    /// <summary>File name of the platform games index.</summary>
    public const string FileName = "README.md";

    /// <summary>
    /// Returns the README contents for <paramref name="platform"/> after adding or updating the row
    /// for <paramref name="gameId"/>. Rows are kept sorted by title id.
    /// </summary>
    public static string Upsert(string? existingContent, string platform, string gameId, string? gameTitle)
    {
        Dictionary<string, string> games = Parse(existingContent);
        games[gameId] = string.IsNullOrWhiteSpace(gameTitle) ? gameId : gameTitle.Trim();
        return Render(platform, games);
    }

    private static string Render(string platform, IDictionary<string, string> games)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"# {platform} Saves");
        builder.AppendLine();
        builder.AppendLine($"Games with memory cards or save states stored in this `{platform}` folder.");
        builder.AppendLine("Each row links to the game's folder, named by its title id.");
        builder.AppendLine();
        builder.AppendLine("| Title ID | Game |");
        builder.AppendLine("| --- | --- |");
        foreach (string id in games.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"| [{id}]({id}) | {games[id]} |");
        }
        builder.AppendLine();
        builder.AppendLine("_Maintained by SaveHub._");
        return builder.ToString();
    }

    /// <summary>Parses the games index into a map of game id → game name.</summary>
    public static IReadOnlyDictionary<string, string> ParseGames(string? content)
    {
        return Parse(content);
    }

    private static Dictionary<string, string> Parse(string? content)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
            if (cells.Length < 2)
            {
                continue;
            }

            string id = StripLink(cells[0].Trim());
            string title = cells[1].Trim();
            if (id.Length == 0 || id.Equals("Title ID", StringComparison.OrdinalIgnoreCase) || id.StartsWith("---"))
            {
                continue;
            }

            result[id] = title;
        }

        return result;
    }

    // Turns a markdown link "[SLUS-20073](SLUS-20073)" back into "SLUS-20073".
    private static string StripLink(string value)
    {
        if (value.StartsWith('[') && value.Contains(']'))
        {
            int end = value.IndexOf(']');
            return value[1..end];
        }
        return value;
    }
}
