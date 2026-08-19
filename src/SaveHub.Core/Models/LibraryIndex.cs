using System.Text.Json;

namespace SaveHub.Core.Models;

/// <summary>
/// Consolidated root index (<c>library.json</c>) mapping each platform to its game id → name
/// entries. Lets a frontend read every game name for the whole backup in a single request instead
/// of one per platform, and is easy to update (rebuild) or cache locally.
/// </summary>
public sealed class LibraryIndex
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Schema version, for forward compatibility.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Platform code → (game id → game name).</summary>
    public Dictionary<string, Dictionary<string, string>> Platforms { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Adds or updates a game's name under a platform.</summary>
    public void Set(string platform, string gameId, string name)
    {
        if (!Platforms.TryGetValue(platform, out Dictionary<string, string>? games))
        {
            games = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Platforms[platform] = games;
        }
        games[gameId] = name;
    }

    /// <summary>Removes a game entry; returns true when it existed.</summary>
    public bool Remove(string platform, string gameId)
    {
        return Platforms.TryGetValue(platform, out Dictionary<string, string>? games) && games.Remove(gameId);
    }

    /// <summary>Returns the game id → name map for a platform (empty when unknown).</summary>
    public IReadOnlyDictionary<string, string> ForPlatform(string platform)
    {
        return Platforms.TryGetValue(platform, out Dictionary<string, string>? games)
            ? games
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public byte[] Serialize()
    {
        return JsonSerializer.SerializeToUtf8Bytes(this, Options);
    }

    public static LibraryIndex Deserialize(byte[] bytes)
    {
        try
        {
            return JsonSerializer.Deserialize<LibraryIndex>(bytes, Options) ?? new LibraryIndex();
        }
        catch (JsonException)
        {
            return new LibraryIndex();
        }
    }
}
