using System.Text.RegularExpressions;

namespace SaveHub.Core.Archiving;

/// <summary>
/// Detects which PlayStation console a <em>folder</em> save (PS3/PS4/PS5/PSP/Vita) belongs to.
/// These consoles all share the <c>PARAM.SFO</c> format, so the console can't be told apart from
/// the file structure alone — it is inferred from the title-id prefix found in the folder name
/// (e.g. <c>BLUS30109</c>) or the <c>PARAM.SFO</c>.
/// </summary>
public static partial class PlaystationDetector
{
    [GeneratedRegex("[A-Z]{4}[0-9]{5}", RegexOptions.IgnoreCase)]
    private static partial Regex TitleIdPattern();

    /// <summary>Extracts a PlayStation title id (e.g. BLUS30109) from a save folder's name.</summary>
    public static string? TitleIdFromFolderNames(IReadOnlyList<string> files)
    {
        if (files is null)
        {
            return null;
        }
        foreach (string path in files)
        {
            string? directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                continue;
            }
            Match match = TitleIdPattern().Match(Path.GetFileName(directory));
            if (match.Success)
            {
                return match.Value.ToUpperInvariant();
            }
        }
        return null;
    }

    /// <summary>
    /// Detects the PlayStation console for a folder save from its PARAM.SFO title id or the
    /// folder name. Returns null when it can't be determined (e.g. a non-PlayStation folder).
    /// </summary>
    public static string? DetectFolderPlatform(IReadOnlyList<string> files)
    {
        if (files is null || files.Count == 0)
        {
            return null;
        }
        string? titleId = ParamSfoReader.TitleIdFromFiles(files) ?? TitleIdFromFolderNames(files);
        return PlatformForTitleId(titleId);
    }

    /// <summary>Maps a PlayStation title-id prefix to a console folder code, or null if unknown.</summary>
    public static string? PlatformForTitleId(string? titleId)
    {
        if (string.IsNullOrWhiteSpace(titleId) || titleId.Trim().Length < 4)
        {
            return null;
        }
        string id = titleId.Trim().ToUpperInvariant();
        string prefix = id[..4];

        if (prefix == "CUSA")
        {
            return "PS4";
        }
        if (prefix == "PPSA")
        {
            return "PS5";
        }
        if (prefix is "PCSA" or "PCSB" or "PCSC" or "PCSD" or "PCSE" or "PCSF" or "PCSG" or "PCSH"
            or "VCJS" or "VLAS" or "VLJS" or "VLJM")
        {
            return "PSV";
        }
        if (prefix is "ULUS" or "ULES" or "ULJS" or "ULJM" or "ULKS" or "UCUS" or "UCES" or "UCJS"
            or "UCKS" or "NPUG" or "NPEG" or "NPHG" or "NPJG" or "NPUZ" or "NPEZ" or "NPJZ")
        {
            return "PSP";
        }
        // Remaining 4-letter + 5-digit ids are PS3 (BLUS/BLES/BCUS/NPUB/NPEB/NPUA/NPEA/...).
        return TitleIdPattern().IsMatch(id) ? "PS3" : null;
    }
}
