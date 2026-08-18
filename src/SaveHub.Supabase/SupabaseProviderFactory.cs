using SaveHub.Core.Configuration;

namespace SaveHub.Supabase;

/// <summary>Reads/writes Supabase settings in a <see cref="SaveHubConfig"/> and builds the provider.</summary>
public static class SupabaseProviderFactory
{
    public const string ProviderName = "supabase";

    public static SupabaseProviderSettings? ReadSettings(SaveHubConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.GetProviderSettings<SupabaseProviderSettings>(ProviderName);
    }

    public static void WriteSettings(SaveHubConfig config, SupabaseProviderSettings settings, bool makeActive = true)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(settings);
        config.SetProviderSettings(ProviderName, settings);
        if (makeActive)
        {
            config.ActiveProvider = ProviderName;
        }
    }

    public static SupabaseSaveStorageProvider Create(SaveHubConfig config)
    {
        SupabaseProviderSettings settings = ReadSettings(config) ?? throw new InvalidOperationException("Supabase is not configured.");
        return new SupabaseSaveStorageProvider(settings);
    }
}
