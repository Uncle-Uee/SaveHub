using System.Text;
using SaveHub.Core.Models;

namespace SaveHub.Core.Archiving;

/// <summary>
/// Central rules for how saves are named and laid out inside a repository.
/// Layout: <c>PLATFORM/GAMEID/{index}.zip</c> (memory card) or
/// <c>PLATFORM/GAMEID/{index}-sstate.zip</c> (save state), with a matching side-car
/// <c>.txt</c> file and an aggregate <c>saves.txt</c> per game folder.
/// </summary>
public static class SaveNaming
{
    /// <summary>Suffix that distinguishes save-state archives from memory-card archives.</summary>
    public const string SaveStateSuffix = "-sstate";

    /// <summary>Suffix that distinguishes save-folder archives (e.g. PS3/PS4 saves).</summary>
    public const string SaveFolderSuffix = "-folder";

    /// <summary>Number of digits the incremental index is padded to (e.g. 1 => "01").</summary>
    public const int IndexPadding = 2;

    /// <summary>File name used for the manifest stored inside every archive.</summary>
    public const string ManifestFileName = "README.txt";

    /// <summary>Normalizes a platform or game id into a safe folder name.</summary>
    public static string Sanitize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char ch in value.Trim())
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-');
        }
        return builder.ToString();
    }

    /// <summary>The base name (without extension) for a save archive, e.g. "01", "01-sstate", "01-folder".</summary>
    public static string BaseName(int index, SaveType saveType)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be non-negative.");
        }
        string padded = index.ToString().PadLeft(IndexPadding, '0');
        return saveType switch
        {
            SaveType.SaveState => padded + SaveStateSuffix,
            SaveType.SaveFolder => padded + SaveFolderSuffix,
            _ => padded,
        };
    }

    /// <summary>The archive file name, e.g. "01.zip" or "01-sstate.zip".</summary>
    public static string ArchiveName(int index, SaveType saveType)
    {
        return BaseName(index, saveType) + ".zip";
    }

    /// <summary>Human-readable label for a save type.</summary>
    public static string Label(SaveType saveType)
    {
        return saveType switch
        {
            SaveType.MemoryCard => "Memory Card",
            SaveType.SaveState => "Save State",
            SaveType.SaveFolder => "Save Folder",
            _ => saveType.ToString(),
        };
    }

    /// <summary>The folder path within the repository for a game, e.g. "PS2/SLUS-21274".</summary>
    public static string GameFolder(string platform, string gameId)
    {
        return $"{Sanitize(platform)}/{Sanitize(gameId)}";
    }

    /// <summary>Attempts to parse an archive file name into its index and save type.</summary>
    public static bool TryParseArchiveName(string archiveName, out int index, out SaveType saveType)
    {
        index = 0;
        saveType = SaveType.MemoryCard;
        if (string.IsNullOrWhiteSpace(archiveName) ||
            !archiveName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string stem = archiveName[..^4];
        if (stem.EndsWith(SaveStateSuffix, StringComparison.OrdinalIgnoreCase))
        {
            saveType = SaveType.SaveState;
            stem = stem[..^SaveStateSuffix.Length];
        }
        else if (stem.EndsWith(SaveFolderSuffix, StringComparison.OrdinalIgnoreCase))
        {
            saveType = SaveType.SaveFolder;
            stem = stem[..^SaveFolderSuffix.Length];
        }

        return int.TryParse(stem, out index);
    }
}
