namespace SaveHub.Core.Models;

/// <summary>
/// A request to upload a single save (one memory card, or one save state) to a storage provider.
/// </summary>
public sealed class SaveUploadRequest
{
    /// <summary>Platform folder name, e.g. "PS2", "GBA". See <see cref="KnownPlatforms"/>.</summary>
    public required string Platform { get; init; }

    /// <summary>
    /// The game identifier used as the sub-folder name, e.g. "SLUS-21274". This is normally the
    /// serial / title id of the game.
    /// </summary>
    public required string GameId { get; init; }

    /// <summary>Whether this is a memory card or a save state.</summary>
    public required SaveType SaveType { get; init; }

    /// <summary>
    /// Absolute paths to the file(s) that make up the save. A memory card is normally a single file;
    /// save states and some handheld saves (e.g. GBA) may consist of multiple files.
    /// </summary>
    public required IReadOnlyList<string> Files { get; init; }

    /// <summary>
    /// Optional root directory for a <see cref="Models.SaveType.SaveFolder"/> upload. When set, files
    /// are stored in the zip using paths relative to this directory so the folder structure is kept.
    /// </summary>
    public string? RootDirectory { get; init; }

    /// <summary>A short human description of the save, e.g. "100% completion, all worlds cleared".</summary>
    public required string Description { get; init; }

    /// <summary>Optional friendly game title, e.g. "Kingdom Hearts II".</summary>
    public string? GameTitle { get; init; }

    /// <summary>
    /// Optional emulator name. Recommended for save states because save states are frequently not
    /// interchangeable between emulators (e.g. mGBA vs VBA-M).
    /// </summary>
    public string? Emulator { get; init; }

    /// <summary>Optional path to a game icon / cover image (png/jpg) to store alongside the save.</summary>
    public string? IconPath { get; init; }

    /// <summary>
    /// When true (default) and no <see cref="IconPath"/> is supplied, SaveHub tries to download cover
    /// art automatically from the known public sources for the platform (PS1/PS2/PSP).
    /// </summary>
    public bool AutoFetchCoverArt { get; init; } = true;

    /// <summary>Optional free-form notes appended to the manifest, e.g. compatibility warnings.</summary>
    public string? Notes { get; init; }
}
