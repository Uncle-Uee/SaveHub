using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using SaveHub.Core.Abstractions;
using SaveHub.Core.Archiving;
using SaveHub.Core.Models;

namespace SaveHub.Supabase;

/// <summary>
/// Stores the save database in a Supabase Storage bucket. Uploads publish directly (owner) or land
/// under a <c>pending/</c> prefix for review. The bucket ends up with the same folder layout as the
/// GitHub provider because all artifacts are built by <c>SaveHub.Core</c>.
/// </summary>
public sealed class SupabaseSaveStorageProvider : ISaveStorageProvider
{
    #region Fields & Properties

    private readonly SupabaseProviderSettings _settings;
    private readonly HttpClient _http;
    private readonly string _bucket;

    public string Name => "supabase";

    public StorageProviderCapabilities Capabilities { get; } = new()
    {
        SupportsPullRequests = true,   // modeled as the pending/ prefix
        SupportsAutoMerge = true,      // owner publishes directly
        SupportsBrowsing = true,
    };

    #endregion

    #region Constructors

    public SupabaseSaveStorageProvider(SupabaseProviderSettings settings, HttpClient? http = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        if (string.IsNullOrWhiteSpace(settings.Url) || string.IsNullOrWhiteSpace(settings.Bucket))
        {
            throw new ArgumentException("Supabase settings require both Url and Bucket.");
        }
        string key = settings.ResolveKey()
            ?? throw new InvalidOperationException(
                $"No Supabase key found. Set it in the config or the '{settings.ApiKeyEnvironmentVariable}' env var.");

        _bucket = settings.Bucket;
        _http = http ?? new HttpClient();
        _http.BaseAddress ??= new Uri(settings.Url.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Remove("apikey");
        _http.DefaultRequestHeaders.Add("apikey", key);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    #endregion

    #region Private Static Methods

    // Supabase list returns folders with a null id.
    private static bool IsFolder(StorageObject o)
    {
        return o.Id is null;
    }

    #endregion

    #region Public Methods

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await ListAsync(string.Empty, cancellationToken).ConfigureAwait(false);
            return new ConnectionTestResult
            {
                Success = true,
                Target = $"{_settings.Url}/{_bucket}",
                HasWriteAccess = _settings.IsOwner,
                AutoMergeEffective = _settings.IsOwner,
                Message = _settings.IsOwner
                    ? "Connected. Uploads publish directly to the bucket."
                    : "Connected. Uploads go to 'pending/' for the owner to review.",
            };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Target = $"{_settings.Url}/{_bucket}",
                Message = $"Could not reach the bucket: {ex.Message}",
            };
        }
    }

    public async Task<IReadOnlyList<string>> ListPlatformsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StorageObject> items = await ListAsync(string.Empty, cancellationToken).ConfigureAwait(false);
        return items.Where(IsFolder).Select(i => i.Name)
            .Where(n => !n.StartsWith("pending", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<IReadOnlyList<string>> ListGamesAsync(string platform, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StorageObject> items = await ListAsync($"{SaveNaming.Sanitize(platform)}/", cancellationToken).ConfigureAwait(false);
        return items.Where(IsFolder).Select(i => i.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<IReadOnlyList<SaveEntry>> ListSavesAsync(string platform, string gameId, CancellationToken cancellationToken = default)
    {
        string folder = SaveNaming.GameFolder(platform, gameId);
        IReadOnlyList<StorageObject> items = await ListAsync($"{folder}/", cancellationToken).ConfigureAwait(false);
        string? readme = await ReadTextAsync($"{folder}/{GameReadmeFormatter.FileName}", cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, string> descriptions = GameReadmeFormatter.ParseDescriptions(readme);

        List<SaveEntry> result = new List<SaveEntry>();
        foreach (StorageObject item in items)
        {
            if (IsFolder(item) || !SaveNaming.TryParseArchiveName(item.Name, out int index, out SaveType type))
            {
                continue;
            }
            descriptions.TryGetValue(item.Name, out string? description);
            result.Add(new SaveEntry
            {
                Platform = SaveNaming.Sanitize(platform),
                GameId = SaveNaming.Sanitize(gameId),
                SaveType = type,
                Index = index,
                ArchiveName = item.Name,
                Description = description,
            });
        }
        return result.OrderBy(e => e.SaveType).ThenBy(e => e.Index).ToArray();
    }

    public async Task<int> GetNextIndexAsync(string platform, string gameId, SaveType saveType, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SaveEntry> saves = await ListSavesAsync(platform, gameId, cancellationToken).ConfigureAwait(false);
        return saves.Where(s => s.SaveType == saveType).Select(s => s.Index).DefaultIfEmpty(0).Max() + 1;
    }

    public async Task<SaveUploadResult> UploadAsync(PreparedSave save, UploadOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(options);

        bool publish = _settings.IsOwner && (options.AutoMerge ?? true);
        string prefix = publish ? string.Empty : "pending/";

        List<StorageFile> files = new List<StorageFile> { save.Archive };

        // Icon reuse: keep an existing icon unless the user explicitly supplied one.
        string? existingIcon = await FindExistingIconAsync(prefix + save.GameFolder, cancellationToken).ConfigureAwait(false);
        string? iconFileName = existingIcon;
        if (save.Icon is { } icon && (save.IconIsExplicit || existingIcon is null))
        {
            files.Add(icon);
            iconFileName = icon.Path[(icon.Path.LastIndexOf('/') + 1)..];
        }

        string gameReadmePath = $"{save.GameFolder}/{GameReadmeFormatter.FileName}";
        string gameReadme = GameReadmeFormatter.Upsert(
            await ReadTextAsync(prefix + gameReadmePath, cancellationToken).ConfigureAwait(false),
            save.Platform, save.GameId, save.GameTitle, save.Index, save.SaveType, save.Description, iconFileName);
        files.Add(new StorageFile(gameReadmePath, Encoding.UTF8.GetBytes(gameReadme)));

        string platformReadmePath = $"{SaveNaming.Sanitize(save.Platform)}/{PlatformReadmeFormatter.FileName}";
        string platformReadme = PlatformReadmeFormatter.Upsert(
            await ReadTextAsync(prefix + platformReadmePath, cancellationToken).ConfigureAwait(false),
            save.Platform, save.GameId, save.GameTitle);
        files.Add(new StorageFile(platformReadmePath, Encoding.UTF8.GetBytes(platformReadme)));

        foreach (StorageFile file in files)
        {
            await UploadObjectAsync(prefix + file.Path, file.Content, cancellationToken).ConfigureAwait(false);
        }

        return new SaveUploadResult
        {
            Success = true,
            Merged = publish,
            ArchivePath = prefix + save.Archive.Path,
            Message = publish
                ? $"Published {save.Archive.Path}."
                : $"Submitted for review at pending/{save.Archive.Path}.",
        };
    }

    public async Task<byte[]?> DownloadFileAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http
            .GetAsync($"storage/v1/object/{_bucket}/{Uri.EscapeDataString(repositoryPath).Replace("%2F", "/")}", cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task UploadFileAsync(string repositoryPath, byte[] content, CancellationToken cancellationToken = default)
    {
        return UploadObjectAsync(repositoryPath, content, cancellationToken);
    }

    public async Task<bool> DeleteFileAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http
            .DeleteAsync($"storage/v1/object/{_bucket}/{Uri.EscapeDataString(repositoryPath).Replace("%2F", "/")}", cancellationToken)
            .ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    #endregion

    #region Private Methods

    private async Task<string?> FindExistingIconAsync(string folder, CancellationToken ct)
    {
        IReadOnlyList<StorageObject> items = await ListAsync(folder.TrimEnd('/') + "/", ct).ConfigureAwait(false);
        return items.FirstOrDefault(i => !IsFolder(i) &&
            i.Name.StartsWith("icon.", StringComparison.OrdinalIgnoreCase))?.Name;
    }

    private async Task UploadObjectAsync(string path, byte[] content, CancellationToken ct)
    {
        using ByteArrayContent body = new ByteArrayContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"storage/v1/object/{_bucket}/{path}")
        {
            Content = body,
        };
        request.Headers.Add("x-upsert", "true");
        using HttpResponseMessage response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string?> ReadTextAsync(string path, CancellationToken ct)
    {
        byte[]? bytes = await DownloadFileAsync(path, ct).ConfigureAwait(false);
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }

    private async Task<IReadOnlyList<StorageObject>> ListAsync(string prefix, CancellationToken ct)
    {
        ListRequest payload = new ListRequest { Prefix = prefix, Limit = 1000 };
        using HttpResponseMessage response = await _http
            .PostAsJsonAsync($"storage/v1/object/list/{_bucket}", payload, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<StorageObject>();
        }
        List<StorageObject>? items = await response.Content.ReadFromJsonAsync<List<StorageObject>>(cancellationToken: ct).ConfigureAwait(false);
        return items ?? [];
    }

    #endregion

    #region Nested Types

    private sealed class ListRequest
    {
        [JsonPropertyName("prefix")] public string Prefix { get; set; } = string.Empty;
        [JsonPropertyName("limit")] public int Limit { get; set; } = 1000;
        [JsonPropertyName("offset")] public int Offset { get; set; }
    }

    private sealed class StorageObject
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("id")] public string? Id { get; set; }
    }

    #endregion
}
