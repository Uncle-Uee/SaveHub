namespace SaveHub.GoogleDrive;

/// <summary>Settings for the Google Drive provider, persisted under the "googledrive" config key.</summary>
public sealed class GoogleDriveProviderSettings
{
    /// <summary>
    /// Name of the folder SaveHub creates (and reuses) at your Drive root to hold the save database.
    /// With the <c>drive.file</c> scope the app can only see folders it created, so it manages this
    /// folder itself — you do not need to pre-create one or paste a folder id.
    /// </summary>
    public string RootFolderName { get; set; } = "SaveHub";

    /// <summary>
    /// Optional advanced override: an explicit folder id to use as the root. Only works when the
    /// folder was created by this app (drive.file cannot access arbitrary folders). Leave empty to
    /// use <see cref="RootFolderName"/>.
    /// </summary>
    public string RootFolderId { get; set; } = string.Empty;

    /// <summary>OAuth client id (Desktop app) from your own Google Cloud project.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth client secret. Prefer the environment variable below.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Environment variable checked for the client secret when it is empty.</summary>
    public string ClientSecretEnvironmentVariable { get; set; } = "SAVEHUB_GDRIVE_CLIENT_SECRET";

    /// <summary>
    /// When true, uploads publish directly under the root folder. When false, uploads go under a
    /// <c>pending/</c> sub-folder for a maintainer to review and move.
    /// </summary>
    public bool IsOwner { get; set; } = true;

    public string? ResolveClientSecret()
    {
        if (!string.IsNullOrWhiteSpace(ClientSecret))
        {
            return ClientSecret;
        }
        string? fromEnv = Environment.GetEnvironmentVariable(ClientSecretEnvironmentVariable);
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv;
    }
}
