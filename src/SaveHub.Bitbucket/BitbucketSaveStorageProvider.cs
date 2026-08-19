using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SaveHub.Core.Abstractions;
using SaveHub.Core.Archiving;
using SaveHub.Core.Models;

namespace SaveHub.Bitbucket;

/// <summary>
/// Stores saves in a Bitbucket repository. Uploads are delivered as pull requests: when the
/// authenticated user has write access and auto-merge is enabled the PR is merged automatically,
/// otherwise it is left open for review. Users without write access contribute through a fork. The
/// repository ends up with the same folder layout as the other providers because all artifacts are
/// built by <c>SaveHub.Core</c>.
/// </summary>
public sealed class BitbucketSaveStorageProvider : ISaveStorageProvider
{
    #region Fields & Properties

    private const string ApiBase = "https://api.bitbucket.org/2.0/";

    private readonly BitbucketProviderSettings _settings;
    private readonly HttpClient _http;
    private string? _resolvedBranch;

    public string Name => "bitbucket";

    public StorageProviderCapabilities Capabilities { get; } = new()
    {
        SupportsPullRequests = true,
        SupportsAutoMerge = true,
        SupportsBrowsing = true,
    };

    private string Workspace => _settings.Workspace;
    private string Repo => _settings.Repository;
    private string FullName => $"{Workspace}/{Repo}";

    #endregion

    #region Constructors

    public BitbucketSaveStorageProvider(BitbucketProviderSettings settings, HttpClient? http = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        if (string.IsNullOrWhiteSpace(settings.Workspace) || string.IsNullOrWhiteSpace(settings.Repository))
        {
            throw new ArgumentException("Bitbucket settings require both Workspace and Repository.");
        }
        if (string.IsNullOrWhiteSpace(settings.Username))
        {
            throw new ArgumentException("Bitbucket settings require a Username.");
        }
        string appPassword = settings.ResolveAppPassword()
            ?? throw new InvalidOperationException(
                $"No Bitbucket app password found. Set it in the config or the '{settings.AppPasswordEnvironmentVariable}' env var.");

        _http = http ?? new HttpClient();
        _http.BaseAddress ??= new Uri(ApiBase);
        string basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{appPassword}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
    }

    #endregion

    #region Public Methods

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        RepositoryDto? repo;
        try
        {
            repo = await GetAsync<RepositoryDto>($"repositories/{FullName}", cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Target = FullName,
                Message = "Authentication failed: the username or app password is missing, invalid, or expired.",
            };
        }
        if (repo is null)
        {
            return new ConnectionTestResult
            {
                Success = false,
                AuthenticatedAs = _settings.Username,
                Target = FullName,
                Message = $"Repository '{FullName}' was not found, or your app password cannot see it.",
            };
        }

