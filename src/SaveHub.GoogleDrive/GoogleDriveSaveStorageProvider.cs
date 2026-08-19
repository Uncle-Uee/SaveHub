using System.Net;
using System.Text;
using Google;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using SaveHub.Core.Abstractions;
using SaveHub.Core.Archiving;
using SaveHub.Core.Models;
using DriveData = Google.Apis.Drive.v3.Data;

namespace SaveHub.GoogleDrive;

/// <summary>
/// Stores the save database inside a shared Google Drive folder. Drive addresses items by opaque
/// file ids, so this provider resolves/creates folders on demand (with a small path→id cache) and
/// upserts files by name. The resulting tree matches the GitHub/Supabase layout.
/// </summary>
public sealed class GoogleDriveSaveStorageProvider : ISaveStorageProvider
{
    private const string FolderMime = "application/vnd.google-apps.folder";

    private readonly DriveService _drive;
    private readonly GoogleDriveProviderSettings _settings;
    private readonly Dictionary<string, string> _folderCache = new(StringComparer.OrdinalIgnoreCase);
    private string? _rootId;

    public string Name => "googledrive";

    public StorageProviderCapabilities Capabilities { get; } = new()
    {
        SupportsPullRequests = true,   // modeled as the pending/ folder
        SupportsAutoMerge = true,      // owner publishes directly
        SupportsBrowsing = true,
    };

