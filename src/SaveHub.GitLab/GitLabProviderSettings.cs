namespace SaveHub.GitLab;

/// <summary>
/// Settings for the GitLab storage provider, persisted in the SaveHub config under the "gitlab" key.
/// </summary>
public sealed class GitLabProviderSettings
{
    /// <summary>Base URL of the GitLab instance. Defaults to gitlab.com; set for self-hosted instances.</summary>
    public string BaseUrl { get; set; } = "https://gitlab.com";

    /// <summary>Project namespace (user or group path), e.g. "your-name".</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>Project path (repository slug), e.g. "my-saves".</summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>Target branch that saves are contributed to. Defaults to the project default when empty.</summary>
    public string Branch { get; set; } = string.Empty;

    /// <summary>
    /// Personal access token (scope <c>api</c>). Prefer leaving this empty and supplying the token via
    /// the environment variable named by <see cref="TokenEnvironmentVariable"/>.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>Environment variable checked for the token when <see cref="Token"/> is empty.</summary>
    public string TokenEnvironmentVariable { get; set; } = "SAVEHUB_GITLAB_TOKEN";

    /// <summary>
    /// When true, SaveHub attempts to merge the merge request automatically. This only succeeds when
    /// the authenticated user can merge (Maintainer or above); otherwise the MR is left for review.
    /// </summary>
    public bool AutoMerge { get; set; }

    /// <summary>Optional commit author name. Falls back to the authenticated user.</summary>
    public string? CommitterName { get; set; }

    /// <summary>Optional commit author email. Falls back to the authenticated user.</summary>
    public string? CommitterEmail { get; set; }

    /// <summary>Resolves the effective token from the setting or environment variable.</summary>
    public string? ResolveToken()
    {
        if (!string.IsNullOrWhiteSpace(Token))
        {
            return Token;
        }
        string? fromEnv = Environment.GetEnvironmentVariable(TokenEnvironmentVariable);
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv;
    }
}
