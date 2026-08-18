using System.ComponentModel;
using SaveHub.Core;
using SaveHub.Core.Abstractions;
using SaveHub.Core.Archiving;
using SaveHub.Core.Configuration;
using SaveHub.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SaveHub.Cli.Commands;

/// <summary>Uploads a memory card or save state to the configured storage provider.</summary>
internal sealed class UploadCommand : AsyncCommand<UploadCommand.Settings>
{
    internal sealed class Settings : GlobalSettings
    {
        [CommandOption("-p|--platform <PLATFORM>")]
        [Description("Platform folder, e.g. PS2, GBA, DS.")]
        public string? Platform { get; init; }

        [CommandOption("-g|--game|--titleid <TITLEID>")]
        [Description("Title id / serial (folder name). Optional: auto-detected for PS1/PS2 cards and PS3+ PARAM.SFO.")]
        public string? GameId { get; init; }

        [CommandOption("-t|--type <TYPE>")]
        [Description("Save type: mc (memory card) or state (save state).")]
        public string? Type { get; init; }

        [CommandOption("-f|--file <PATH>")]
        [Description("Save file(s). Repeat for multiple files (save states, GBA saves).")]
        public string[] Files { get; init; } = [];

        [CommandOption("-d|--description <TEXT>")]
        [Description("Short description, e.g. \"100% completion\".")]
        public string? Description { get; init; }

        [CommandOption("--title|--name <TEXT>")]
        [Description("Game name. Used as the folder for Nintendo saves and when no title id is found.")]
        public string? GameTitle { get; init; }

        [CommandOption("-e|--emulator <NAME>")]
        [Description("Emulator name (recommended for save states).")]
        public string? Emulator { get; init; }

        [CommandOption("-i|--icon <PATH>")]
        [Description("Optional cover art / icon to store with the save (overrides auto-download).")]
        public string? IconPath { get; init; }

        [CommandOption("--no-cover-art")]
        [Description("Do not auto-download cover art when no --icon is provided.")]
        public bool NoCoverArt { get; init; }

        [CommandOption("--notes <TEXT>")]
        [Description("Optional extra notes (e.g. compatibility warnings).")]
        public string? Notes { get; init; }

        [CommandOption("--auto-merge")]
        [Description("Force auto-merge for this upload (needs write access and config autoMerge).")]
        public bool? AutoMerge { get; init; }

        [CommandOption("--no-auto-merge")]
        [Description("Open a pull request for review even when config autoMerge is enabled.")]
        public bool NoAutoMerge { get; init; }

        [CommandOption("--index <N>")]
        [Description("Replace the save at this index (e.g. 1 to overwrite 01.zip) instead of appending.")]
        public int? Index { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        SaveHubConfigStore store = CliContext.ResolveStore(settings.ConfigPath);
        if (!store.Exists)
        {
            AnsiConsole.MarkupLine("[red]No configuration found.[/] Run [bold]savehub config github[/] first.");
            return 1;
        }

        SaveHubConfig config = store.Load();
        SaveHubClient client = await CliContext.CreateClientAsync(config, cancellationToken);

        string platform = Ask("Platform", settings.Platform, KnownPlatforms.All);
        SaveType saveType = ParseType(settings.Type);
        List<string> files = ResolveFiles(settings.Files, saveType);
        if (files.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No valid files provided.[/]");
            return 1;
        }

        GameIdResolution resolution = GameIdResolver.Resolve(platform, saveType, files, settings.GameId, settings.GameTitle);
        string gameId = resolution.GameId;
        if (string.IsNullOrWhiteSpace(settings.GameId))
        {
            string note = resolution.Resolved
                ? $"[grey]Game id from {resolution.Source}:[/] {Markup.Escape(gameId)}"
                : $"[yellow]No title id or name found — using the '{gameId}' folder.[/]";
            AnsiConsole.MarkupLine(note);
        }

        string? gameTitle = settings.GameTitle;
        if (string.IsNullOrWhiteSpace(gameTitle) && SaveNameExtractor.Read(platform, files) is { } detectedName)
        {
            gameTitle = detectedName;
            AnsiConsole.MarkupLine($"[grey]Game name from save:[/] {Markup.Escape(detectedName)}");
        }

        string description = Ask("Description", settings.Description);
        string? emulator = settings.Emulator;
        if (saveType == SaveType.SaveState && string.IsNullOrWhiteSpace(emulator))
        {
            emulator = AnsiConsole.Prompt(new TextPrompt<string>("Emulator (recommended for save states):").AllowEmpty());
        }

        SaveUploadRequest request = new SaveUploadRequest
        {
            Platform = platform,
            GameId = gameId,
            SaveType = saveType,
            Files = files,
            Description = description,
            GameTitle = gameTitle,
            Emulator = string.IsNullOrWhiteSpace(emulator) ? null : emulator,
            IconPath = settings.IconPath,
            AutoFetchCoverArt = !settings.NoCoverArt,
            Notes = settings.Notes,
        };

        SaveUploadResult result = null!;
        bool? autoMerge = settings.NoAutoMerge ? false : settings.AutoMerge;
        await AnsiConsole.Status().StartAsync("Building archive and uploading...", async _ =>
        {
            result = await client.UploadAsync(request, new UploadOptions { AutoMerge = autoMerge, TargetIndex = settings.Index });
        });

        if (result.Merged)
        {
            AnsiConsole.MarkupLine($"[green]Merged:[/] {result.ArchivePath}");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Success.[/] {Markup.Escape(result.Message)}");
        }
        if (!string.IsNullOrWhiteSpace(result.PullRequestUrl))
        {
            AnsiConsole.MarkupLine($"[blue]{result.PullRequestUrl}[/]");
        }
        return 0;
    }

