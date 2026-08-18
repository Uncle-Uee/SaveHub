namespace SaveHub.Core.Archiving;

/// <summary>Downloads cover art from the public sources in <see cref="CoverArtSource"/>.</summary>
public sealed class HttpCoverArtResolver : ICoverArtResolver
{
    private static readonly HttpClient SharedClient = new();
    private readonly HttpClient _http;

    public HttpCoverArtResolver(HttpClient? http = null)
    {
        _http = http ?? SharedClient;
    }

    public async Task<CoverArt?> TryResolveAsync(string platform, string serial, CancellationToken cancellationToken = default)
    {
        if (CoverArtSource.Resolve(platform, serial) is not { } source)
        {
            return null;
        }

        try
        {
            using HttpResponseMessage response = await _http.GetAsync(source.Url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            return bytes.Length == 0 ? null : new CoverArt(bytes, source.Extension);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}
