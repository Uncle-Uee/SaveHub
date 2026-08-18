namespace SaveHub.GitHub;

/// <summary>
/// Settings for the GitHub storage provider, persisted in the SaveHub config under the "github" key.
/// </summary>
public sealed class GitHubProviderSettings
{
    /// <summary>Repository owner (user or organization), e.g. "your-name".</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>Repository name, e.g. "my-saves".</summary>
    public string Repository { get; set; } = string.Empty;

    /// <summary>Target branch that saves are contributed to. Defaults to the repo default when empty.</summary>
    public string Branch { get; set; } = string.Empty;

    /// <summary>
    /// Personal access token. Prefer leaving this empty and supplying the token via the environment
    /// variable named by <see cref="TokenEnvironmentVariable"/> so secrets stay out of the config file.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>Environment variable checked for the token when <see cref="Token"/> is empty.</summary>
    public string TokenEnvironmentVariable { get; set; } = "SAVEHUB_GITHUB_TOKEN";

    /// <summary>
    /// When true, SaveHub will attempt to merge the pull request automatically. This only succeeds
    /// when the authenticated user has write access (owner or contributor); otherwise a pull request
    /// is opened for review. Enabling this is at the user's own risk.
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
