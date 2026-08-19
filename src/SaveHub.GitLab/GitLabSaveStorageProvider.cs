using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SaveHub.Core.Abstractions;
using SaveHub.Core.Archiving;
using SaveHub.Core.Models;

namespace SaveHub.GitLab;

/// <summary>
/// Stores saves in a GitLab project. Uploads are delivered as merge requests: when the authenticated
/// user can merge and auto-merge is enabled the MR is merged automatically, otherwise it is left open
/// for review. Users without write access contribute through a fork. The project ends up with the
/// same folder layout as the other providers because all artifacts are built by <c>SaveHub.Core</c>.
/// </summary>
public sealed class GitLabSaveStorageProvider : ISaveStorageProvider
{
    #region Fields & Properties

    private const int DeveloperAccess = 30;
    private const int MaintainerAccess = 40;

    private readonly GitLabProviderSettings _settings;
    private readonly HttpClient _http;
    private readonly string _projectId;
    private string? _resolvedBranch;

    public string Name => "gitlab";

    public StorageProviderCapabilities Capabilities { get; } = new()
    {
        SupportsPullRequests = true,
        SupportsAutoMerge = true,
        SupportsBrowsing = true,
    };

    private string ProjectPath => $"{_settings.Owner}/{_settings.Repository}";

    #endregion

    #region Constructors

    public GitLabSaveStorageProvider(GitLabProviderSettings settings, HttpClient? http = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        if (string.IsNullOrWhiteSpace(settings.Owner) || string.IsNullOrWhiteSpace(settings.Repository))
        {
            throw new ArgumentException("GitLab settings require both Owner and Repository.");
        }
        string token = settings.ResolveToken()
            ?? throw new InvalidOperationException(
                $"No GitLab token found. Set it in the config or the '{settings.TokenEnvironmentVariable}' env var.");

        _projectId = Uri.EscapeDataString(ProjectPath);
        _http = http ?? new HttpClient();
        string baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? "https://gitlab.com" : settings.BaseUrl;
        _http.BaseAddress ??= new Uri(baseUrl.TrimEnd('/') + "/api/v4/");
        _http.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");
        _http.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
    }

    #endregion

    #region Public Methods

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        UserDto? me;
        try
        {
            me = await GetAsync<UserDto>("user", cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Target = ProjectPath,
                Message = "Authentication failed: the token is missing, invalid, or expired.",
            };
        }

        ProjectDto? project = await GetAsync<ProjectDto>($"projects/{_projectId}", cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return new ConnectionTestResult
            {
                Success = false,
                AuthenticatedAs = me?.Username,
                Target = ProjectPath,
                Message = $"Project '{ProjectPath}' was not found, or your token cannot see it.",
            };
        }

