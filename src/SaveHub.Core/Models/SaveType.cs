namespace SaveHub.Core.Models;

/// <summary>
/// The kind of save data being uploaded.
/// </summary>
public enum SaveType
{
    /// <summary>A memory card image. Exactly one memory card is stored per archive.</summary>
    MemoryCard,

    /// <summary>An emulator save state. One save state per archive, but the archive may contain multiple files.</summary>
    SaveState,

    /// <summary>A save data folder (e.g. PS3/PS4/PS5). Zips a directory of files, preserving structure.</summary>
    SaveFolder,
}
