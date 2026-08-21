namespace SaveHub.Core.Archiving;

/// <summary>
/// Wraps another <see cref="ICoverArtResolver"/> and caches downloaded covers via a
/// <see cref="CoverArtCache"/>, so the same cover is not fetched again on later uploads.
/// </summary>
public sealed class CachingCoverArtResolver : ICoverArtResolver
{
    private readonly ICoverArtResolver _inner;
    private readonly CoverArtCache _cache;

    public CachingCoverArtResolver(ICoverArtResolver inner, CoverArtCache cache)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<CoverArt?> TryResolveAsync(string platform, string serial, CancellationToken cancellationToken = default)
    {
        if (CoverArtSource.Resolve(platform, serial) is not { } source)
        {
            return null;
        }

        byte[]? cached = _cache.TryRead(platform, serial);
        if (cached is not null && cached.Length > 0)
        {
            return new CoverArt(cached, source.Extension);
        }

        CoverArt? resolved = await _inner.TryResolveAsync(platform, serial, cancellationToken).ConfigureAwait(false);
        if (resolved is { } art && art.Content.Length > 0)
        {
            _cache.Store(platform, serial, art.Content, art.Extension);
        }
        return resolved;
    }
}
