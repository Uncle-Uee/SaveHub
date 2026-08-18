using SaveHub.Core.Models;

namespace SaveHub.Core.Archiving;

/// <summary>
/// The fully prepared set of artifacts for one save, ready to be committed by a storage provider.
/// </summary>
public sealed class PreparedSave
{
    public required string Platform { get; init; }
    public required string GameId { get; init; }
    public required SaveType SaveType { get; init; }
    public required int Index { get; init; }
    public required string Description { get; init; }

    /// <summary>Optional friendly game title, used in the platform games index.</summary>
    public string? GameTitle { get; init; }

    /// <summary>The folder for this game, e.g. "PS2/SLUS-21274".</summary>
    public required string GameFolder { get; init; }

    /// <summary>The archive file (zip) which embeds the description manifest.</summary>
    public required StorageFile Archive { get; init; }

    /// <summary>Optional icon / cover-art file to store in the game folder.</summary>
    public StorageFile? Icon { get; init; }

    /// <summary>True when the icon was explicitly supplied by the user (should overwrite an existing icon).</summary>
    public bool IconIsExplicit { get; init; }

    /// <summary>All files that should be committed (archive, and icon when present).</summary>
    public IEnumerable<StorageFile> AllFiles()
    {
        yield return Archive;
        if (Icon is { } icon)
        {
            yield return icon;
        }
    }
}

/// <summary>A single file to be committed to a storage backend.</summary>
/// <param name="Path">Repository-relative path, e.g. "PS2/SLUS-21274/01.zip".</param>
/// <param name="Content">Raw file bytes.</param>
public readonly record struct StorageFile(string Path, byte[] Content);
