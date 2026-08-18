namespace SaveHub.Core.Archiving;

/// <summary>How a game id was determined.</summary>
public readonly record struct GameIdResolution(string GameId, string Source)
{
    /// <summary>True unless the id fell back to <see cref="GameIdResolver.UnknownGame"/>.</summary>
    public bool Resolved => !string.Equals(GameId, GameIdResolver.UnknownGame, StringComparison.OrdinalIgnoreCase);
}
