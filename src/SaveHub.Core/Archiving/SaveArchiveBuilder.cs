using System.IO.Compression;
using System.Text;
using SaveHub.Core.Models;

namespace SaveHub.Core.Archiving;

/// <summary>
/// Builds the zip archive (and its side-car text file) for a save, following the SaveHub layout.
/// The archive always contains the raw save file(s) plus a <see cref="SaveNaming.ManifestFileName"/>
/// describing what the save is for.
/// </summary>
public static class SaveArchiveBuilder
{
    /// <summary>
    /// Builds all artifacts for a save at the given incremental <paramref name="index"/>.
    /// </summary>
    /// <param name="request">The upload request describing the save and its files.</param>
    /// <param name="index">The incremental index that determines the archive name (e.g. 1 => "01.zip").</param>
    /// <param name="icon">Optional resolved icon/cover-art content to store in the game folder.</param>
    /// <param name="createdUtc">Timestamp recorded in the manifest. Defaults to now.</param>
    public static PreparedSave Build(
        SaveUploadRequest request,
        int index,
        CoverArt? icon = null,
        bool iconIsExplicit = false,
        DateTimeOffset? createdUtc = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Files is null || request.Files.Count == 0)
        {
            throw new ArgumentException("At least one save file is required.", nameof(request));
        }

        if (request.SaveType == SaveType.MemoryCard && request.Files.Count != 1)
        {
            throw new ArgumentException(
                "A memory card upload must contain exactly one file. Use SaveType.SaveState for multi-file saves.",
                nameof(request));
        }

        foreach (string file in request.Files)
        {
            if (!File.Exists(file))
            {
                throw new FileNotFoundException("Save file not found.", file);
            }
        }

        DateTimeOffset timestamp = createdUtc ?? DateTimeOffset.UtcNow;
        string manifest = SaveManifest.Render(request, index, timestamp);
        string folder = SaveNaming.GameFolder(request.Platform, request.GameId);
        string archiveName = SaveNaming.ArchiveName(index, request.SaveType);

        byte[] zipBytes = BuildZip(request.Files, manifest, request.RootDirectory);

        PreparedSave prepared = new PreparedSave
        {
            Platform = request.Platform,
            GameId = request.GameId,
            SaveType = request.SaveType,
            Index = index,
            Description = request.Description,
            GameTitle = request.GameTitle,
            GameFolder = folder,
            Archive = new StorageFile($"{folder}/{archiveName}", zipBytes),
            Icon = BuildIcon(icon, folder),
            IconIsExplicit = iconIsExplicit && icon is not null,
        };

        return prepared;
    }

    private static byte[] BuildZip(IReadOnlyList<string> files, string manifest, string? rootDirectory)
    {
        string? root = string.IsNullOrWhiteSpace(rootDirectory) ? null : Path.GetFullPath(rootDirectory);
        using MemoryStream memory = new MemoryStream();
        using (ZipArchive archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in files)
            {
                string entryName = UniqueEntryName(EntryNameFor(path, root), usedNames);
                ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                using Stream entryStream = entry.Open();
                using FileStream source = File.OpenRead(path);
                source.CopyTo(entryStream);
            }

            ZipArchiveEntry manifestEntry = archive.CreateEntry(SaveNaming.ManifestFileName, CompressionLevel.Optimal);
            using StreamWriter writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8);
            writer.Write(manifest);
        }

        return memory.ToArray();
    }

    // Preserves folder structure (relative to root) for folder uploads; otherwise uses the file name.
    private static string EntryNameFor(string path, string? root)
    {
        if (root is not null)
        {
            string full = Path.GetFullPath(path);
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                string relative = Path.GetRelativePath(root, full).Replace('\\', '/');
                if (!string.IsNullOrWhiteSpace(relative) && relative != ".")
                {
                    return relative;
                }
            }
        }
        return Path.GetFileName(path);
    }

    private static StorageFile? BuildIcon(CoverArt? icon, string folder)
    {
        if (icon is not { } cover || cover.Content.Length == 0)
        {
            return null;
        }

        string extension = string.IsNullOrWhiteSpace(cover.Extension) ? ".png" : cover.Extension.ToLowerInvariant();
        return new StorageFile($"{folder}/icon{extension}", cover.Content);
    }

    private static string UniqueEntryName(string name, HashSet<string> used)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "save.bin";
        }

        if (used.Add(name))
        {
            return name;
        }

        string stem = Path.GetFileNameWithoutExtension(name);
        string ext = Path.GetExtension(name);
        for (int i = 1; ; i++)
        {
            string candidate = $"{stem}_{i}{ext}";
            if (used.Add(candidate))
            {
                return candidate;
            }
        }
    }
}
