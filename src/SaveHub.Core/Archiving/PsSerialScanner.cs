using System.Text;
using System.Text.RegularExpressions;

namespace SaveHub.Core.Archiving;

/// <summary>
/// Extracts a PlayStation game serial (title id) from a memory-card image by scanning for the
/// serial strings the console stores in save directory/product-code entries (e.g. a PS2 card holds
/// folders like <c>BASCUS-97199...</c>, a PS1 card holds product codes like <c>BASLUS-00190...</c>).
/// </summary>
public static partial class PsSerialScanner
{
    [GeneratedRegex(
        "(SLUS|SCUS|SLES|SCES|SLPS|SLPM|SCPS|SCAJ|SCKA|SLKA|SLAJ|SLED|SCED|PBPX|PAPX|PCPX)[-_ ]?([0-9]{5})",
        RegexOptions.CultureInvariant)]
    private static partial Regex SerialRegex();

    /// <summary>Scans raw card bytes and returns the most common serial as "PREFIX-NNNNN", or null.</summary>
    public static string? Scan(byte[] data)
    {
        if (data is null || data.Length == 0)
        {
            return null;
        }

        // Card data is ASCII for these fields; ISO-8859-1 maps every byte 1:1 so the scan is safe.
        string text = Encoding.Latin1.GetString(data);
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in SerialRegex().Matches(text))
        {
            string serial = $"{match.Groups[1].Value.ToUpperInvariant()}-{match.Groups[2].Value}";
            counts[serial] = counts.TryGetValue(serial, out int c) ? c + 1 : 1;
        }

        return counts.Count == 0
            ? null
            : counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).First().Key;
    }

    /// <summary>Reads a card file and returns its detected serial, or null when none is found.</summary>
    public static string? ScanFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }
        return Scan(File.ReadAllBytes(path));
    }
}
