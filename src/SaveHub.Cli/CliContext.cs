using Google.Apis.Util.Store;
using SaveHub.Core;
using SaveHub.Core.Configuration;
using SaveHub.GoogleDrive;
using SaveHub.Hosting;

namespace SaveHub.Cli;

/// <summary>Resolves the config store and the active storage provider for CLI commands.</summary>
internal static class CliContext
{
    public static SaveHubConfigStore ResolveStore(string? configPath)
    {
        string path = string.IsNullOrWhiteSpace(configPath) ? SaveHubConfigStore.DefaultPath : configPath;
        return new SaveHubConfigStore(path);
    }

    /// <summary>Builds a client for the active provider, throwing with guidance when unconfigured.</summary>
    public static SaveHubClient CreateClient(SaveHubConfig config)
    {
        return SaveHubHost.CreateClient(config);
    }

    /// <summary>
    /// Builds a client, first ensuring an interactive provider (Google Drive) has a session. Google
    /// uses a persisted token cache, so this is silent when a valid token already exists.
    /// </summary>
    public static async Task<SaveHubClient> CreateClientAsync(SaveHubConfig config, CancellationToken cancellationToken = default)
    {
        if (string.Equals(config.ActiveProvider, GoogleDriveProviderFactory.ProviderName, StringComparison.OrdinalIgnoreCase)
            && !GoogleDriveSession.HasActiveSession)
        {
            GoogleDriveProviderSettings settings = GoogleDriveProviderFactory.ReadSettings(config)
                ?? throw new InvalidOperationException("Google Drive is not configured. Run 'savehub config google' first.");
            await GoogleDriveAuthenticator
                .SignInAsync(settings, new FileDataStore("SaveHub.GoogleDrive"), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        return SaveHubHost.CreateClient(config);
    }
}
