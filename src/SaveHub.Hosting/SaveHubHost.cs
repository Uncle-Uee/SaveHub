using SaveHub.Core;
using SaveHub.Core.Abstractions;
using SaveHub.Core.Archiving;
using SaveHub.Core.Configuration;
using SaveHub.Bitbucket;
using SaveHub.GitHub;
using SaveHub.GitLab;
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
        new(GitLabProviderFactory.ProviderName, "GitLab"),
        new(BitbucketProviderFactory.ProviderName, "Bitbucket"),
        new(SupabaseProviderFactory.ProviderName, "Supabase"),
        new(GoogleDriveProviderFactory.ProviderName, "Google Drive"),
    ];

    /// <summary>Builds a client for the active provider in <paramref name="config"/>.</summary>
    public static SaveHubClient CreateClient(SaveHubConfig config)
    {
        return CreateClient(config, null);
    }

    /// <summary>Builds a client for the active provider, using the given cover-art resolver.</summary>
    public static SaveHubClient CreateClient(SaveHubConfig config, ICoverArtResolver? coverArtResolver)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new SaveHubClient(CreateProvider(config), coverArtResolver);
    }

    private static ISaveStorageProvider CreateProvider(SaveHubConfig config)
    {
        string name = (config.ActiveProvider ?? string.Empty).Trim().ToLowerInvariant();
        return name switch
        {
            GitHubProviderFactory.ProviderName => GitHubProviderFactory.Create(config),
            GitLabProviderFactory.ProviderName => GitLabProviderFactory.Create(config),
            BitbucketProviderFactory.ProviderName => BitbucketProviderFactory.Create(config),
            SupabaseProviderFactory.ProviderName => SupabaseProviderFactory.Create(config),
            GoogleDriveProviderFactory.ProviderName => GoogleDriveProviderFactory.Create(config),
            _ => throw new InvalidOperationException(
                $"Unknown or unconfigured provider '{config.ActiveProvider}'. Configure one first."),
        };
    }

    /// <summary>Builds a client, or returns null with a reason when it cannot be created.</summary>
    public static SaveHubClient? TryCreateClient(SaveHubConfig config, out string error)
    {
        return TryCreateClient(config, null, out error);
    }

    /// <summary>Builds a client with the given cover-art resolver, or returns null with a reason.</summary>
    public static SaveHubClient? TryCreateClient(SaveHubConfig config, ICoverArtResolver? coverArtResolver, out string error)
    {
        error = string.Empty;
        try
        {
            return CreateClient(config, coverArtResolver);
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
