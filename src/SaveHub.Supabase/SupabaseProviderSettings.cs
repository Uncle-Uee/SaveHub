namespace SaveHub.Supabase;

/// <summary>Settings for the Supabase Storage provider, persisted under the "supabase" config key.</summary>
public sealed class SupabaseProviderSettings
{
    /// <summary>Project URL, e.g. https://YOUR-PROJECT.supabase.co.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Storage bucket that holds the save database, e.g. "saves".</summary>
    public string Bucket { get; set; } = "saves";

    /// <summary>API key (service role or user JWT). Prefer the environment variable below.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Environment variable checked for the key when <see cref="ApiKey"/> is empty.</summary>
    public string ApiKeyEnvironmentVariable { get; set; } = "SAVEHUB_SUPABASE_KEY";

    /// <summary>
    /// When true, uploads publish directly to the bucket root. When false, uploads go under a
    /// <c>pending/</c> prefix for a maintainer to review and move.
    /// </summary>
    public bool IsOwner { get; set; }

    public string? ResolveKey()
    {
        if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            return ApiKey;
        }
        string? fromEnv = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv;
    }
}