    private static SaveType ParseType(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim().ToLowerInvariant() switch
            {
                "mc" or "card" or "memorycard" or "memory" => SaveType.MemoryCard,
                "state" or "savestate" or "sstate" => SaveType.SaveState,
                "folder" or "savefolder" or "dir" => SaveType.SaveFolder,
                _ => throw new ArgumentException($"Unknown save type '{value}'. Use 'mc', 'state', or 'folder'."),
            };
        }

        string choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Save type?")
            .AddChoices("Memory Card", "Save State", "Save Folder"));
        return choice switch
        {
            "Save State" => SaveType.SaveState,
            "Save Folder" => SaveType.SaveFolder,
            _ => SaveType.MemoryCard,
        };
    }

    private static List<string> ResolveFiles(string[] provided, SaveType saveType)
    {
        List<string> files = provided
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(Path.GetFullPath)
            .ToList();

        if (files.Count == 0)
        {
            bool multi = saveType != SaveType.MemoryCard;
            string hint = multi
                ? "Enter save file paths (blank line to finish):"
                : "Enter the memory card file path:";
            AnsiConsole.MarkupLine($"[grey]{hint}[/]");
            while (true)
            {
                string path = AnsiConsole.Prompt(new TextPrompt<string>("File:").AllowEmpty());
                if (string.IsNullOrWhiteSpace(path))
                {
                    break;
                }
                files.Add(Path.GetFullPath(path.Trim('"')));
                if (!multi)
                {
                    break;
                }
            }
        }

        List<string> missing = files.Where(f => !File.Exists(f)).ToList();
        foreach (string m in missing)
        {
            AnsiConsole.MarkupLine($"[red]Missing file:[/] {Markup.Escape(m)}");
        }
        return files.Where(File.Exists).ToList();
    }

    private static string Ask(string label, string? value, IReadOnlyList<string>? suggestions = null)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        if (suggestions is { Count: > 0 })
        {
            TextPrompt<string> prompt = new TextPrompt<string>($"{label}:")
                .Validate(v => string.IsNullOrWhiteSpace(v)
                    ? ValidationResult.Error("Required")
                    : ValidationResult.Success());
            AnsiConsole.MarkupLine($"[grey]Common: {string.Join(", ", suggestions)}[/]");
            return AnsiConsole.Prompt(prompt).Trim();
        }

        return AnsiConsole.Prompt(new TextPrompt<string>($"{label}:")
            .Validate(v => string.IsNullOrWhiteSpace(v)
                ? ValidationResult.Error("Required")
                : ValidationResult.Success())).Trim();
    }
}
