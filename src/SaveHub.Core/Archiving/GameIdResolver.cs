using SaveHub.Core.Models;

namespace SaveHub.Core.Archiving;

/// <summary>
/// Determines the game-id folder name for an upload, in priority order:
/// <list type="number">
/// <item>an explicit title id (user supplied);</item>
/// <item>a serial read from a PS1/PS2 memory card;</item>
/// <item>a title id read from a PS3/PS4/PS5/PSP/Vita <c>PARAM.SFO</c>;</item>
/// <item>a game name (user supplied) — used for Nintendo and anything without an id;</item>
/// <item>a game name read from the save (<c>PARAM.SFO</c> TITLE);</item>
/// <item>the <see cref="UnknownGame"/> folder as a last resort.</item>
/// </list>
/// </summary>
public static class GameIdResolver
{
    /// <summary>Folder used when neither a title id nor a game name can be determined.</summary>
    public const string UnknownGame = "Unknown";

    /// <summary>
    /// Attempts to read only a machine title id (PS serial or PARAM.SFO). Returns null when the
    /// platform/save doesn't carry one (e.g. Nintendo). Used by "detect title id" buttons.
    /// </summary>
    public static string? DetectTitleId(string platform, SaveType saveType, IReadOnlyList<string> files)
    {
        if (files is null || files.Count == 0)
        {
            return null;
        }

        string platformCode = Normalize(platform);

        if (saveType == SaveType.MemoryCard && platformCode is "PS1" or "PSX" or "PS2")
        {
            string? serial = PsSerialScanner.ScanFile(files[0]);
            if (!string.IsNullOrWhiteSpace(serial))
            {
                return serial;
            }
        }

        if (platformCode is "PS3" or "PS4" or "PS5" or "PSP" or "PSV" or "VITA")
        {
            string? titleId = ParamSfoReader.TitleIdFromFiles(files);
            if (!string.IsNullOrWhiteSpace(titleId))
            {
                return titleId;
            }

            // PS3/PS4/PS5 save folders are named with the title id (e.g. BLUS30109, CUSA01234).
            string? fromFolder = PlaystationDetector.TitleIdFromFolderNames(files);
            if (!string.IsNullOrWhiteSpace(fromFolder))
            {
                return fromFolder;
            }
        }

        return null;
    }

    /// <summary>Resolves the folder game id for an upload following the documented priority.</summary>
    public static GameIdResolution Resolve(
        string platform,
        SaveType saveType,
        IReadOnlyList<string> files,
        string? explicitTitleId,
        string? gameName = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitTitleId))
        {
            return new GameIdResolution(SaveNaming.Sanitize(explicitTitleId), "provided");
        }

        string? detected = DetectTitleId(platform, saveType, files);
        if (!string.IsNullOrWhiteSpace(detected))
        {
            string source = saveType == SaveType.MemoryCard && Normalize(platform) is "PS1" or "PSX" or "PS2"
                ? "memory card"
                : "PARAM.SFO";
            return new GameIdResolution(SaveNaming.Sanitize(detected), source);
        }

        if (!string.IsNullOrWhiteSpace(gameName))
        {
            return new GameIdResolution(SaveNaming.Sanitize(gameName), "game name");
        }

        string? savedName = files is { Count: > 0 } ? ParamSfoReader.GameNameFromFiles(files) : null;
        if (!string.IsNullOrWhiteSpace(savedName))
        {
            return new GameIdResolution(SaveNaming.Sanitize(savedName), "save title");
        }

        return new GameIdResolution(UnknownGame, "unknown");
    }

    private static string Normalize(string? platform)
    {
        return (platform ?? string.Empty).Trim().ToUpperInvariant();
    }
}