        bool canWrite = await HasWriteAccessAsync(cancellationToken).ConfigureAwait(false);
        return new ConnectionTestResult
        {
            Success = true,
            AuthenticatedAs = _settings.Username,
            Target = repo.FullName ?? FullName,
            HasWriteAccess = canWrite,
            AutoMergeEffective = canWrite && _settings.AutoMerge,
            Message = canWrite
                ? "Connected. You have write access; uploads open a PR and can auto-merge when enabled."
                : "Connected. No write access; uploads go via a fork and PR for the owner to review.",
        };
    }

    public async Task<IReadOnlyList<string>> ListPlatformsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SrcEntry> entries = await ListSrcAsync(FullName, string.Empty, cancellationToken).ConfigureAwait(false);
        return entries
            .Where(e => e.Type == "commit_directory" && !LastSegment(e.Path).StartsWith('.'))
            .Select(e => LastSegment(e.Path))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> ListGamesAsync(string platform, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SrcEntry> entries = await ListSrcAsync(FullName, SaveNaming.Sanitize(platform), cancellationToken).ConfigureAwait(false);
        return entries
            .Where(e => e.Type == "commit_directory")
            .Select(e => LastSegment(e.Path))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<SaveEntry>> ListSavesAsync(string platform, string gameId, CancellationToken cancellationToken = default)
    {
        string folder = SaveNaming.GameFolder(platform, gameId);
        IReadOnlyList<SrcEntry> entries = await ListSrcAsync(FullName, folder, cancellationToken).ConfigureAwait(false);
        string branch = await GetBranchAsync(cancellationToken).ConfigureAwait(false);
        string? readme = await ReadTextAsync(FullName, $"{folder}/{GameReadmeFormatter.FileName}", branch, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, string> descriptions = GameReadmeFormatter.ParseDescriptions(readme);

        List<SaveEntry> result = new List<SaveEntry>();
        foreach (SrcEntry entry in entries)
        {
            string name = LastSegment(entry.Path);
            if (entry.Type != "commit_file" || !SaveNaming.TryParseArchiveName(name, out int index, out SaveType type))
            {
                continue;
            }
            descriptions.TryGetValue(name, out string? description);
            result.Add(new SaveEntry
            {
                Platform = SaveNaming.Sanitize(platform),
                GameId = SaveNaming.Sanitize(gameId),
                SaveType = type,
                Index = index,
                ArchiveName = name,
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
        return await ReadRawAsync(FullName, repositoryPath, branch, cancellationToken).ConfigureAwait(false);
    }

    public async Task UploadFileAsync(string repositoryPath, byte[] content, CancellationToken cancellationToken = default)
    {
        string branch = await GetBranchAsync(cancellationToken).ConfigureAwait(false);
        await CommitFilesAsync(FullName, branch, $"Update {repositoryPath}",
            [new StorageFile(repositoryPath, content)], [], cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteFileAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        string branch = await GetBranchAsync(cancellationToken).ConfigureAwait(false);
        if (await ReadRawAsync(FullName, repositoryPath, branch, cancellationToken).ConfigureAwait(false) is null)
        {
            return false;
        }
        await CommitFilesAsync(FullName, branch, $"Delete {repositoryPath}", [], [repositoryPath], cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<SaveUploadResult> UploadAsync(PreparedSave save, UploadOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(options);

        string baseBranch = await GetBranchAsync(cancellationToken).ConfigureAwait(false);
        bool canWrite = await HasWriteAccessAsync(cancellationToken).ConfigureAwait(false);

        string headFullName = FullName;
        if (!canWrite)
        {
            headFullName = await EnsureForkAsync(cancellationToken).ConfigureAwait(false);
        }

        // Build the file set (archive, optional icon, per-game README, platform README).
        List<StorageFile> files = new List<StorageFile> { save.Archive };
        string? existingIcon = await FindExistingIconAsync(headFullName, save.GameFolder, baseBranch, cancellationToken).ConfigureAwait(false);
        string? iconFileName = existingIcon;
        if (save.Icon is { } icon && (save.IconIsExplicit || existingIcon is null))
        {
            files.Add(icon);
            iconFileName = icon.Path[(icon.Path.LastIndexOf('/') + 1)..];
        }

        string gameReadmePath = $"{save.GameFolder}/{GameReadmeFormatter.FileName}";
        string gameReadme = GameReadmeFormatter.Upsert(
            await ReadTextAsync(headFullName, gameReadmePath, baseBranch, cancellationToken).ConfigureAwait(false),
            save.Platform, save.GameId, save.GameTitle, save.Index, save.SaveType, save.Description, iconFileName);
        files.Add(new StorageFile(gameReadmePath, Encoding.UTF8.GetBytes(gameReadme)));

        string platformReadmePath = $"{SaveNaming.Sanitize(save.Platform)}/{PlatformReadmeFormatter.FileName}";
        string platformReadme = PlatformReadmeFormatter.Upsert(
            await ReadTextAsync(headFullName, platformReadmePath, baseBranch, cancellationToken).ConfigureAwait(false),
            save.Platform, save.GameId, save.GameTitle);
        files.Add(new StorageFile(platformReadmePath, Encoding.UTF8.GetBytes(platformReadme)));

        string workBranch = BuildBranchName(save);
        string baseHash = await GetBranchHeadAsync(headFullName, baseBranch, cancellationToken).ConfigureAwait(false);
        await CreateBranchAsync(headFullName, workBranch, baseHash, cancellationToken).ConfigureAwait(false);
        await CommitFilesAsync(headFullName, workBranch, DefaultTitle(save), files, [], cancellationToken).ConfigureAwait(false);

        string title = options.Title ?? DefaultTitle(save);
        PullRequestDto pr = await CreatePullRequestAsync(
            workBranch, headFullName, baseBranch, title, BuildDescription(save), cancellationToken).ConfigureAwait(false);

        bool wantMerge = (options.AutoMerge ?? _settings.AutoMerge) && _settings.AutoMerge;
        if (wantMerge && canWrite)
        {
            if (await TryMergeAsync(pr.Id, cancellationToken).ConfigureAwait(false))
            {
                return new SaveUploadResult
                {
                    Success = true,
                    Merged = true,
                    Branch = workBranch,
                    PullRequestUrl = pr.HtmlUrl,
                    ArchivePath = save.Archive.Path,
                    Message = $"Uploaded and auto-merged: {save.Archive.Path}",
                };
            }
        }

        string reason = wantMerge && !canWrite
            ? " Auto-merge was skipped because you are not the owner or a contributor; the owner must review and merge."
            : string.Empty;
        return new SaveUploadResult
        {
            Success = true,
            Merged = false,
            Branch = workBranch,
            PullRequestUrl = pr.HtmlUrl,
            ArchivePath = save.Archive.Path,
            Message = $"Pull request opened for review: {pr.HtmlUrl}.{reason}",
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
        RepositoryDto? repo = await GetAsync<RepositoryDto>($"repositories/{FullName}", ct).ConfigureAwait(false);
        _resolvedBranch = string.IsNullOrWhiteSpace(repo?.MainBranch?.Name) ? "main" : repo!.MainBranch!.Name!;
        return _resolvedBranch;
    }

    private async Task<bool> HasWriteAccessAsync(CancellationToken ct)
    {
        string query = "user/permissions/repositories?q=" + Uri.EscapeDataString($"repository.full_name=\"{FullName}\"");
        PermissionPage? page = await GetAsync<PermissionPage>(query, ct).ConfigureAwait(false);
        string? permission = page?.Values?.FirstOrDefault()?.Permission;
        return permission is "write" or "admin";
    }

    private async Task<string> EnsureForkAsync(CancellationToken ct)
    {
        string forkFullName = $"{_settings.Username}/{Repo}";

        RepositoryDto? existing = await GetAsync<RepositoryDto>($"repositories/{forkFullName}", ct).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.FullName ?? forkFullName;
        }

        ForkRequest payload = new ForkRequest { Name = Repo };
        using HttpResponseMessage response = await _http
            .PostAsJsonAsync($"repositories/{FullName}/forks", payload, JsonWebOptions, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        for (int attempt = 0; attempt < 15; attempt++)
        {
            RepositoryDto? fork = await GetAsync<RepositoryDto>($"repositories/{forkFullName}", ct).ConfigureAwait(false);
            if (fork is not null)
            {
                return fork.FullName ?? forkFullName;
            }
            await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        }
        throw new InvalidOperationException("Timed out waiting for the Bitbucket fork to become available.");
    }

    private async Task<string?> FindExistingIconAsync(string repoFullName, string folder, string branch, CancellationToken ct)
    {
        IReadOnlyList<SrcEntry> entries = await ListSrcAsync(repoFullName, folder, ct).ConfigureAwait(false);
        return entries
            .Where(e => e.Type == "commit_file")
            .Select(e => LastSegment(e.Path))
            .FirstOrDefault(n => n.StartsWith("icon.", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<SrcEntry>> ListSrcAsync(string repoFullName, string path, CancellationToken ct)
    {
        string branch = await GetBranchAsync(ct).ConfigureAwait(false);
        string url = $"repositories/{repoFullName}/src/{Uri.EscapeDataString(branch)}/{path}".TrimEnd('/') + "/?pagelen=100";
        List<SrcEntry> all = new List<SrcEntry>();
        string? next = url;
        while (next is not null)
        {
            SrcPage? page;
            try
            {
                page = await GetAsync<SrcPage>(next, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                return all;
            }
            if (page?.Values is null)
            {
                break;
            }
            all.AddRange(page.Values);
            next = page.Next;
        }
        return all;
    }

    private async Task<byte[]?> ReadRawAsync(string repoFullName, string path, string branch, CancellationToken ct)
    {
        string url = $"repositories/{repoFullName}/src/{Uri.EscapeDataString(branch)}/{path}";
        using HttpResponseMessage response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    private async Task<string?> ReadTextAsync(string repoFullName, string path, string branch, CancellationToken ct)
    {
        byte[]? bytes = await ReadRawAsync(repoFullName, path, branch, ct).ConfigureAwait(false);
        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }

    private async Task<string> GetBranchHeadAsync(string repoFullName, string branch, CancellationToken ct)
    {
        BranchDto? dto = await GetAsync<BranchDto>($"repositories/{repoFullName}/refs/branches/{Uri.EscapeDataString(branch)}", ct).ConfigureAwait(false);
        return dto?.Target?.Hash
            ?? throw new InvalidOperationException($"Could not resolve the head commit of branch '{branch}'.");
    }

    private async Task CreateBranchAsync(string repoFullName, string name, string targetHash, CancellationToken ct)
    {
        CreateBranchRequest payload = new CreateBranchRequest { Name = name, Target = new BranchTarget { Hash = targetHash } };
        using HttpResponseMessage response = await _http
            .PostAsJsonAsync($"repositories/{repoFullName}/refs/branches", payload, JsonWebOptions, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task CommitFilesAsync(
        string repoFullName, string branch, string message, IReadOnlyList<StorageFile> files, IReadOnlyList<string> deletions, CancellationToken ct)
    {
        using MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new StringContent(message), "message");
        form.Add(new StringContent(branch), "branch");
        if (!string.IsNullOrWhiteSpace(_settings.CommitterName) && !string.IsNullOrWhiteSpace(_settings.CommitterEmail))
        {
            form.Add(new StringContent($"{_settings.CommitterName} <{_settings.CommitterEmail}>"), "author");
        }
        foreach (string path in deletions)
        {
            form.Add(new StringContent(path), "files");
        }
        foreach (StorageFile file in files)
        {
            ByteArrayContent content = new ByteArrayContent(file.Content);
            form.Add(content, file.Path, LastSegment(file.Path));
        }
        using HttpResponseMessage response = await _http
            .PostAsync($"repositories/{repoFullName}/src", form, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<PullRequestDto> CreatePullRequestAsync(
        string sourceBranch, string sourceRepoFullName, string destinationBranch, string title, string description, CancellationToken ct)
    {
        PullRequestRequest payload = new PullRequestRequest
        {
            Title = title,
            Description = description,
            Source = new PullRequestEndpoint
            {
                Branch = new BranchRef { Name = sourceBranch },
                Repository = new RepositoryRef { FullName = sourceRepoFullName },
            },
            Destination = new PullRequestEndpoint { Branch = new BranchRef { Name = destinationBranch } },
        };
        using HttpResponseMessage response = await _http
            .PostAsJsonAsync($"repositories/{FullName}/pullrequests", payload, JsonWebOptions, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        PullRequestDto? pr = await response.Content.ReadFromJsonAsync<PullRequestDto>(JsonWebOptions, ct).ConfigureAwait(false);
        return pr ?? throw new InvalidOperationException("Bitbucket did not return the created pull request.");
    }

    private async Task<bool> TryMergeAsync(long pullRequestId, CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await _http
                .PostAsync($"repositories/{FullName}/pullrequests/{pullRequestId}/merge", null, ct).ConfigureAwait(false);
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

    private static string LastSegment(string path)
    {
        string trimmed = path.TrimEnd('/');
        int slash = trimmed.LastIndexOf('/');
        return slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
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

    private sealed class RepositoryDto
    {
        [JsonPropertyName("full_name")] public string? FullName { get; set; }
        [JsonPropertyName("mainbranch")] public MainBranchDto? MainBranch { get; set; }
    }

    private sealed class MainBranchDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    private sealed class BranchDto
    {
        [JsonPropertyName("target")] public BranchTarget? Target { get; set; }
    }

    private sealed class BranchTarget
    {
        [JsonPropertyName("hash")] public string? Hash { get; set; }
    }

    private sealed class SrcPage
    {
        [JsonPropertyName("values")] public List<SrcEntry>? Values { get; set; }
        [JsonPropertyName("next")] public string? Next { get; set; }
    }

    private sealed class SrcEntry
    {
        [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    }

    private sealed class PermissionPage
    {
        [JsonPropertyName("values")] public List<PermissionEntry>? Values { get; set; }
    }

    private sealed class PermissionEntry
    {
        [JsonPropertyName("permission")] public string? Permission { get; set; }
    }

    private sealed class ForkRequest
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }

    private sealed class CreateBranchRequest
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("target")] public BranchTarget Target { get; set; } = new();
    }

    private sealed class PullRequestRequest
    {
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("source")] public PullRequestEndpoint Source { get; set; } = new();
        [JsonPropertyName("destination")] public PullRequestEndpoint Destination { get; set; } = new();
    }

    private sealed class PullRequestEndpoint
    {
        [JsonPropertyName("branch")] public BranchRef Branch { get; set; } = new();
        [JsonPropertyName("repository")] public RepositoryRef? Repository { get; set; }
    }

    private sealed class BranchRef
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }

    private sealed class RepositoryRef
    {
        [JsonPropertyName("full_name")] public string FullName { get; set; } = string.Empty;
    }

    private sealed class PullRequestDto
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("links")] public LinksDto? Links { get; set; }

        public string? HtmlUrl => Links?.Html?.Href;
    }

    private sealed class LinksDto
    {
        [JsonPropertyName("html")] public LinkDto? Html { get; set; }
    }

    private sealed class LinkDto
    {
        [JsonPropertyName("href")] public string? Href { get; set; }
    }

    #endregion
}
