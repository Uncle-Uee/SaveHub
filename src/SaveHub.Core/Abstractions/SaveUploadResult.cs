namespace SaveHub.Core.Abstractions;

/// <summary>The outcome of an upload.</summary>
public sealed class SaveUploadResult
{
    public required bool Success { get; init; }

    /// <summary>True when the change was merged; false when it was left as a pull request for review.</summary>
    public required bool Merged { get; init; }

    /// <summary>Branch created for the change, when applicable.</summary>
    public string? Branch { get; init; }

    /// <summary>The pull request / review URL, when one was created.</summary>
    public string? PullRequestUrl { get; init; }

    /// <summary>The committed archive path within the repository, e.g. "PS2/SLUS-21274/01.zip".</summary>
    public string? ArchivePath { get; init; }

    /// <summary>Human-readable status message.</summary>
    public required string Message { get; init; }
}
