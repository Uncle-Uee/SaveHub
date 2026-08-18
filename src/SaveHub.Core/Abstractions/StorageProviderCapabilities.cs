namespace SaveHub.Core.Abstractions;

/// <summary>Describes what a storage provider is able to do.</summary>
public sealed class StorageProviderCapabilities
{
    /// <summary>Whether uploads are delivered as a reviewable pull request.</summary>
    public required bool SupportsPullRequests { get; init; }

    /// <summary>Whether the provider can automatically merge an upload without review.</summary>
    public required bool SupportsAutoMerge { get; init; }

    /// <summary>Whether the provider can enumerate existing platforms/games/saves.</summary>
    public required bool SupportsBrowsing { get; init; }
}
