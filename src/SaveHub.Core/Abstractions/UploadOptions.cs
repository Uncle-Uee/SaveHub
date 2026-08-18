namespace SaveHub.Core.Abstractions;

/// <summary>Options that influence how a single upload is performed.</summary>
public sealed class UploadOptions
{
    /// <summary>
    /// Whether to merge the change automatically instead of leaving a pull request for review.
    /// When null, the provider falls back to its configured default (e.g. the GitHub
    /// <c>autoMerge</c> setting). Providers only merge when the authenticated user is permitted to
    /// (see <see cref="StorageProviderCapabilities.SupportsAutoMerge"/>); otherwise a pull request
    /// is opened.
    /// </summary>
    public bool? AutoMerge { get; init; }

    /// <summary>Optional custom commit / pull-request title. A sensible default is used when null.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// When set, the save replaces the existing archive at this index instead of appending a new one.
    /// Used by "edit" flows (e.g. update <c>01.zip</c> rather than create <c>02.zip</c>).
    /// </summary>
    public int? TargetIndex { get; init; }
}
