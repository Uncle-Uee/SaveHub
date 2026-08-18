using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Json;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace SaveHub.GoogleDrive;

/// <summary>
/// Performs the interactive Google sign-in (browser loopback OAuth) and produces a
/// <see cref="GoogleDriveSession"/>. Frontends choose where tokens are cached:
/// <see cref="MemoryTokenStore"/> for a per-run session (desktop), or a
/// <see cref="FileDataStore"/> to persist across CLI invocations.
/// </summary>
public static class GoogleDriveAuthenticator
{
    /// <summary>
    /// Opens the browser, signs the user in, and returns an active session. The session is also set
    /// as <see cref="GoogleDriveSession.Current"/>.
    /// </summary>
    /// <param name="settings">Provider settings holding the OAuth client id/secret.</param>
    /// <param name="tokenStore">Where to cache the token (memory = session only).</param>
    /// <param name="sessionLength">Soft session lifetime. Defaults to <see cref="GoogleDriveSession.DefaultLength"/>.</param>
    public static async Task<GoogleDriveSession> SignInAsync(
        GoogleDriveProviderSettings settings,
        IDataStore tokenStore,
        TimeSpan? sessionLength = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tokenStore);
        if (string.IsNullOrWhiteSpace(settings.ClientId))
        {
            throw new InvalidOperationException("Google Drive requires an OAuth ClientId.");
        }
        string secret = settings.ResolveClientSecret()
            ?? throw new InvalidOperationException(
                $"No OAuth client secret. Set it in the config or the '{settings.ClientSecretEnvironmentVariable}' env var.");

        UserCredential credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            new ClientSecrets { ClientId = settings.ClientId, ClientSecret = secret },
            [DriveService.Scope.DriveFile],
            "user",
            cancellationToken,
            tokenStore).ConfigureAwait(false);

        DriveService drive = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "SaveHub",
        });

        string? email = null;
        try
        {
            AboutResource.GetRequest about = drive.About.Get();
            about.Fields = "user";
            Google.Apis.Drive.v3.Data.About result = await about.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            email = result.User?.EmailAddress;
        }
        catch
        {
            // Non-fatal: sign-in succeeded even if we couldn't read the profile.
        }

        GoogleDriveSession session = new GoogleDriveSession(drive, DateTimeOffset.UtcNow + (sessionLength ?? GoogleDriveSession.DefaultLength), email);
        GoogleDriveSession.Current = session;
        return session;
    }

    /// <summary>An in-memory token cache: the session lives only for the life of the process.</summary>
    public sealed class MemoryTokenStore : IDataStore
    {
        private readonly Dictionary<string, string> _data = new();

        public Task ClearAsync()
        {
            _data.Clear();
            return Task.CompletedTask;
        }

        public Task DeleteAsync<T>(string key)
        {
            _data.Remove(key);
            return Task.CompletedTask;
        }

        public Task<T> GetAsync<T>(string key)
        {
            return _data.TryGetValue(key, out string? json)
                ? Task.FromResult(NewtonsoftJsonSerializer.Instance.Deserialize<T>(json))
                : Task.FromResult<T>(default!);
        }

        public Task StoreAsync<T>(string key, T value)
        {
            _data[key] = NewtonsoftJsonSerializer.Instance.Serialize(value);
            return Task.CompletedTask;
        }
    }
}
