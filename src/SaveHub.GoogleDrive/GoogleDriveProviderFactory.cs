using SaveHub.Core.Configuration;

namespace SaveHub.GoogleDrive;

/// <summary>Reads/writes Google Drive settings and builds the provider from the current session.</summary>
public static class GoogleDriveProviderFactory
{
    public const string ProviderName = "googledrive";

    public static GoogleDriveProviderSettings? ReadSettings(SaveHubConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.GetProviderSettings<GoogleDriveProviderSettings>(ProviderName);
    }

    public static void WriteSettings(SaveHubConfig config, GoogleDriveProviderSettings settings, bool makeActive = true)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(settings);
        config.SetProviderSettings(ProviderName, settings);
        if (makeActive)
        {
            config.ActiveProvider = ProviderName;
        }
    }

    /// <summary>
    /// Builds the provider using the active <see cref="GoogleDriveSession"/>. Sign in first
    /// (<see cref="GoogleDriveAuthenticator.SignInAsync"/>); throws if there is no active session.
    /// </summary>
    public static GoogleDriveSaveStorageProvider Create(SaveHubConfig config)
    {
        GoogleDriveProviderSettings settings = ReadSettings(config)
            ?? throw new InvalidOperationException("Google Drive is not configured.");
        if (!GoogleDriveSession.HasActiveSession)
        {
            throw new InvalidOperationException("Sign in to Google Drive first (the session has expired or is not started).");
        }
        return new GoogleDriveSaveStorageProvider(GoogleDriveSession.Current!.Service, settings);
    }
}
