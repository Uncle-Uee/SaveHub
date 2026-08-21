namespace SaveHub.Core.Archiving;

/// <summary>
/// On-disk store of cover-art images keyed by platform + serial. Used both by
/// <see cref="CachingCoverArtResolver"/> (to avoid re-downloading) and by frontends (to preview
/// cached covers and to persist user-supplied cover art).
/// </summary>
public sealed class CoverArtCache
{
    private readonly string _rootDirectory;

    public CoverArtCache(string rootDirectory)
    {
        _rootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
    }

    /// <summary>The root cache directory.</summary>
    public string RootDirectory => _rootDirectory;

    /// <summary>Returns the path of a cached cover for the platform/serial, or null when none exists.</summary>
    public string? FindCachedPath(string platform, string serial)
    {
        if (string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(serial))
        {
            return null;
        }
        string folder = Path.Combine(_rootDirectory, Sanitize(platform));
        if (!Directory.Exists(folder))
        {
            return null;
        }
        string key = Sanitize(serial);
        if (CoverArtSource.Resolve(platform, serial) is { } source)
        {
            string preferred = Path.Combine(folder, key + source.Extension);
            if (File.Exists(preferred))
            {
                return preferred;
            }
        }
        foreach (string file in Directory.EnumerateFiles(folder, key + ".*"))
        {
            return file;
        }
        return null;
    }

    /// <summary>Reads a cached cover's bytes, or null when none is cached.</summary>
    public byte[]? TryRead(string platform, string serial)
    {
        string? path = FindCachedPath(platform, serial);
        if (path is null)
        {
            return null;
        }
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Writes a cover to the cache (best-effort).</summary>
    public void Store(string platform, string serial, byte[] content, string extension)
    {
        if (string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(serial) || content is null || content.Length == 0)
        {
            return;
        }
        try
        {
            string folder = Path.Combine(_rootDirectory, Sanitize(platform));
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, Sanitize(serial) + NormalizeExtension(extension)), content);
        }
        catch (IOException)
        {
            // Caching is best-effort.
        }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".img";
        }
        return extension.StartsWith('.') ? extension : "." + extension;
    }

    private static string Sanitize(string value)
    {
        string result = value.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(invalid, '_');
        }
        return result;
    }
}
