using System.ComponentModel;
using SaveHub.Core;
using SaveHub.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SaveHub.Cli.Commands;

/// <summary>Downloads a save archive from the configured storage provider.</summary>
internal sealed class DownloadCommand : AsyncCommand<DownloadCommand.Settings>
{
    internal sealed class Settings : GlobalSettings
    {
        [CommandOption("-p|--platform <PLATFORM>")]
        [Description("Platform folder, e.g. PS2.")]
        public string? Platform { get; init; }

        [CommandOption("-g|--game <GAMEID>")]
        [Description("Game id / serial folder, e.g. SCUS-97199.")]
        public string? GameId { get; init; }

        [CommandOption("-a|--archive <NAME>")]
        [Description("Archive file name, e.g. 01.zip or 01-sstate.zip.")]
        public string? Archive { get; init; }

        [CommandOption("-o|--output <PATH>")]
        [Description("Destination file path. Defaults to the archive name in the current directory.")]
        public string? Output { get; init; }
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

        SaveHubClient client = await CliContext.CreateClientAsync(store.Load(), cancellationToken);
        string output = string.IsNullOrWhiteSpace(settings.Output)
            ? System.IO.Path.Combine(Environment.CurrentDirectory, settings.Archive)
            : settings.Output;

        bool ok = false;
        await AnsiConsole.Status().StartAsync("Downloading...", async _ =>
        {
            ok = await client.DownloadArchiveToFileAsync(settings.Platform, settings.GameId, settings.Archive, output, cancellationToken);
        });

        if (!ok)
        {
            AnsiConsole.MarkupLine("[red]Not found:[/] that archive does not exist in the repository.");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Downloaded:[/] {Markup.Escape(output)}");
        return 0;
    }
}
