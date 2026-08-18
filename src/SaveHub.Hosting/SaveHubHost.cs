using SaveHub.Core;
using SaveHub.Core.Configuration;
using SaveHub.GitHub;
using SaveHub.GoogleDrive;
using SaveHub.Supabase;

namespace SaveHub.Hosting;

/// <summary>
/// Aggregates every storage provider so the CLI and desktop app can be provider-agnostic: they only
/// depend on <c>SaveHub.Core</c> + this hosting layer, and pick the active provider from config.
/// </summary>
public static class SaveHubHost
{
    public static readonly IReadOnlyList<ProviderDescriptor> Providers =
    [
        new(GitHubProviderFactory.ProviderName, "GitHub"),
        new(SupabaseProviderFactory.ProviderName, "Supabase"),
        new(GoogleDriveProviderFactory.ProviderName, "Google Drive"),
    ];

    /// <summary>Builds a client for the active provider in <paramref name="config"/>.</summary>
    public static SaveHubClient CreateClient(SaveHubConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        string name = (config.ActiveProvider ?? string.Empty).Trim().ToLowerInvariant();
        return name switch
        {
            GitHubProviderFactory.ProviderName => new SaveHubClient(GitHubProviderFactory.Create(config)),
            SupabaseProviderFactory.ProviderName => new SaveHubClient(SupabaseProviderFactory.Create(config)),
            GoogleDriveProviderFactory.ProviderName => new SaveHubClient(GoogleDriveProviderFactory.Create(config)),
            _ => throw new InvalidOperationException(
                $"Unknown or unconfigured provider '{config.ActiveProvider}'. Configure one first."),
        };
    }

    /// <summary>Builds a client, or returns null with a reason when it cannot be created.</summary>
    public static SaveHubClient? TryCreateClient(SaveHubConfig config, out string error)
    {
        error = string.Empty;
        try
        {
            return CreateClient(config);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }
}

/// <summary>Metadata about a storage provider for UIs to list.</summary>
public readonly record struct ProviderDescriptor(string Name, string DisplayName);
