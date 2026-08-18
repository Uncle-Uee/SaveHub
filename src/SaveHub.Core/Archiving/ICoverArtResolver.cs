namespace SaveHub.Core.Archiving;

/// <summary>Resolves cover art for a save. Implementations may hit the network.</summary>
public interface ICoverArtResolver
{
    Task<CoverArt?> TryResolveAsync(string platform, string serial, CancellationToken cancellationToken = default);
}
