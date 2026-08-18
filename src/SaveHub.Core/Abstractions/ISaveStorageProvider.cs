using SaveHub.Core.Archiving;
using SaveHub.Core.Models;

namespace SaveHub.Core.Abstractions;

/// <summary>
/// A storage backend that hosts the save database (GitHub today; Google Drive, Supabase, Firebase,
/// etc. in the future). Implementations are responsible for placing archives, maintaining the
/// per-game and per-platform <c>README.md</c> indexes, and enforcing their own permission rules for
/// auto-merge.
/// </summary>
public interface ISaveStorageProvider
{
    /// <summary>Stable provider key, e.g. "github".</summary>
    string Name { get; }

    /// <summary>What this provider can do.</summary>
    StorageProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Verifies connectivity and credentials against the backend without making any changes.
    /// </summary>
    Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists platform folders that already exist in the backend.</summary>
    Task<IReadOnlyList<string>> ListPlatformsAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists game folders under a platform.</summary>
    Task<IReadOnlyList<string>> ListGamesAsync(string platform, CancellationToken cancellationToken = default);

    /// <summary>Lists the saves stored for a game.</summary>
    Task<IReadOnlyList<SaveEntry>> ListSavesAsync(string platform, string gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the next free incremental index for the given save type in a game folder, so the
    /// caller can build a correctly named archive.
    /// </summary>
    Task<int> GetNextIndexAsync(string platform, string gameId, SaveType saveType, CancellationToken cancellationToken = default);

    /// <summary>Commits a prepared save and returns the outcome.</summary>
    Task<SaveUploadResult> UploadAsync(PreparedSave save, UploadOptions options, CancellationToken cancellationToken = default);

    /// <summary>Downloads a file's raw bytes by its repository-relative path, or null when missing.</summary>
    Task<byte[]?> DownloadFileAsync(string repositoryPath, CancellationToken cancellationToken = default);

    /// <summary>Creates or replaces a file at the given repository-relative path.</summary>
    Task UploadFileAsync(string repositoryPath, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>Deletes a file by its repository-relative path. Returns true when a file was removed.</summary>
    Task<bool> DeleteFileAsync(string repositoryPath, CancellationToken cancellationToken = default);
}
