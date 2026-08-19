using SaveHub.Core.Abstractions;
using SaveHub.Core.Archiving;
using SaveHub.Core.Models;

namespace SaveHub.Core;

/// <summary>
/// High-level entry point for frontends. Given a storage provider, it validates a request, builds
/// the archive/side-car artifacts, and hands them to the provider for upload.
/// </summary>
public sealed class SaveHubClient
{
    private const string LibraryIndexPath = "library.json";

    private static readonly string[] EmbeddedIconNames = ["ICON0.PNG"];

    private readonly ISaveStorageProvider _provider;
    private readonly ICoverArtResolver _coverArtResolver;

    /// <summary>The active storage provider.</summary>
    public ISaveStorageProvider Provider => _provider;

    public SaveHubClient(ISaveStorageProvider provider, ICoverArtResolver? coverArtResolver = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _coverArtResolver = coverArtResolver ?? new HttpCoverArtResolver();
    }

    /// <summary>Verifies connectivity and credentials against the backend without making changes.</summary>
    public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return _provider.TestConnectionAsync(cancellationToken);
    }

    public Task<IReadOnlyList<string>> ListPlatformsAsync(CancellationToken cancellationToken = default)
    {
        return _provider.ListPlatformsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<string>> ListGamesAsync(string platform, CancellationToken cancellationToken = default)
    {
        return _provider.ListGamesAsync(platform, cancellationToken);
    }

    public Task<IReadOnlyList<SaveEntry>> ListSavesAsync(string platform, string gameId, CancellationToken cancellationToken = default)
    {
        return _provider.ListSavesAsync(platform, gameId, cancellationToken);
    }

    /// <summary>
    /// Prepares a save into its archive artifacts without uploading. Resolves the icon from a
    /// user-supplied path, or by downloading cover art when enabled. When <paramref name="indexOverride"/>
    /// is provided, that index is used (for replacing an existing save) instead of appending.
    /// </summary>
    public async Task<PreparedSave> PrepareAsync(
        SaveUploadRequest request,
        int? indexOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        int index = indexOverride ?? await _provider
            .GetNextIndexAsync(request.Platform, request.GameId, request.SaveType, cancellationToken)
            .ConfigureAwait(false);
        CoverArt? icon = await ResolveIconAsync(request, cancellationToken).ConfigureAwait(false);
        bool iconIsExplicit = !string.IsNullOrWhiteSpace(request.IconPath);
        return SaveArchiveBuilder.Build(request, index, icon, iconIsExplicit);
    }

    private async Task<CoverArt?> ResolveIconAsync(SaveUploadRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.IconPath))
        {
            if (!File.Exists(request.IconPath))
            {
                throw new FileNotFoundException("Icon file not found.", request.IconPath);
            }
            string extension = Path.GetExtension(request.IconPath);
            return new CoverArt(File.ReadAllBytes(request.IconPath), string.IsNullOrEmpty(extension) ? ".png" : extension);
        }

        // Prefer downloaded box art; fall back to the save's own icon (e.g. a PS3 folder's ICON0.PNG).
        if (request.AutoFetchCoverArt)
        {
            CoverArt? cover = await _coverArtResolver
                .TryResolveAsync(request.Platform, request.GameId, cancellationToken)
                .ConfigureAwait(false);
            if (cover is not null)
            {
                return cover;
            }
        }

        if (FindEmbeddedIcon(request.Files) is { } embedded)
        {
            string extension = Path.GetExtension(embedded);
            return new CoverArt(File.ReadAllBytes(embedded), string.IsNullOrEmpty(extension) ? ".png" : extension);
        }

        return null;
    }

    /// <summary>Finds a save's own icon (e.g. a PS3 folder's ICON0.PNG) among the upload files.</summary>
    private static string? FindEmbeddedIcon(IReadOnlyList<string>? files)
    {
        if (files is null)
        {
            return null;
        }
        foreach (string path in files)
        {
            if (File.Exists(path) &&
                EmbeddedIconNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            {
                return path;
            }
        }
        return null;
    }

    /// <summary>Builds and uploads a save through the active provider.</summary>
    public async Task<SaveUploadResult> UploadAsync(
        SaveUploadRequest request,
        UploadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new UploadOptions();
        PreparedSave prepared = await PrepareAsync(request, options.TargetIndex, cancellationToken).ConfigureAwait(false);
        return await _provider
            .UploadAsync(prepared, options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Downloads a save archive's bytes, or null when it does not exist.</summary>
    public Task<byte[]?> DownloadArchiveAsync(string platform, string gameId, string archiveName, CancellationToken cancellationToken = default)
    {
        string path = $"{SaveNaming.GameFolder(platform, gameId)}/{archiveName}";
        return _provider.DownloadFileAsync(path, cancellationToken);
    }

    /// <summary>Downloads a save archive and writes it to <paramref name="destinationPath"/>.</summary>
    public async Task<bool> DownloadArchiveToFileAsync(
        string platform, string gameId, string archiveName, string destinationPath,
        CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await DownloadArchiveAsync(platform, gameId, archiveName, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return false;
        }
        string? dir = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllBytesAsync(destinationPath, bytes, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Reads the platform games index and returns a map of game id → game name.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetGameNamesAsync(string platform, CancellationToken cancellationToken = default)
    {
        string path = $"{SaveNaming.Sanitize(platform)}/{PlatformReadmeFormatter.FileName}";
        byte[]? bytes = await _provider.DownloadFileAsync(path, cancellationToken).ConfigureAwait(false);
        return bytes is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : PlatformReadmeFormatter.ParseGames(System.Text.Encoding.UTF8.GetString(bytes));
    }

    /// <summary>Downloads the game's cover icon bytes, or null when there is none.</summary>
    public async Task<byte[]?> GetGameIconAsync(string platform, string gameId, CancellationToken cancellationToken = default)
    {
        string folder = SaveNaming.GameFolder(platform, gameId);
        foreach (string name in new[] { "icon.jpg", "icon.png", "icon.jpeg", "icon.webp", "icon.bmp", "icon.gif" })
        {
            byte[]? bytes = await _provider.DownloadFileAsync($"{folder}/{name}", cancellationToken).ConfigureAwait(false);
            if (bytes is { Length: > 0 })
            {
                return bytes;
            }
        }
        return null;
    }

    /// <summary>Deletes a save archive and removes its row from the game's README index.</summary>
    public async Task<bool> DeleteSaveAsync(string platform, string gameId, string archiveName, CancellationToken cancellationToken = default)
    {
        string folder = SaveNaming.GameFolder(platform, gameId);
        bool deleted = await _provider.DeleteFileAsync($"{folder}/{archiveName}", cancellationToken).ConfigureAwait(false);

        string readmePath = $"{folder}/{GameReadmeFormatter.FileName}";
        byte[]? readmeBytes = await _provider.DownloadFileAsync(readmePath, cancellationToken).ConfigureAwait(false);
        if (readmeBytes is not null)
        {
            string updated = GameReadmeFormatter.RemoveRow(System.Text.Encoding.UTF8.GetString(readmeBytes), archiveName);
            await _provider.UploadFileAsync(readmePath, System.Text.Encoding.UTF8.GetBytes(updated), cancellationToken).ConfigureAwait(false);
        }
        return deleted;
    }

    /// <summary>Reads the consolidated library index (game names per platform), or an empty index.</summary>
    public async Task<LibraryIndex> GetLibraryIndexAsync(CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await _provider.DownloadFileAsync(LibraryIndexPath, cancellationToken).ConfigureAwait(false);
        return bytes is null ? new LibraryIndex() : LibraryIndex.Deserialize(bytes);
    }

    /// <summary>
    /// Rebuilds the whole library index from every platform's game list and per-platform README
    /// names, then writes <c>library.json</c> to the backend. One read per platform (never per game).
    /// </summary>
    public async Task<LibraryIndex> RebuildLibraryIndexAsync(CancellationToken cancellationToken = default)
    {
        LibraryIndex index = new LibraryIndex();
        foreach (string platform in await ListPlatformsAsync(cancellationToken).ConfigureAwait(false))
        {
            IReadOnlyDictionary<string, string> names = await GetGameNamesAsync(platform, cancellationToken).ConfigureAwait(false);
            foreach (string game in await ListGamesAsync(platform, cancellationToken).ConfigureAwait(false))
            {
                if (game.StartsWith('!'))
                {
                    // Skip the bulk memory-card index folder; it is not a game.
                    continue;
                }
                index.Set(platform, game, names.TryGetValue(game, out string? name) ? name : game);
            }
        }
        await _provider.UploadFileAsync(LibraryIndexPath, index.Serialize(), cancellationToken).ConfigureAwait(false);
        return index;
    }

    /// <summary>
    /// Sets (or renames) a game's display name in the per-platform README index and the root
    /// library index. Requires write access to the backend.
    /// </summary>
    public async Task SetGameNameAsync(string platform, string gameId, string name, CancellationToken cancellationToken = default)
    {
        string readmePath = $"{SaveNaming.Sanitize(platform)}/{PlatformReadmeFormatter.FileName}";
        byte[]? bytes = await _provider.DownloadFileAsync(readmePath, cancellationToken).ConfigureAwait(false);
        string existing = bytes is null ? string.Empty : System.Text.Encoding.UTF8.GetString(bytes);
        string updated = PlatformReadmeFormatter.Upsert(existing, platform, gameId, name);
        await _provider.UploadFileAsync(readmePath, System.Text.Encoding.UTF8.GetBytes(updated), cancellationToken).ConfigureAwait(false);

        LibraryIndex index = await GetLibraryIndexAsync(cancellationToken).ConfigureAwait(false);
        index.Set(platform, gameId, string.IsNullOrWhiteSpace(name) ? gameId : name.Trim());
        await _provider.UploadFileAsync(LibraryIndexPath, index.Serialize(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds or updates rows in a platform's bulk memory-card index (<c>PLATFORM/!index/README.md</c>),
    /// which catalogs each memory card with its game name, id, and cover art. Existing rows are merged
    /// by id. Requires write access to the backend.
    /// </summary>
    public async Task UpdateMemoryCardIndexAsync(
        string platform,
        IReadOnlyList<MemoryCardIndexEntry> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return;
        }
        string path = SaveNaming.MemoryCardIndexReadmePath(platform);
        byte[]? bytes = await _provider.DownloadFileAsync(path, cancellationToken).ConfigureAwait(false);
        string existing = bytes is null ? string.Empty : System.Text.Encoding.UTF8.GetString(bytes);
        string updated = MemoryCardIndexFormatter.Upsert(existing, platform, entries);
        await _provider.UploadFileAsync(path, System.Text.Encoding.UTF8.GetBytes(updated), cancellationToken).ConfigureAwait(false);
    }
}
