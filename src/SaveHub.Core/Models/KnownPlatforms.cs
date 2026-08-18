namespace SaveHub.Core.Models;

/// <summary>
/// Well-known platform folder names. Platforms are plain strings in the API, so callers may use
/// values outside this list; these constants exist for convenience and discoverability.
/// </summary>
public static class KnownPlatforms
{
    public const string Ps1 = "PS1";
    public const string Ps2 = "PS2";
    public const string Ps3 = "PS3";
    public const string Ps4 = "PS4";
    public const string Psp = "PSP";
    public const string PsVita = "PSV";
    public const string Gb = "GB";
    public const string Gbc = "GBC";
    public const string Gba = "GBA";
    public const string Nds = "DS";
    public const string N3ds = "3DS";
    public const string Nes = "NES";
    public const string Snes = "SNES";
    public const string N64 = "N64";
    public const string GameCube = "GC";
    public const string Wii = "WII";
    public const string Switch = "SWITCH";
    public const string Genesis = "GENESIS";
    public const string Dreamcast = "DREAMCAST";
    public const string VirtualBoy = "VB";

    /// <summary>All well-known platform identifiers.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Ps1, Ps2, Ps3, Ps4, Psp, PsVita,
        Gb, Gbc, Gba, Nds, N3ds, VirtualBoy,
        Nes, Snes, N64, GameCube, Wii, Switch,
        Genesis, Dreamcast,
    ];

    private static readonly HashSet<string> NintendoPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        Gb, Gbc, Gba, Nds, N3ds, VirtualBoy, Nes, Snes, N64, GameCube, Wii, Switch,
    };

    /// <summary>Whether the platform is a Nintendo console/handheld (uses the file name as the folder).</summary>
    public static bool IsNintendo(string platform)
    {
        return !string.IsNullOrWhiteSpace(platform) && NintendoPlatforms.Contains(platform.Trim());
    }
}

