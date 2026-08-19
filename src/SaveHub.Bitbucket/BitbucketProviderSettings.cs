namespace SaveHub.Bitbucket;

/// <summary>
/// Settings for the Bitbucket storage provider, persisted in the SaveHub config under the "bitbucket"
/// key. Authentication uses a username plus an app password (Basic auth).
/// </summary>
public sealed class BitbucketProviderSettings
{
    /// <summary>Workspace (repository owner) ID, e.g. "your-name".</summary>
    public string Workspace { get; set; } = string.Empty;

    /// <summary>Repository slug, e.g. "my-saves".</summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>Target branch that saves are contributed to. Defaults to the repo main branch when empty.</summary>
    public string Branch { get; set; } = string.Empty;

    /// <summary>Bitbucket username the app password belongs to.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// App password (with Repositories read/write and Pull requests write). Prefer leaving this empty
    /// and supplying it via the environment variable named by <see cref="AppPasswordEnvironmentVariable"/>.
    /// </summary>
    public string? AppPassword { get; set; }

    /// <summary>Environment variable checked for the app password when <see cref="AppPassword"/> is empty.</summary>
    public string AppPasswordEnvironmentVariable { get; set; } = "SAVEHUB_BITBUCKET_APP_PASSWORD";

    /// <summary>
    /// When true, SaveHub attempts to merge the pull request automatically. This only succeeds when the
    /// authenticated user has write access; otherwise the pull request is left open for review.
    /// </summary>
    public bool AutoMerge { get; set; }

    /// <summary>Optional commit author name. Falls back to the authenticated user.</summary>
    public string? CommitterName { get; set; }

    /// <summary>Optional commit author email. Falls back to the authenticated user.</summary>
    public string? CommitterEmail { get; set; }

    /// <summary>Resolves the effective app password from the setting or environment variable.</summary>
    public string? ResolveAppPassword()
    {
        if (!string.IsNullOrWhiteSpace(AppPassword))
        {
            return AppPassword;
        }
        string? fromEnv = Environment.GetEnvironmentVariable(AppPasswordEnvironmentVariable);
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv;
    }
}
