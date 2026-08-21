using System.Text;
using System.Text.RegularExpressions;

namespace SaveHub.Core.Archiving;

/// <summary>
/// Reads metadata from PlayStation memory-card images: which console the card is for (PS1 vs PS2)
/// and the human-readable game/save title stored on the card (full-width Shift-JIS).
/// </summary>
public static partial class MemoryCardReader
{
    private static readonly Encoding ShiftJis;
    private static readonly byte[] Ps2Magic = Encoding.ASCII.GetBytes("Sony PS2 Memory Card Format");
    private static readonly byte[] GmeMagic = Encoding.ASCII.GetBytes("123-456-STD");
    private static readonly byte[] VgsMagic = Encoding.ASCII.GetBytes("VgsM");

    static MemoryCardReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ShiftJis = Encoding.GetEncoding(932);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>Detects the console a memory-card image belongs to: "PS1", "PS2", or null.</summary>
    public static string? DetectPlatform(byte[] data)
    {
        if (data is null || data.Length < 4)
        {
            return null;
        }
        // PS2 raw image: the format magic sits at the very start.
        if (StartsWith(data, 0, Ps2Magic))
        {
            return "PS2";
        }
        // DexDrive (.gme) and Connectix VGS (.vgs) wrap a PS1 card behind a fixed header.
        if (StartsWith(data, 0, GmeMagic) || StartsWith(data, 0, VgsMagic))
        {
            return "PS1";
        }
        // PSP/PS3 virtual card (.vmp/.mcs): 128-byte header, then a raw PS1 card ("MC").
        if (data.Length >= 0x82 && data[0x80] == (byte)'M' && data[0x81] == (byte)'C')
        {
            return "PS1";
        }
        // Raw PS1 card: "MC" magic; standard image is 128 KB (larger multi-block images exist).
        if (data.Length >= 131072 && data[0] == (byte)'M' && data[1] == (byte)'C')
        {
            return "PS1";
        }
        // Some tools prepend a short header to a PS2 image; scan the first block for the magic.
        if (IndexOf(data, Ps2Magic, 1024) >= 0)
        {
            return "PS2";
        }
        return null;
    }

    private static bool StartsWith(byte[] data, int offset, byte[] pattern)
    {
        if (offset < 0 || offset + pattern.Length > data.Length)
        {
            return false;
        }
        for (int i = 0; i < pattern.Length; i++)
        {
            if (data[offset + i] != pattern[i])
            {
                return false;
            }
        }
        return true;
    }

    private static int IndexOf(byte[] data, byte[] pattern, int limit)
    {
        int end = Math.Min(limit, data.Length - pattern.Length);
        for (int i = 0; i <= end; i++)
        {
            if (StartsWith(data, i, pattern))
            {
                return i;
            }
        }
        return -1;
    }

    public static string? DetectPlatformFromFile(string path)
    {
        return File.Exists(path) ? DetectPlatform(File.ReadAllBytes(path)) : null;
    }

    /// <summary>
    /// Reads the game/save title from a card. For PS2 this is the clean game name (from
    /// <c>icon.sys</c>); for PS1 it is the save's title frame (which may include progress text).
    /// </summary>
    public static string? ReadGameName(string? platform, byte[] data)
    {
        if (data is null)
        {
            return null;
        }
        string? console = string.IsNullOrWhiteSpace(platform) ? DetectPlatform(data) : platform.Trim().ToUpperInvariant();
        return console switch
        {
            "PS2" => ReadPs2Title(data),
            "PS1" or "PSX" => ReadPs1Title(data),
            _ => ReadPs2Title(data) ?? ReadPs1Title(data),
        };
    }

    public static string? ReadGameNameFromFile(string? platform, string path)
    {
        return File.Exists(path) ? ReadGameName(platform, File.ReadAllBytes(path)) : null;
    }

    // PS2: icon.sys begins with "PS2D"; the 68-byte title is at offset 0xC0.
    private static string? ReadPs2Title(byte[] data)
    {
        for (int i = 0; i + 0xC0 + 68 <= data.Length; i++)
        {
            if (data[i] == (byte)'P' && data[i + 1] == (byte)'S' && data[i + 2] == (byte)'2' && data[i + 3] == (byte)'D')
            {
                string title = Normalize(ShiftJis.GetString(data, i + 0xC0, 68));
                if (title.Length > 0)
                {
                    return title;
                }
            }
        }
        return null;
    }

    // PS1: save data blocks start at 0x2000 (step 0x2000); the title frame magic is "SC", title at +0x04.
    private static string? ReadPs1Title(byte[] data)
    {
        for (int i = 0x2000; i + 4 + 64 <= data.Length; i += 0x2000)
        {
            if (data[i] == (byte)'S' && data[i + 1] == (byte)'C')
            {
                string title = Normalize(ShiftJis.GetString(data, i + 4, 64));
                if (title.Length > 0)
                {
                    return title;
                }
            }
        }
        return null;
    }

    private static string Normalize(string raw)
    {
        StringBuilder builder = new StringBuilder(raw.Length);
        foreach (char ch in raw)
        {
            int code = ch;
            if (code == 0)
            {
                break; // null terminator
            }
            if (code >= 0xFF01 && code <= 0xFF5E)
            {
                builder.Append((char)(code - 0xFEE0)); // full-width -> half-width
            }
            else if (code == 0x3000)
            {
                builder.Append(' '); // full-width space
            }
            else if (code >= 0x20)
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append(' ');
            }
        }
        return Whitespace().Replace(builder.ToString(), " ").Trim();
    }
}
