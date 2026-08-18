using System.Text;
using Octokit;
using SaveHub.Core.Abstractions;
using SaveHub.Core.Archiving;
using SaveHub.Core.Models;

namespace SaveHub.GitHub;

/// <summary>
/// Stores saves in a public GitHub repository. Uploads are delivered as pull requests. When the
/// authenticated user has write access (owner or contributor) and auto-merge is enabled, the pull
/// request is merged automatically; otherwise it is left open for review. Users without write access
/// contribute through a fork.
/// </summary>
public sealed class GitHubSaveStorageProvider : ISaveStorageProvider
{
    private readonly GitHubProviderSettings _settings;
    private readonly IGitHubClient _client;
    private string? _resolvedBranch;

    public string Name => "github";

    public StorageProviderCapabilities Capabilities { get; } = new()
    {
        SupportsPullRequests = true,
        SupportsAutoMerge = true,
        SupportsBrowsing = true,
    };

    private string Owner => _settings.Owner;
    private string Repo => _settings.Repository;

    public GitHubSaveStorageProvider(GitHubProviderSettings settings, IGitHubClient? client = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        if (string.IsNullOrWhiteSpace(settings.Owner) || string.IsNullOrWhiteSpace(settings.Repository))
        {
            throw new ArgumentException("GitHub settings require both Owner and Repository.");
        }

        if (client is not null)
        {
            _client = client;
        }
        else
        {
            string token = settings.ResolveToken()
                ?? throw new InvalidOperationException(
                    "No GitHub token found. Set it in the config or in the environment variable " +
                    $"'{settings.TokenEnvironmentVariable}'.");
            _client = new GitHubClient(new ProductHeaderValue("SaveHub"))
            {
                Credentials = new Credentials(token),
            };
        }
    }

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
        Repository repo = await _client.Repository.Get(Owner, Repo).ConfigureAwait(false);
        _resolvedBranch = repo.DefaultBranch;
        return _resolvedBranch;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        string? login = null;
        try
        {
            User user = await _client.User.Current().ConfigureAwait(false);
            login = user.Login;
        }
        catch (AuthorizationException)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Target = $"{Owner}/{Repo}",
                Message = "Authentication failed: the token is missing, invalid, or expired.",
            };
        }

        try
        {
            Repository repo = await _client.Repository.Get(Owner, Repo).ConfigureAwait(false);
            bool canWrite = HasWriteAccess(repo);
            return new ConnectionTestResult
            {
                Success = true,
                AuthenticatedAs = login,
                Target = repo.FullName,
                HasWriteAccess = canWrite,
                AutoMergeEffective = canWrite && _settings.AutoMerge,
                Message = canWrite
                    ? "Connected. You have write access; uploads open a PR and can auto-merge when enabled."
                    : "Connected. No write access; uploads go via a fork and PR for the owner to review.",
            };
        }
        catch (NotFoundException)
        {
            return new ConnectionTestResult
            {
                Success = false,
                AuthenticatedAs = login,
                Target = $"{Owner}/{Repo}",
                Message = $"Repository '{Owner}/{Repo}' was not found, or your token cannot see it.",
            };
        }
    }

    public async Task<IReadOnlyList<string>> ListPlatformsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RepositoryContent> contents = await TryGetContentsAsync(string.Empty, cancellationToken).ConfigureAwait(false);
        return contents
            .Where(c => c.Type == ContentType.Dir && !c.Name.StartsWith('.'))
            .Select(c => c.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> ListGamesAsync(string platform, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RepositoryContent> contents = await TryGetContentsAsync(SaveNaming.Sanitize(platform), cancellationToken).ConfigureAwait(false);
        return contents
            .Where(c => c.Type == ContentType.Dir)
            .Select(c => c.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<SaveEntry>> ListSavesAsync(string platform, string gameId, CancellationToken cancellationToken = default)
    {
        string folder = SaveNaming.GameFolder(platform, gameId);
        IReadOnlyList<RepositoryContent> contents = await TryGetContentsAsync(folder, cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> descriptions = await ReadDescriptionsAsync(folder, cancellationToken).ConfigureAwait(false);

        List<SaveEntry> result = new List<SaveEntry>();
        foreach (RepositoryContent item in contents)
        {
            if (item.Type != ContentType.File || !SaveNaming.TryParseArchiveName(item.Name, out int index, out SaveType type))
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

        return result
            .OrderBy(e => e.SaveType)
            .ThenBy(e => e.Index)
            .ToArray();
    }

    public async Task<int> GetNextIndexAsync(string platform, string gameId, SaveType saveType, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SaveEntry> saves = await ListSavesAsync(platform, gameId, cancellationToken).ConfigureAwait(false);
        int max = saves.Where(s => s.SaveType == saveType).Select(s => s.Index).DefaultIfEmpty(0).Max();
        return max + 1;
    }

    public async Task<byte[]?> DownloadFileAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        try
        {
            string branch = await GetBranchAsync(cancellationToken).ConfigureAwait(false);
            return await _client.Repository.Content
                .GetRawContentByRef(Owner, Repo, repositoryPath, branch).ConfigureAwait(false);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    public async Task UploadFileAsync(string repositoryPath, byte[] content, CancellationToken cancellationToken = default)
    {
        // Commits a single file directly to the base branch (management op) via the Git Data API.
        string branch = await GetBranchAsync(cancellationToken).ConfigureAwait(false);
        Reference reference = await _client.Git.Reference.Get(Owner, Repo, $"heads/{branch}").ConfigureAwait(false);
        string latestSha = reference.Object.Sha;
        Commit baseCommit = await _client.Git.Commit.Get(Owner, Repo, latestSha).ConfigureAwait(false);

        BlobReference blob = await _client.Git.Blob.Create(Owner, Repo, new NewBlob
        {
            Content = Convert.ToBase64String(content),
            Encoding = EncodingType.Base64,
        }).ConfigureAwait(false);

        NewTree newTree = new NewTree { BaseTree = baseCommit.Tree.Sha };
        newTree.Tree.Add(new NewTreeItem { Path = repositoryPath, Mode = "100644", Type = TreeType.Blob, Sha = blob.Sha });
        TreeResponse tree = await _client.Git.Tree.Create(Owner, Repo, newTree).ConfigureAwait(false);

        Commit commit = await _client.Git.Commit
            .Create(Owner, Repo, new NewCommit($"Update {repositoryPath}", tree.Sha, latestSha))
            .ConfigureAwait(false);
        await _client.Git.Reference
            .Update(Owner, Repo, $"heads/{branch}", new ReferenceUpdate(commit.Sha))
            .ConfigureAwait(false);
    }

    public async Task<bool> DeleteFileAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        string branch = await GetBranchAsync(cancellationToken).ConfigureAwait(false);
        string? sha;
        try
        {
            IReadOnlyList<RepositoryContent> contents = await _client.Repository.Content
                .GetAllContentsByRef(Owner, Repo, repositoryPath, branch).ConfigureAwait(false);
            sha = contents.FirstOrDefault()?.Sha;
        }
        catch (NotFoundException)
        {
            return false;
        }
        if (sha is null)
        {
            return false;
        }

        await _client.Repository.Content
            .DeleteFile(Owner, Repo, repositoryPath, new DeleteFileRequest($"Delete {repositoryPath}", sha, branch))
            .ConfigureAwait(false);
        return true;
    }

    public async Task<SaveUploadResult> UploadAsync(PreparedSave save, UploadOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(options);

        string baseBranch = await GetBranchAsync(cancellationToken).ConfigureAwait(false);
        User currentUser = await _client.User.Current().ConfigureAwait(false);
        Repository repo = await _client.Repository.Get(Owner, Repo).ConfigureAwait(false);
        bool canWrite = HasWriteAccess(repo);

        // Choose where to push: the target repo directly (contributor/owner) or a fork (everyone else).
        string headOwner = Owner;
        if (!canWrite)
        {
            headOwner = await EnsureForkAsync(currentUser.Login, cancellationToken).ConfigureAwait(false);
        }

        Reference baseRef = await _client.Git.Reference.Get(Owner, Repo, $"heads/{baseBranch}").ConfigureAwait(false);
        string baseSha = baseRef.Object.Sha;

        string workBranch = BuildBranchName(save);
        await CreateBranchAsync(headOwner, workBranch, baseSha, cancellationToken).ConfigureAwait(false);

        // Commit the archive; reuse an existing icon if the game folder already has one, otherwise
        // commit the newly resolved cover art.
        List<StorageFile> files = new List<StorageFile> { save.Archive };
        string? existingIcon = await FindExistingIconAsync(save.GameFolder, cancellationToken).ConfigureAwait(false);
        string? iconFileName = existingIcon;
        if (save.Icon is { } newIcon && (save.IconIsExplicit || existingIcon is null))
        {
            files.Add(newIcon);
            iconFileName = newIcon.Path[(newIcon.Path.LastIndexOf('/') + 1)..];
        }

        // Refresh the per-game README (saves index), embedding the icon when one is available.
        string gameReadmePath = $"{save.GameFolder}/{GameReadmeFormatter.FileName}";
        string? existingGameReadme = await ReadTextFileAsync(gameReadmePath, cancellationToken).ConfigureAwait(false);
        string updatedGameReadme = GameReadmeFormatter.Upsert(
            existingGameReadme, save.Platform, save.GameId, save.GameTitle, save.Index, save.SaveType, save.Description, iconFileName);
        files.Add(new StorageFile(gameReadmePath, Encoding.UTF8.GetBytes(updatedGameReadme)));

        // Refresh the platform games index (e.g. PS2/README.md) with this game's title id + name.
        string platformReadmePath = $"{SaveNaming.Sanitize(save.Platform)}/{PlatformReadmeFormatter.FileName}";
        string? existingReadme = await ReadTextFileAsync(platformReadmePath, cancellationToken).ConfigureAwait(false);
        string updatedReadme = PlatformReadmeFormatter.Upsert(existingReadme, save.Platform, save.GameId, save.GameTitle);
        files.Add(new StorageFile(platformReadmePath, Encoding.UTF8.GetBytes(updatedReadme)));

        string commitSha = await CommitFilesAsync(headOwner, workBranch, baseSha, files, save, cancellationToken)
            .ConfigureAwait(false);

        string title = options.Title ?? DefaultTitle(save);
        string body = BuildPullRequestBody(save);
        string head = string.Equals(headOwner, Owner, StringComparison.OrdinalIgnoreCase)
            ? workBranch
            : $"{headOwner}:{workBranch}";

        PullRequest pr = await _client.PullRequest
            .Create(Owner, Repo, new NewPullRequest(title, head, baseBranch) { Body = body })
            .ConfigureAwait(false);

        bool wantMerge = (options.AutoMerge ?? _settings.AutoMerge) && _settings.AutoMerge;
        if (wantMerge && canWrite)
        {
            try
            {
                PullRequestMerge merge = await _client.PullRequest
                    .Merge(Owner, Repo, pr.Number, new MergePullRequest { MergeMethod = PullRequestMergeMethod.Squash })
                    .ConfigureAwait(false);
                if (merge.Merged)
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
            catch (ApiException ex)
            {
                return new SaveUploadResult
                {
                    Success = true,
                    Merged = false,
                    Branch = workBranch,
                    PullRequestUrl = pr.HtmlUrl,
                    ArchivePath = save.Archive.Path,
                    Message = $"Pull request opened (auto-merge failed: {ex.Message}): {pr.HtmlUrl}",
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

    private static bool HasWriteAccess(Repository repo)
    {
        RepositoryPermissions permissions = repo.Permissions;
        return permissions is not null && (permissions.Admin || permissions.Maintain || permissions.Push);
    }

    private async Task<string> EnsureForkAsync(string login, CancellationToken ct)
    {
        try
        {
            Repository existing = await _client.Repository.Get(login, Repo).ConfigureAwait(false);
            if (existing.Fork)
            {
                return login;
            }
        }
        catch (NotFoundException)
        {
            // No fork yet; create one below.
        }

        await _client.Repository.Forks.Create(Owner, Repo, new NewRepositoryFork()).ConfigureAwait(false);

        // Forks are created asynchronously; wait until the repository is available.
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                await _client.Repository.Get(login, Repo).ConfigureAwait(false);
                return login;
            }
            catch (NotFoundException)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Timed out waiting for the GitHub fork to become available.");
    }

    private async Task CreateBranchAsync(string owner, string branch, string sha, CancellationToken ct)
    {
        try
        {
            await _client.Git.Reference.Get(owner, Repo, $"heads/{branch}").ConfigureAwait(false);
            // Branch already exists; move it to the base so we commit onto a clean state.
            await _client.Git.Reference
                .Update(owner, Repo, $"heads/{branch}", new ReferenceUpdate(sha, force: true))
                .ConfigureAwait(false);
        }
        catch (NotFoundException)
        {
            await _client.Git.Reference
                .Create(owner, Repo, new NewReference($"refs/heads/{branch}", sha))
                .ConfigureAwait(false);
        }
    }

    private async Task<string> CommitFilesAsync(
        string owner,
        string branch,
        string baseSha,
        IReadOnlyList<StorageFile> files,
        PreparedSave save,
        CancellationToken ct)
    {
        Commit baseCommit = await _client.Git.Commit.Get(owner, Repo, baseSha).ConfigureAwait(false);
        NewTree newTree = new NewTree { BaseTree = baseCommit.Tree.Sha };

        foreach (StorageFile file in files)
        {
            BlobReference blob = await _client.Git.Blob.Create(owner, Repo, new NewBlob
            {
                Content = Convert.ToBase64String(file.Content),
                Encoding = EncodingType.Base64,
            }).ConfigureAwait(false);

            newTree.Tree.Add(new NewTreeItem
            {
                Path = file.Path,
                Mode = "100644",
                Type = TreeType.Blob,
                Sha = blob.Sha,
            });
        }

        TreeResponse tree = await _client.Git.Tree.Create(owner, Repo, newTree).ConfigureAwait(false);
        string message = DefaultTitle(save);
        NewCommit newCommit = new NewCommit(message, tree.Sha, baseSha);

        if (!string.IsNullOrWhiteSpace(_settings.CommitterName) && !string.IsNullOrWhiteSpace(_settings.CommitterEmail))
        {
            Committer signature = new Committer(_settings.CommitterName, _settings.CommitterEmail, DateTimeOffset.UtcNow);
            newCommit = new NewCommit(message, tree.Sha, baseSha) { Author = signature, Committer = signature };
        }

        Commit commit = await _client.Git.Commit.Create(owner, Repo, newCommit).ConfigureAwait(false);
        await _client.Git.Reference
            .Update(owner, Repo, $"heads/{branch}", new ReferenceUpdate(commit.Sha))
            .ConfigureAwait(false);
        return commit.Sha;
    }

    private async Task<string?> FindExistingIconAsync(string folder, CancellationToken ct)
    {
        IReadOnlyList<RepositoryContent> contents = await TryGetContentsAsync(folder, ct).ConfigureAwait(false);
        return contents
            .FirstOrDefault(c => c.Type == ContentType.File &&
                                 c.Name.StartsWith("icon.", StringComparison.OrdinalIgnoreCase))?
            .Name;
    }

    private async Task<IReadOnlyList<RepositoryContent>> TryGetContentsAsync(string path, CancellationToken ct)
    {
        try
        {
            string branch = await GetBranchAsync(ct).ConfigureAwait(false);
            return string.IsNullOrEmpty(path)
                ? await _client.Repository.Content.GetAllContentsByRef(Owner, Repo, branch).ConfigureAwait(false)
                : await _client.Repository.Content.GetAllContentsByRef(Owner, Repo, path, branch).ConfigureAwait(false);
        }
        catch (NotFoundException)
        {
            return Array.Empty<RepositoryContent>();
        }
    }

    private async Task<string?> ReadTextFileAsync(string path, CancellationToken ct)
    {
        try
        {
            string branch = await GetBranchAsync(ct).ConfigureAwait(false);
            IReadOnlyList<RepositoryContent> contents = await _client.Repository.Content
                .GetAllContentsByRef(Owner, Repo, path, branch).ConfigureAwait(false);
            return contents.FirstOrDefault()?.Content;
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    private async Task<Dictionary<string, string>> ReadDescriptionsAsync(string folder, CancellationToken ct)
    {
        string? raw = await ReadTextFileAsync($"{folder}/{GameReadmeFormatter.FileName}", ct).ConfigureAwait(false);
        return new Dictionary<string, string>(GameReadmeFormatter.ParseDescriptions(raw), StringComparer.OrdinalIgnoreCase);
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

    private static string BuildPullRequestBody(PreparedSave save)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"**Platform:** {save.Platform}");
        builder.AppendLine($"**Game:** {save.GameId}");
        builder.AppendLine($"**Type:** {SaveNaming.Label(save.SaveType)}");
        builder.AppendLine($"**Archive:** `{save.Archive.Path}`");
        builder.AppendLine();
        builder.AppendLine("**Description:**");
        builder.AppendLine(save.Description);
        builder.AppendLine();
        builder.AppendLine("_Submitted with SaveHub._");
        return builder.ToString();
    }
}
