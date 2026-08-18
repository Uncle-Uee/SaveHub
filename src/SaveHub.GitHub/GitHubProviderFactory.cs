using SaveHub.Core.Configuration;

namespace SaveHub.GitHub;

/// <summary>Helpers to read/write GitHub settings from a <see cref="SaveHubConfig"/> and build the provider.</summary>
public static class GitHubProviderFactory
{
    public const string ProviderName = "github";

    /// <summary>Reads the GitHub settings section from config, or null when unconfigured.</summary>
    public static GitHubProviderSettings? ReadSettings(SaveHubConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.GetProviderSettings<GitHubProviderSettings>(ProviderName);
    }

    /// <summary>Writes the GitHub settings section into config and marks GitHub as the active provider.</summary>
    public static void WriteSettings(SaveHubConfig config, GitHubProviderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(settings);
        config.SetProviderSettings(ProviderName, settings);
        config.ActiveProvider = ProviderName;
    }

    /// <summary>Builds a provider from the GitHub settings stored in config.</summary>
    public static GitHubSaveStorageProvider Create(SaveHubConfig config)
    {
        GitHubProviderSettings settings = ReadSettings(config)
            ?? throw new InvalidOperationException("GitHub is not configured. Run the configure step first.");
        return new GitHubSaveStorageProvider(settings);
    }
}