    public GoogleDriveSaveStorageProvider(DriveService drive, GoogleDriveProviderSettings settings)
    {
        _drive = drive ?? throw new ArgumentNullException(nameof(drive));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string? rootId = await GetRootFolderIdAsync(create: true, cancellationToken).ConfigureAwait(false);
            string name = _settings.RootFolderName;
            if (!string.IsNullOrWhiteSpace(_settings.RootFolderId) && rootId is not null)
            {
                FilesResource.GetRequest get = _drive.Files.Get(rootId);
                get.Fields = "id,name";
                name = (await get.ExecuteAsync(cancellationToken).ConfigureAwait(false)).Name;
            }
            return new ConnectionTestResult
            {
                Success = true,
                AuthenticatedAs = GoogleDriveSession.Current?.AccountEmail,
                Target = name,
                HasWriteAccess = _settings.IsOwner,
                AutoMergeEffective = _settings.IsOwner,
                Message = $"Connected. Using the '{name}' folder in your Google Drive.",
            };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult
            {
                Success = false,
                AuthenticatedAs = GoogleDriveSession.Current?.AccountEmail,
                Message = $"Could not access Google Drive: {ex.Message}",
            };
        }
    }

    public async Task<IReadOnlyList<string>> ListPlatformsAsync(CancellationToken cancellationToken = default)
    {
        string? folderId = await ResolveFolderIdAsync(string.Empty, create: false, cancellationToken).ConfigureAwait(false);
        if (folderId is null)
        {
            return [];
        }
        List<DriveData.File> children = await ChildrenAsync(folderId, cancellationToken).ConfigureAwait(false);
        return children.Where(IsFolder)
            .Select(c => c.Name)
            .Where(n => !n.StartsWith("pending", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<IReadOnlyList<string>> ListGamesAsync(string platform, CancellationToken cancellationToken = default)
    {
        string? folderId = await ResolveFolderIdAsync(SaveNaming.Sanitize(platform), create: false, cancellationToken).ConfigureAwait(false);
        if (folderId is null)
        {
            return [];
        }
        List<DriveData.File> children = await ChildrenAsync(folderId, cancellationToken).ConfigureAwait(false);
        return children.Where(IsFolder).Select(c => c.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<IReadOnlyList<SaveEntry>> ListSavesAsync(string platform, string gameId, CancellationToken cancellationToken = default)
    {
        string folder = SaveNaming.GameFolder(platform, gameId);
        string? folderId = await ResolveFolderIdAsync(folder, create: false, cancellationToken).ConfigureAwait(false);
        if (folderId is null)
        {
            return [];
        }
        List<DriveData.File> children = await ChildrenAsync(folderId, cancellationToken).ConfigureAwait(false);
        string? readme = await ReadTextAsync($"{folder}/{GameReadmeFormatter.FileName}", cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, string> descriptions = GameReadmeFormatter.ParseDescriptions(readme);

        List<SaveEntry> result = new List<SaveEntry>();
        foreach (DriveData.File child in children)
        {
            if (IsFolder(child) || !SaveNaming.TryParseArchiveName(child.Name, out int index, out SaveType type))
            {
                continue;
            }
            descriptions.TryGetValue(child.Name, out string? description);
            result.Add(new SaveEntry
            {
                Platform = SaveNaming.Sanitize(platform),
                GameId = SaveNaming.Sanitize(gameId),
                SaveType = type,
                Index = index,
                ArchiveName = child.Name,
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
            await UpsertFileAsync(prefix + file.Path, file.Content, cancellationToken).ConfigureAwait(false);
        }

        return new SaveUploadResult
        {
            Success = true,
            Merged = publish,
            ArchivePath = prefix + save.Archive.Path,
            Message = publish
                ? $"Published {save.Archive.Path} to Google Drive."
                : $"Submitted for review at pending/{save.Archive.Path}.",
        };
    }

    public async Task<byte[]?> DownloadFileAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        (string folderPath, string name) = SplitPath(repositoryPath);
        string? folderId = await ResolveFolderIdAsync(folderPath, create: false, cancellationToken).ConfigureAwait(false);
        if (folderId is null)
        {
            return null;
        }
        DriveData.File? file = (await ChildrenAsync(folderId, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(c => !IsFolder(c) && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (file is null)
        {
            return null;
        }

        using MemoryStream stream = new MemoryStream();
        IDownloadProgress progress = await _drive.Files.Get(file.Id).DownloadAsync(stream, cancellationToken).ConfigureAwait(false);
        return progress.Status == DownloadStatus.Completed ? stream.ToArray() : null;
    }

    public Task UploadFileAsync(string repositoryPath, byte[] content, CancellationToken cancellationToken = default)
    {
        return UpsertFileAsync(repositoryPath, content, cancellationToken);
    }

    public async Task<bool> DeleteFileAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        (string folderPath, string name) = SplitPath(repositoryPath);
        string? folderId = await ResolveFolderIdAsync(folderPath, create: false, cancellationToken).ConfigureAwait(false);
        if (folderId is null)
        {
            return false;
        }
        DriveData.File? file = (await ChildrenAsync(folderId, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(c => !IsFolder(c) && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (file is null)
        {
            return false;
        }
        await _drive.Files.Delete(file.Id).ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<string?> FindExistingIconAsync(string folderPath, CancellationToken ct)
    {
        string? folderId = await ResolveFolderIdAsync(folderPath, create: false, ct).ConfigureAwait(false);
        if (folderId is null)
        {
            return null;
        }
        List<DriveData.File> children = await ChildrenAsync(folderId, ct).ConfigureAwait(false);
        return children.FirstOrDefault(c => !IsFolder(c) &&
            c.Name.StartsWith("icon.", StringComparison.OrdinalIgnoreCase))?.Name;
    }

    private async Task UpsertFileAsync(string path, byte[] content, CancellationToken ct)
    {
        (string folderPath, string name) = SplitPath(path);
        string folderId = await ResolveFolderIdAsync(folderPath, create: true, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Could not resolve folder for '{path}'.");

        DriveData.File? existing = (await ChildrenAsync(folderId, ct).ConfigureAwait(false))
            .FirstOrDefault(c => !IsFolder(c) && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        using MemoryStream stream = new MemoryStream(content);
        if (existing is null)
        {
            DriveData.File metadata = new DriveData.File { Name = name, Parents = [folderId] };
            FilesResource.CreateMediaUpload create = _drive.Files.Create(metadata, stream, "application/octet-stream");
            await create.UploadAsync(ct).ConfigureAwait(false);
        }
        else
        {
            FilesResource.UpdateMediaUpload update = _drive.Files.Update(new DriveData.File(), existing.Id, stream, "application/octet-stream");
            await update.UploadAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task<string?> ReadTextAsync(string path, CancellationToken ct)
    {
        byte[]? bytes = await DownloadFileAsync(path, ct).ConfigureAwait(false);
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }

    private async Task<string?> ResolveFolderIdAsync(string relativePath, bool create, CancellationToken ct)
    {
        string? rootId = await GetRootFolderIdAsync(create, ct).ConfigureAwait(false);
        if (rootId is null)
        {
            return null;
        }
        if (_folderCache.TryGetValue(relativePath, out string? cached))
        {
            return cached;
        }

        string? parent = rootId;
        string accumulated = string.Empty;
        foreach (string segment in relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            accumulated = accumulated.Length == 0 ? segment : $"{accumulated}/{segment}";
            if (!_folderCache.TryGetValue(accumulated, out string? id))
            {
                id = await FindChildFolderIdAsync(parent, segment, ct).ConfigureAwait(false);
                if (id is null)
                {
                    if (!create)
                    {
                        return null;
                    }
                    id = await CreateFolderAsync(parent, segment, ct).ConfigureAwait(false);
                }
                _folderCache[accumulated] = id;
            }
            parent = id;
        }

        _folderCache[relativePath] = parent;
        return parent;
    }

    // Resolves the app's base folder: an explicit id, else a folder named RootFolderName that this
    // app created at the Drive root (found or created). With drive.file the app only sees its own files.
    private async Task<string?> GetRootFolderIdAsync(bool create, CancellationToken ct)
    {
        if (_rootId is not null)
        {
            return _rootId;
        }

        // Honor an explicit id only while it still resolves; a deleted or foreign folder (drive.file
        // cannot see folders this app did not create) would otherwise fail every request.
        if (!string.IsNullOrWhiteSpace(_settings.RootFolderId) &&
            await FolderExistsAsync(_settings.RootFolderId, ct).ConfigureAwait(false))
        {
            _rootId = _settings.RootFolderId;
            return _rootId;
        }

        string name = string.IsNullOrWhiteSpace(_settings.RootFolderName) ? GoogleDriveProviderSettings.DefaultRootFolderName : _settings.RootFolderName;
        FilesResource.ListRequest list = _drive.Files.List();
        list.Q = $"name = '{Escape(name)}' and mimeType = '{FolderMime}' and 'root' in parents and trashed = false";
        // Order by creation time and take the oldest so the same folder is always reused
        // (never creating a second one) even if a duplicate already exists.
        list.Fields = "files(id,name,createdTime)";
        list.OrderBy = "createdTime";
        list.PageSize = 10;
        DriveData.FileList response = await list.ExecuteAsync(ct).ConfigureAwait(false);
        _rootId = response.Files.FirstOrDefault()?.Id;
        if (_rootId is null && create)
        {
            _rootId = await CreateFolderAsync("root", name, ct).ConfigureAwait(false);
        }
        return _rootId;
    }

    // Whether a folder id still resolves for this app (false when deleted, trashed, or inaccessible).
    private async Task<bool> FolderExistsAsync(string folderId, CancellationToken ct)
    {
        try
        {
            FilesResource.GetRequest get = _drive.Files.Get(folderId);
            get.Fields = "id,trashed";
            DriveData.File file = await get.ExecuteAsync(ct).ConfigureAwait(false);
            return file.Trashed != true;
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    private async Task<string?> FindChildFolderIdAsync(string parentId, string name, CancellationToken ct)
    {
        List<DriveData.File> children = await ChildrenAsync(parentId, ct).ConfigureAwait(false);
        return children.FirstOrDefault(c => IsFolder(c) && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private async Task<string> CreateFolderAsync(string parentId, string name, CancellationToken ct)
    {
        DriveData.File metadata = new DriveData.File { Name = name, MimeType = FolderMime, Parents = [parentId] };
        FilesResource.CreateRequest create = _drive.Files.Create(metadata);
        create.Fields = "id";
        DriveData.File created = await create.ExecuteAsync(ct).ConfigureAwait(false);
        return created.Id;
    }

    private async Task<List<DriveData.File>> ChildrenAsync(string parentId, CancellationToken ct)
    {
        List<DriveData.File> result = new List<DriveData.File>();
        string? pageToken = null;
        do
        {
            FilesResource.ListRequest list = _drive.Files.List();
            list.Q = $"'{parentId}' in parents and trashed = false";
            list.Fields = "nextPageToken, files(id,name,mimeType)";
            list.PageSize = 1000;
            list.PageToken = pageToken;
            DriveData.FileList response = await list.ExecuteAsync(ct).ConfigureAwait(false);
            result.AddRange(response.Files);
            pageToken = response.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken));
        return result;
    }

    private static bool IsFolder(DriveData.File file)
    {
        return file.MimeType == FolderMime;
    }

    private static (string FolderPath, string Name) SplitPath(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash < 0 ? (string.Empty, path) : (path[..slash], path[(slash + 1)..]);
    }
}
