using SaveHub.Core.Configuration;

namespace SaveHub.Bitbucket;

/// <summary>Helpers to read/write Bitbucket settings from a <see cref="SaveHubConfig"/> and build the provider.</summary>
public static class BitbucketProviderFactory
{
    public const string ProviderName = "bitbucket";

    /// <summary>Reads the Bitbucket settings section from config, or null when unconfigured.</summary>
    public static BitbucketProviderSettings? ReadSettings(SaveHubConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.GetProviderSettings<BitbucketProviderSettings>(ProviderName);
    }

    /// <summary>Writes the Bitbucket settings section into config and marks Bitbucket as the active provider.</summary>
    public static void WriteSettings(SaveHubConfig config, BitbucketProviderSettings settings, bool makeActive = true)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(settings);
        config.SetProviderSettings(ProviderName, settings);
        if (makeActive)
        {
            config.ActiveProvider = ProviderName;
        }
    }

    /// <summary>Builds a provider from the Bitbucket settings stored in config.</summary>
    public static BitbucketSaveStorageProvider Create(SaveHubConfig config)
    {
        BitbucketProviderSettings settings = ReadSettings(config)
            ?? throw new InvalidOperationException("Bitbucket is not configured. Run the configure step first.");
        return new BitbucketSaveStorageProvider(settings);
    }
}
