using Google.Apis.Drive.v3;

namespace SaveHub.GoogleDrive;

/// <summary>
/// An authenticated Google Drive session. Holds the <see cref="DriveService"/> obtained from the
/// browser sign-in and a soft expiry (the user is asked to sign in again afterwards).
/// </summary>
public sealed class GoogleDriveSession
{
    /// <summary>Default session lifetime before re-sign-in is requested.</summary>
    public static readonly TimeSpan DefaultLength = TimeSpan.FromHours(2.5);

    public DriveService Service { get; }
    public DateTimeOffset ExpiresAt { get; }
    public string? AccountEmail { get; }

    public bool IsActive => DateTimeOffset.UtcNow < ExpiresAt;

    /// <summary>The current process-wide session, set after a successful sign-in.</summary>
    public static GoogleDriveSession? Current { get; set; }

    public static bool HasActiveSession => Current is { IsActive: true };

    public GoogleDriveSession(DriveService service, DateTimeOffset expiresAt, string? accountEmail)
    {
        Service = service;
        ExpiresAt = expiresAt;
        AccountEmail = accountEmail;
    }
}
