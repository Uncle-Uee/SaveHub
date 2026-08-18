using System.Text;

namespace SaveHub.Core.Archiving;

/// <summary>
/// Minimal reader for the PlayStation <c>PARAM.SFO</c> metadata format used by PS3/PS4/PS5/PSP/Vita
/// save data. It extracts key/value pairs and, in particular, the game's title id.
/// Format reference: 20-byte header (magic <c>\0PSF</c>), an index table, a key table, and a data table.
/// </summary>
public static class ParamSfoReader
{
    private const string StandardFileName = "PARAM.SFO";
    private static readonly byte[] Magic = [0x00, 0x50, 0x53, 0x46]; // "\0PSF"

    /// <summary>Keys checked, in order, when resolving a title id.</summary>
    private static readonly string[] TitleIdKeys = ["TITLE_ID", "DISC_ID"];

    /// <summary>Keys checked, in order, when resolving a game name.</summary>
    private static readonly string[] GameNameKeys = ["TITLE"];

    /// <summary>Whether the bytes start with a valid PARAM.SFO header.</summary>
    public static bool LooksLikeParamSfo(ReadOnlySpan<byte> data)
    {
        return data.Length >= 20 && data[..4].SequenceEqual(Magic);
    }

    /// <summary>Parses all string/integer entries into a dictionary. Returns empty on malformed input.</summary>
    public static IReadOnlyDictionary<string, string> Read(byte[] data)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (data is null || !LooksLikeParamSfo(data))
        {
            return result;
        }

        try
        {
            uint keyTableStart = BitConverter.ToUInt32(data, 0x08);
            uint dataTableStart = BitConverter.ToUInt32(data, 0x0C);
            uint entries = BitConverter.ToUInt32(data, 0x10);

            for (uint i = 0u; i < entries; i++)
            {
                int entry = 0x14 + (int)i * 16;
                if (entry + 16 > data.Length)
                {
                    break;
                }

                ushort keyOffset = BitConverter.ToUInt16(data, entry);
                ushort dataFmt = BitConverter.ToUInt16(data, entry + 0x02);
                uint dataLen = BitConverter.ToUInt32(data, entry + 0x04);
                uint dataOffset = BitConverter.ToUInt32(data, entry + 0x0C);

                string? key = ReadNullTerminated(data, (int)(keyTableStart + keyOffset));
                if (key is null)
                {
                    continue;
                }

                int valueStart = (int)(dataTableStart + dataOffset);
                if (valueStart < 0 || valueStart > data.Length)
                {
                    continue;
                }

                string value;
                if (dataFmt == 0x0404) // uint32
                {
                    if (valueStart + 4 > data.Length)
                    {
                        continue;
                    }
                    value = BitConverter.ToUInt32(data, valueStart).ToString();
                }
                else // 0x0004 / 0x0204 UTF-8 string
                {
                    int end = Math.Min(valueStart + (int)dataLen, data.Length);
                    value = Encoding.UTF8.GetString(data, valueStart, end - valueStart).TrimEnd('\0').Trim();
                }

                result[key] = value;
            }
        }
        catch
        {
            return result;
        }

        return result;
    }

    /// <summary>Returns the title id (TITLE_ID or DISC_ID), or null when not present.</summary>
    public static string? ReadTitleId(byte[] data)
    {
        return ReadFirst(data, TitleIdKeys);
    }

    /// <summary>Returns the game name (TITLE), or null when not present.</summary>
    public static string? ReadGameName(byte[] data)
    {
        return ReadFirst(data, GameNameKeys);
    }

    private static string? ReadFirst(byte[] data, string[] keys)
    {
        IReadOnlyDictionary<string, string> map = Read(data);
        foreach (string key in keys)
        {
            if (map.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return null;
    }

    /// <summary>Finds a PARAM.SFO among the files and returns its title id (TITLE_ID/DISC_ID).</summary>
    public static string? TitleIdFromFiles(IReadOnlyList<string> files)
    {
        return FromFiles(files, ReadTitleId);
    }

    /// <summary>Finds a PARAM.SFO among the files and returns its game name (TITLE).</summary>
    public static string? GameNameFromFiles(IReadOnlyList<string> files)
    {
        return FromFiles(files, ReadGameName);
    }

    private static string? FromFiles(IReadOnlyList<string> files, Func<byte[], string?> reader)
    {
        if (files is null)
        {
            return null;
        }

        // Prefer a file literally named PARAM.SFO, then sniff any file with the PSF magic.
        foreach (string path in files)
        {
            if (File.Exists(path) &&
                string.Equals(Path.GetFileName(path), StandardFileName, StringComparison.OrdinalIgnoreCase))
            {
                string? value = reader(File.ReadAllBytes(path));
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        foreach (string path in files)
        {
            if (!File.Exists(path) || !StartsWithMagic(path))
            {
                continue;
            }
            string? value = reader(File.ReadAllBytes(path));
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool StartsWithMagic(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            Span<byte> head = stackalloc byte[4];
            return stream.Read(head) == 4 && head.SequenceEqual(Magic);
        }
        catch
        {
            return false;
        }
    }

    private static string? ReadNullTerminated(byte[] data, int start)
    {
        if (start < 0 || start >= data.Length)
        {
            return null;
        }
        int end = start;
        while (end < data.Length && data[end] != 0)
        {
            end++;
        }
        return Encoding.ASCII.GetString(data, start, end - start);
    }
}
