using System.ComponentModel;
using SaveHub.Core;
using SaveHub.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SaveHub.Cli.Commands;

/// <summary>Deletes a save archive and removes its row from the game's saves index.</summary>
internal sealed class DeleteCommand : AsyncCommand<DeleteCommand.Settings>
{
    internal sealed class Settings : GlobalSettings
    {
        [CommandOption("-p|--platform <PLATFORM>")]
        [Description("Platform folder, e.g. PS2.")]
        public string? Platform { get; init; }

        [CommandOption("-g|--game <GAMEID>")]
        [Description("Game id / serial folder, e.g. SLUS-21274.")]
        public string? GameId { get; init; }

        [CommandOption("-a|--archive <NAME>")]
        [Description("Archive file name to delete, e.g. 01.zip or 01-sstate.zip.")]
        public string? Archive { get; init; }

        [CommandOption("-y|--yes")]
        [Description("Skip the confirmation prompt.")]
        public bool Yes { get; init; }
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
            string.IsNullOrWhiteSpace(settings.Archive))
        {
            AnsiConsole.MarkupLine("[red]--platform, --game and --archive are required.[/]");
            return 1;
        }

        string platform = settings.Platform;
        string game = settings.GameId;
        string archive = settings.Archive;

        if (!settings.Yes &&
            !AnsiConsole.Confirm($"Delete {platform}/{game}/{archive}? This cannot be undone.", false))
        {
            return 0;
        }

        SaveHubClient client = await CliContext.CreateClientAsync(store.Load(), cancellationToken);

        bool deleted = false;
        await AnsiConsole.Status().StartAsync("Deleting...", async _ =>
        {
            deleted = await client.DeleteSaveAsync(platform, game, archive, cancellationToken);
        });

        if (deleted)
        {
            AnsiConsole.MarkupLine($"[green]Deleted[/] {platform}/{game}/{archive}.");
            return 0;
        }

        AnsiConsole.MarkupLine("[yellow]Nothing deleted[/] — that archive was not found.");
        return 1;
    }
}
