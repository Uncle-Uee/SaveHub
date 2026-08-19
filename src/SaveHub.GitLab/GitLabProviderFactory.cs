using SaveHub.Core.Configuration;

namespace SaveHub.GitLab;

/// <summary>Helpers to read/write GitLab settings from a <see cref="SaveHubConfig"/> and build the provider.</summary>
public static class GitLabProviderFactory
{
    public const string ProviderName = "gitlab";

    /// <summary>Reads the GitLab settings section from config, or null when unconfigured.</summary>
    public static GitLabProviderSettings? ReadSettings(SaveHubConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.GetProviderSettings<GitLabProviderSettings>(ProviderName);
    }

    /// <summary>Writes the GitLab settings section into config and marks GitLab as the active provider.</summary>
    public static void WriteSettings(SaveHubConfig config, GitLabProviderSettings settings, bool makeActive = true)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(settings);
        config.SetProviderSettings(ProviderName, settings);
        if (makeActive)
        {
            config.ActiveProvider = ProviderName;
        }
    }

    /// <summary>Builds a provider from the GitLab settings stored in config.</summary>
    public static GitLabSaveStorageProvider Create(SaveHubConfig config)
    {
        GitLabProviderSettings settings = ReadSettings(config)
            ?? throw new InvalidOperationException("GitLab is not configured. Run the configure step first.");
        return new GitLabSaveStorageProvider(settings);
    }
}
