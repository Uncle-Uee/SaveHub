namespace SaveHub.Core.Archiving;

/// <summary>
/// Maps a platform + game serial to a public cover-art URL. Serials are used verbatim, so callers
/// must pass the serial in the form each source expects (PS1/PS2 use "SLUS-12345"; PS3 uses the
/// title id "BCES00081"; PSP uses "NPEG00001" with no dash).
/// </summary>
public static class CoverArtSource
{
    /// <summary>Returns the cover URL and file extension for a platform/serial, or null if unsupported.</summary>
    public static (string Url, string Extension)? Resolve(string platform, string serial)
    {
        if (string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(serial))
        {
            return null;
        }

        string trimmedSerial = serial.Trim();
        return platform.Trim().ToUpperInvariant() switch
        {
            "PS1" or "PSX" => ($"https://raw.githubusercontent.com/xlenore/psx-covers/main/covers/default/{trimmedSerial}.jpg", ".jpg"),
            "PS2" => ($"https://raw.githubusercontent.com/xlenore/ps2-covers/main/covers/default/{trimmedSerial}.jpg", ".jpg"),
            "PS3" => ($"https://art.gametdb.com/ps3/coverHQ/EN/{trimmedSerial}.jpg", ".jpg"),
            "PSP" => ($"https://raw.githubusercontent.com/Andiweli/HexFlow-Covers/main/Covers/PSP/{trimmedSerial}.png", ".png"),
            _ => null,
        };
    }
}