        int access = AccessLevel(project);
        bool canWrite = access >= DeveloperAccess;
        bool canMerge = access >= MaintainerAccess;
        return new ConnectionTestResult
        {
            Success = true,
            AuthenticatedAs = me?.Username,
            Target = project.PathWithNamespace ?? ProjectPath,
            HasWriteAccess = canWrite,
            AutoMergeEffective = canMerge && _settings.AutoMerge,
            Message = canWrite
                ? "Connected. You can push; uploads open a merge request and can auto-merge when enabled."
                : "Connected. No write access; uploads go via a fork and merge request for the owner to review.",
        };
    }

    public async Task<IReadOnlyList<string>> ListPlatformsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TreeEntry> entries = await ListTreeAsync(_projectId, string.Empty, cancellationToken).ConfigureAwait(false);
        return entries
            .Where(e => e.Type == "tree" && !e.Name.StartsWith('.'))
            .Select(e => e.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> ListGamesAsync(string platform, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TreeEntry> entries = await ListTreeAsync(_projectId, SaveNaming.Sanitize(platform), cancellationToken).ConfigureAwait(false);
        return entries
            .Where(e => e.Type == "tree")
            .Select(e => e.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<SaveEntry>> ListSavesAsync(string platform, string gameId, CancellationToken cancellationToken = default)
    {
        string folder = SaveNaming.GameFolder(platform, gameId);
        IReadOnlyList<TreeEntry> entries = await ListTreeAsync(_projectId, folder, cancellationToken).ConfigureAwait(false);
        string? readme = await ReadTextAsync(_projectId, $"{folder}/{GameReadmeFormatter.FileName}", await GetBranchAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, string> descriptions = GameReadmeFormatter.ParseDescriptions(readme);

        List<SaveEntry> result = new List<SaveEntry>();
        foreach (TreeEntry entry in entries)
        {
            if (entry.Type != "blob" || !SaveNaming.TryParseArchiveName(entry.Name, out int index, out SaveType type))
            {
                continue;
            }
            descriptions.TryGetValue(entry.Name, out string? description);
            result.Add(new SaveEntry
            {
                Platform = SaveNaming.Sanitize(platform),
                GameId = SaveNaming.Sanitize(gameId),
                SaveType = type,
                Index = index,
                ArchiveName = entry.Name,
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

    public async Task<byte[]?> DownloadFileAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        string branch = await GetBranchAsync(cancellationToken).ConfigureAwait(false);
        return await ReadRawAsync(_projectId, repositoryPath, branch, cancellationToken).ConfigureAwait(false);
    }

    public async Task UploadFileAsync(string repositoryPath, byte[] content, CancellationToken cancellationToken = default)
    {
        string branch = await GetBranchAsync(cancellationToken).ConfigureAwait(false);
        bool exists = await FileExistsAsync(_projectId, repositoryPath, branch, cancellationToken).ConfigureAwait(false);
        CommitAction action = new CommitAction
        {
            Action = exists ? "update" : "create",
            FilePath = repositoryPath,
            Content = Convert.ToBase64String(content),
            Encoding = "base64",
        };
        await CommitAsync(_projectId, branch, null, $"Update {repositoryPath}", [action], cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteFileAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        string branch = await GetBranchAsync(cancellationToken).ConfigureAwait(false);
        if (!await FileExistsAsync(_projectId, repositoryPath, branch, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }
        CommitAction action = new CommitAction { Action = "delete", FilePath = repositoryPath };
        return await CommitAsync(_projectId, branch, null, $"Delete {repositoryPath}", [action], cancellationToken).ConfigureAwait(false);
    }

    public async Task<SaveUploadResult> UploadAsync(PreparedSave save, UploadOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(options);

        string baseBranch = await GetBranchAsync(cancellationToken).ConfigureAwait(false);
        ProjectDto project = await GetAsync<ProjectDto>($"projects/{_projectId}", cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"GitLab project '{ProjectPath}' was not found.");
        int access = AccessLevel(project);
        bool canWrite = access >= DeveloperAccess;
        bool canMerge = access >= MaintainerAccess;

        string headId = _projectId;
        if (!canWrite)
        {
            ProjectDto fork = await EnsureForkAsync(cancellationToken).ConfigureAwait(false);
            headId = fork.Id.ToString();
        }

        // Build the file set (archive, optional icon, per-game README, platform README).
        List<StorageFile> files = new List<StorageFile> { save.Archive };
        string? existingIcon = await FindExistingIconAsync(headId, save.GameFolder, baseBranch, cancellationToken).ConfigureAwait(false);
        string? iconFileName = existingIcon;
        if (save.Icon is { } icon && (save.IconIsExplicit || existingIcon is null))
        {
            files.Add(icon);
            iconFileName = icon.Path[(icon.Path.LastIndexOf('/') + 1)..];
        }

        string gameReadmePath = $"{save.GameFolder}/{GameReadmeFormatter.FileName}";
        string gameReadme = GameReadmeFormatter.Upsert(
            await ReadTextAsync(headId, gameReadmePath, baseBranch, cancellationToken).ConfigureAwait(false),
            save.Platform, save.GameId, save.GameTitle, save.Index, save.SaveType, save.Description, iconFileName);
        files.Add(new StorageFile(gameReadmePath, Encoding.UTF8.GetBytes(gameReadme)));

        string platformReadmePath = $"{SaveNaming.Sanitize(save.Platform)}/{PlatformReadmeFormatter.FileName}";
        string platformReadme = PlatformReadmeFormatter.Upsert(
            await ReadTextAsync(headId, platformReadmePath, baseBranch, cancellationToken).ConfigureAwait(false),
            save.Platform, save.GameId, save.GameTitle);
        files.Add(new StorageFile(platformReadmePath, Encoding.UTF8.GetBytes(platformReadme)));

        string workBranch = BuildBranchName(save);
        List<CommitAction> actions = new List<CommitAction>();
        foreach (StorageFile file in files)
        {
            bool exists = await FileExistsAsync(headId, file.Path, baseBranch, cancellationToken).ConfigureAwait(false);
            actions.Add(new CommitAction
            {
                Action = exists ? "update" : "create",
                FilePath = file.Path,
                Content = Convert.ToBase64String(file.Content),
                Encoding = "base64",
            });
        }
        await CommitAsync(headId, workBranch, baseBranch, DefaultTitle(save), actions, cancellationToken).ConfigureAwait(false);

        string title = options.Title ?? DefaultTitle(save);
        MergeRequestDto mr = await CreateMergeRequestAsync(
            headId, workBranch, baseBranch, title, BuildDescription(save), project.Id, cancellationToken).ConfigureAwait(false);

        bool wantMerge = (options.AutoMerge ?? _settings.AutoMerge) && _settings.AutoMerge;
        if (wantMerge && canMerge)
        {
            if (await TryMergeAsync(project.Id, mr.Iid, cancellationToken).ConfigureAwait(false))
            {
                return new SaveUploadResult
                {
                    Success = true,
                    Merged = true,
                    Branch = workBranch,
                    PullRequestUrl = mr.WebUrl,
                    ArchivePath = save.Archive.Path,
                    Message = $"Uploaded and auto-merged: {save.Archive.Path}",
                };
            }
        }

        string reason = wantMerge && !canMerge
            ? " Auto-merge was skipped because you cannot merge in this project; the owner must review and merge."
            : string.Empty;
        return new SaveUploadResult
        {
            Success = true,
            Merged = false,
            Branch = workBranch,
            PullRequestUrl = mr.WebUrl,
            ArchivePath = save.Archive.Path,
            Message = $"Merge request opened for review: {mr.WebUrl}.{reason}",
        };
    }

    #endregion

    #region Private Methods

    private async Task<string> GetBranchAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_settings.Branch))
        {
            return _settings.Branch;
        }
        if (_resolvedBranch is not null)
        {
            return _resolvedBranch;
        }
        ProjectDto? project = await GetAsync<ProjectDto>($"projects/{_projectId}", ct).ConfigureAwait(false);
        _resolvedBranch = string.IsNullOrWhiteSpace(project?.DefaultBranch) ? "main" : project!.DefaultBranch!;
        return _resolvedBranch;
    }

    private static int AccessLevel(ProjectDto project)
    {
        int project_ = project.Permissions?.ProjectAccess?.AccessLevel ?? 0;
        int group = project.Permissions?.GroupAccess?.AccessLevel ?? 0;
        return Math.Max(project_, group);
    }

    private async Task<ProjectDto> EnsureForkAsync(CancellationToken ct)
    {
        UserDto? me = await GetAsync<UserDto>("user", ct).ConfigureAwait(false);
        string forkPath = Uri.EscapeDataString($"{me?.Username}/{_settings.Repository}");

        using HttpResponseMessage response = await _http
            .PostAsync($"projects/{_projectId}/fork", null, ct).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Conflict)
        {
            response.EnsureSuccessStatusCode();
        }

        // Forks import asynchronously; wait until the fork project resolves.
        for (int attempt = 0; attempt < 15; attempt++)
        {
            ProjectDto? fork = await GetAsync<ProjectDto>($"projects/{forkPath}", ct).ConfigureAwait(false);
            if (fork is not null)
            {
                return fork;
            }
            await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        throw new InvalidOperationException("Timed out waiting for the GitLab fork to become available.");
    }

    private async Task<string?> FindExistingIconAsync(string projectId, string folder, string branch, CancellationToken ct)
    {
        IReadOnlyList<TreeEntry> entries = await ListTreeAsync(projectId, folder, ct).ConfigureAwait(false);
        return entries
            .FirstOrDefault(e => e.Type == "blob" && e.Name.StartsWith("icon.", StringComparison.OrdinalIgnoreCase))?
            .Name;
    }

    private async Task<IReadOnlyList<TreeEntry>> ListTreeAsync(string projectId, string path, CancellationToken ct)
    {
        string branch = await GetBranchAsync(ct).ConfigureAwait(false);
        string query = $"projects/{projectId}/repository/tree?per_page=100&pagination=none&ref={Uri.EscapeDataString(branch)}";
        if (!string.IsNullOrEmpty(path))
        {
            query += $"&path={Uri.EscapeDataString(path)}";
        }
        try
        {
            List<TreeEntry>? entries = await GetAsync<List<TreeEntry>>(query, ct).ConfigureAwait(false);
            return entries ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    private async Task<bool> FileExistsAsync(string projectId, string path, string branch, CancellationToken ct)
    {
        string url = $"projects/{projectId}/repository/files/{Uri.EscapeDataString(path)}?ref={Uri.EscapeDataString(branch)}";
        using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, url);
        using HttpResponseMessage response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    private async Task<byte[]?> ReadRawAsync(string projectId, string path, string branch, CancellationToken ct)
    {
        string url = $"projects/{projectId}/repository/files/{Uri.EscapeDataString(path)}/raw?ref={Uri.EscapeDataString(branch)}";
        using HttpResponseMessage response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    private async Task<string?> ReadTextAsync(string projectId, string path, string branch, CancellationToken ct)
    {
        byte[]? bytes = await ReadRawAsync(projectId, path, branch, ct).ConfigureAwait(false);
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }

    private async Task<bool> CommitAsync(
        string projectId, string branch, string? startBranch, string message, IReadOnlyList<CommitAction> actions, CancellationToken ct)
    {
        CommitRequest payload = new CommitRequest
        {
            Branch = branch,
            StartBranch = startBranch,
            CommitMessage = message,
            AuthorName = _settings.CommitterName,
            AuthorEmail = _settings.CommitterEmail,
            Actions = actions,
        };
        using HttpResponseMessage response = await _http
            .PostAsJsonAsync($"projects/{projectId}/repository/commits", payload, JsonWebOptions, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return true;
    }

    private async Task<MergeRequestDto> CreateMergeRequestAsync(
        string sourceProjectId, string sourceBranch, string targetBranch, string title, string description, long targetProjectId, CancellationToken ct)
    {
        MergeRequestRequest payload = new MergeRequestRequest
        {
            SourceBranch = sourceBranch,
            TargetBranch = targetBranch,
            Title = title,
            Description = description,
            TargetProjectId = targetProjectId,
        };
        using HttpResponseMessage response = await _http
            .PostAsJsonAsync($"projects/{sourceProjectId}/merge_requests", payload, JsonWebOptions, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        MergeRequestDto? mr = await response.Content.ReadFromJsonAsync<MergeRequestDto>(JsonWebOptions, ct).ConfigureAwait(false);
        return mr ?? throw new InvalidOperationException("GitLab did not return the created merge request.");
    }

    private async Task<bool> TryMergeAsync(long targetProjectId, long mergeRequestIid, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await _http
                .PutAsync($"projects/{targetProjectId}/merge_requests/{mergeRequestIid}/merge", null, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
    {
        using HttpResponseMessage response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonWebOptions, ct).ConfigureAwait(false);
    }

    private static string BuildBranchName(PreparedSave save)
    {
        string stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        string baseName = SaveNaming.BaseName(save.Index, save.SaveType);
        return $"savehub/{save.Platform}-{save.GameId}-{baseName}-{stamp}".ToLowerInvariant();
    }

    private static string DefaultTitle(PreparedSave save)
    {
        string kind = SaveNaming.Label(save.SaveType).ToLowerInvariant();
        return $"Add {save.Platform}/{save.GameId} {kind} ({SaveNaming.ArchiveName(save.Index, save.SaveType)})";
    }

    private static string BuildDescription(PreparedSave save)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"**Platform:** {save.Platform}");
        builder.AppendLine($"**Game:** {save.GameId}");
        builder.AppendLine($"**Type:** {SaveNaming.Label(save.SaveType)}");
        builder.AppendLine($"**Archive:** `{save.Archive.Path}`");
        if (!string.IsNullOrWhiteSpace(save.Description))
        {
            builder.AppendLine();
            builder.AppendLine(save.Description);
        }
        return builder.ToString();
    }

    #endregion

    #region Nested Types

    private static readonly JsonSerializerOptions JsonWebOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class UserDto
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("username")] public string? Username { get; set; }
    }

    private sealed class ProjectDto
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("default_branch")] public string? DefaultBranch { get; set; }
        [JsonPropertyName("path_with_namespace")] public string? PathWithNamespace { get; set; }
        [JsonPropertyName("web_url")] public string? WebUrl { get; set; }
        [JsonPropertyName("permissions")] public PermissionsDto? Permissions { get; set; }
    }

    private sealed class PermissionsDto
    {
        [JsonPropertyName("project_access")] public AccessDto? ProjectAccess { get; set; }
        [JsonPropertyName("group_access")] public AccessDto? GroupAccess { get; set; }
    }

    private sealed class AccessDto
    {
        [JsonPropertyName("access_level")] public int AccessLevel { get; set; }
    }

    private sealed class TreeEntry
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
    }

    private sealed class CommitAction
    {
        [JsonPropertyName("action")] public string Action { get; set; } = string.Empty;
        [JsonPropertyName("file_path")] public string FilePath { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string? Content { get; set; }
        [JsonPropertyName("encoding")] public string? Encoding { get; set; }
    }

    private sealed class CommitRequest
    {
        [JsonPropertyName("branch")] public string Branch { get; set; } = string.Empty;
        [JsonPropertyName("start_branch")] public string? StartBranch { get; set; }
        [JsonPropertyName("commit_message")] public string CommitMessage { get; set; } = string.Empty;
        [JsonPropertyName("author_name")] public string? AuthorName { get; set; }
        [JsonPropertyName("author_email")] public string? AuthorEmail { get; set; }
        [JsonPropertyName("actions")] public IReadOnlyList<CommitAction> Actions { get; set; } = [];
    }

    private sealed class MergeRequestRequest
    {
        [JsonPropertyName("source_branch")] public string SourceBranch { get; set; } = string.Empty;
        [JsonPropertyName("target_branch")] public string TargetBranch { get; set; } = string.Empty;
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("target_project_id")] public long TargetProjectId { get; set; }
    }

    private sealed class MergeRequestDto
    {
        [JsonPropertyName("iid")] public long Iid { get; set; }
        [JsonPropertyName("web_url")] public string? WebUrl { get; set; }
        [JsonPropertyName("project_id")] public long ProjectId { get; set; }
    }

    #endregion
}
