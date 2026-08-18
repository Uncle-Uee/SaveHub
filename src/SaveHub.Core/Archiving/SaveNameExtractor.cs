namespace SaveHub.Core.Archiving;

using SaveHub.Core.Models;

/// <summary>
/// Best-effort extraction of a game display name from the save files: a PARAM.SFO <c>TITLE</c>
/// (PS3/PS4/PS5/PSP/Vita) first, then a PS1/PS2 memory-card title. Nintendo saves (GBA/NDS/... )
/// are raw backup memory with no embedded title, so the save file name is used instead — this
/// keeps the stored name 1:1 with the file the emulator expects.
/// </summary>
public static class SaveNameExtractor
{
    public static string? Read(string platform, IReadOnlyList<string> files)
    {
        if (files is null || files.Count == 0)
        {
            return null;
        }

        string? fromSfo = ParamSfoReader.GameNameFromFiles(files);
        if (!string.IsNullOrWhiteSpace(fromSfo))
        {
            return fromSfo;
        }

        string? fromCard = MemoryCardReader.ReadGameNameFromFile(platform, files[0]);
        if (!string.IsNullOrWhiteSpace(fromCard))
        {
            return fromCard;
        }

        // Nintendo .sav/.dsv files carry no title, so fall back to the save file name.
        if (KnownPlatforms.IsNintendo(platform))
        {
            string? name = Path.GetFileNameWithoutExtension(files[0]);
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        return null;
    }
}
