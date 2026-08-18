using System.Text;
using SaveHub.Core.Models;

namespace SaveHub.Core.Archiving;

/// <summary>
/// Renders the human-readable manifest that is written both inside the archive
/// (as <see cref="SaveNaming.ManifestFileName"/>) and as an external side-car <c>.txt</c> file.
/// </summary>
public static class SaveManifest
{
    /// <summary>
    /// URL shown in manifests so downloaders know where the data came from. Override if you fork.
    /// </summary>
    public static string ProjectUrl { get; set; } = "https://github.com/uncle-uee/SaveHub";

    public static string Render(SaveUploadRequest request, int index, DateTimeOffset createdUtc)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("SaveHub Save");
        builder.AppendLine("============");
        builder.AppendLine();
        builder.AppendLine($"Platform:     {request.Platform}");
        builder.AppendLine($"Game ID:      {request.GameId}");
        if (!string.IsNullOrWhiteSpace(request.GameTitle))
        {
            builder.AppendLine($"Game Title:   {request.GameTitle}");
        }
        builder.AppendLine($"Type:         {SaveNaming.Label(request.SaveType)}");
        builder.AppendLine($"Archive:      {SaveNaming.ArchiveName(index, request.SaveType)}");
        if (!string.IsNullOrWhiteSpace(request.Emulator))
        {
            builder.AppendLine($"Emulator:     {request.Emulator}");
        }
        builder.AppendLine($"Created:      {createdUtc.UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine();
        builder.AppendLine("Description:");
        builder.AppendLine($"  {request.Description}");

        string?[] fileNames = request.Files.Select(Path.GetFileName).Where(f => f is not null).ToArray();
        if (fileNames.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Files:");
            foreach (string? name in fileNames)
            {
                builder.AppendLine($"  - {name}");
            }
        }

        if (request.SaveType == SaveType.SaveState)
        {
            builder.AppendLine();
            builder.AppendLine("Note:");
            builder.AppendLine("  Save states are usually emulator-specific and are often NOT interchangeable");
            builder.AppendLine("  between different emulators (for example mGBA vs VBA-M, or different core");
            builder.AppendLine("  versions). Use the emulator named above for best results.");
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            builder.AppendLine();
            builder.AppendLine("Notes:");
            builder.AppendLine($"  {request.Notes}");
        }

        builder.AppendLine();
        builder.AppendLine($"Created with SaveHub - {ProjectUrl}");
        return builder.ToString();
    }
}
