namespace SaveHub.Core.Models;

/// <summary>
/// Describes a single save already present in a storage backend.
/// </summary>
public sealed class SaveEntry
{
    public required string Platform { get; init; }
    public required string GameId { get; init; }
    public required SaveType SaveType { get; init; }

    /// <summary>The incremental index encoded in the archive name (e.g. 1 for "01.zip").</summary>
    public required int Index { get; init; }

    /// <summary>The archive file name, e.g. "01.zip" or "01-sstate.zip".</summary>
    public required string ArchiveName { get; init; }

    /// <summary>Description read from the side-car text file, if available.</summary>
    public string? Description { get; init; }
}
