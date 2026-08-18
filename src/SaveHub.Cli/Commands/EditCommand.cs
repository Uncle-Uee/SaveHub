using System.ComponentModel;
using SaveHub.Core;
using SaveHub.Core.Abstractions;
using SaveHub.Core.Configuration;
using SaveHub.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SaveHub.Cli.Commands;

/// <summary>Replaces an existing save's contents and description in place (by index).</summary>
internal sealed class EditCommand : AsyncCommand<EditCommand.Settings>
{
    internal sealed class Settings : GlobalSettings
    {
        [CommandOption("-p|--platform <PLATFORM>")]
        [Description("Platform folder, e.g. PS2.")]
        public string? Platform { get; init; }

        [CommandOption("-g|--game|--titleid <GAMEID>")]
        [Description("Game id / serial folder, e.g. SLUS-21274.")]
        public string? GameId { get; init; }

        [CommandOption("-t|--type <TYPE>")]
        [Description("Save type: mc (memory card), state (save state), or folder.")]
        public string? Type { get; init; }

        [CommandOption("--index <N>")]
        [Description("Index of the save to replace, e.g. 1 for 01.zip.")]
        public int? Index { get; init; }

        [CommandOption("-f|--file <PATH>")]
        [Description("Replacement save file(s). Repeat for multiple files.")]
        public string[] Files { get; init; } = [];

        [CommandOption("-d|--description <TEXT>")]
        [Description("New description for the save.")]
        public string? Description { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        SaveHubConfigStore store = CliContext.ResolveStore(settings.ConfigPath);
        if (!store.Exists)
        {
            AnsiConsole.MarkupLine("[red]No configuration found.[/] Run [bold]savehub config github[/] first.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(settings.Platform) ||
            string.IsNullOrWhiteSpace(settings.GameId) ||
            settings.Index is null ||
            settings.Files.Length == 0 ||
            string.IsNullOrWhiteSpace(settings.Description))
        {
            AnsiConsole.MarkupLine("[red]--platform, --game, --index, --file and --description are required.[/]");
            return 1;
        }

        SaveType saveType = ParseType(settings.Type);
        List<string> files = settings.Files
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(Path.GetFullPath)
            .Where(File.Exists)
            .ToList();
        if (files.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No valid files found.[/]");
            return 1;
        }

        SaveHubClient client = await CliContext.CreateClientAsync(store.Load(), cancellationToken);
        SaveUploadRequest request = new SaveUploadRequest
        {
            Platform = settings.Platform,
            GameId = settings.GameId,
            SaveType = saveType,
            Files = files,
            Description = settings.Description,
            AutoFetchCoverArt = false,
        };

        SaveUploadResult result = null!;
        await AnsiConsole.Status().StartAsync("Updating...", async _ =>
        {
            result = await client.UploadAsync(request, new UploadOptions { TargetIndex = settings.Index }, cancellationToken);
        });

        if (result.Merged)
        {
            AnsiConsole.MarkupLine($"[green]Updated:[/] {result.ArchivePath}");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Submitted:[/] {Markup.Escape(result.Message)}");
        }
        if (!string.IsNullOrWhiteSpace(result.PullRequestUrl))
        {
            AnsiConsole.MarkupLine($"[blue]{result.PullRequestUrl}[/]");
        }
        return 0;
    }

    private static SaveType ParseType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SaveType.MemoryCard;
        }
        return value.Trim().ToLowerInvariant() switch
        {
            "state" or "savestate" or "sstate" => SaveType.SaveState,
            "folder" or "savefolder" or "dir" => SaveType.SaveFolder,
            _ => SaveType.MemoryCard,
        };
    }
}
