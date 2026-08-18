using System.ComponentModel;
using SaveHub.Core;
using SaveHub.Core.Configuration;
using SaveHub.Core.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SaveHub.Cli.Commands;

/// <summary>Lists platforms, games, or saves stored in the backend.</summary>
internal sealed class ListCommand : AsyncCommand<ListCommand.Settings>
{
    internal sealed class Settings : GlobalSettings
    {
        [CommandArgument(0, "[what]")]
        [Description("What to list: platforms | games | saves.")]
        public string What { get; init; } = "platforms";

        [CommandOption("-p|--platform <PLATFORM>")]
        public string? Platform { get; init; }

        [CommandOption("-g|--game <GAMEID>")]
        public string? GameId { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        SaveHubConfigStore store = CliContext.ResolveStore(settings.ConfigPath);
        if (!store.Exists)
        {
            AnsiConsole.MarkupLine("[red]No configuration found.[/] Run [bold]savehub config github[/] first.");
            return 1;
        }

        SaveHubClient client = await CliContext.CreateClientAsync(store.Load(), cancellationToken);

        switch (settings.What.ToLowerInvariant())
        {
            case "platforms":
                Print("Platforms", await client.ListPlatformsAsync());
                break;
            case "games":
                if (string.IsNullOrWhiteSpace(settings.Platform))
                {
                    AnsiConsole.MarkupLine("[red]--platform is required to list games.[/]");
                    return 1;
                }
                Print($"Games in {settings.Platform}", await client.ListGamesAsync(settings.Platform));
                break;
            case "saves":
                if (string.IsNullOrWhiteSpace(settings.Platform) || string.IsNullOrWhiteSpace(settings.GameId))
                {
                    AnsiConsole.MarkupLine("[red]--platform and --game are required to list saves.[/]");
                    return 1;
                }
                IReadOnlyList<SaveEntry> saves = await client.ListSavesAsync(settings.Platform, settings.GameId);
                Table table = new Table().Border(TableBorder.Rounded);
                table.AddColumns("Archive", "Type", "Description");
                foreach (SaveEntry s in saves)
                {
                    table.AddRow(
                        Markup.Escape(s.ArchiveName),
                        s.SaveType.ToString(),
                        Markup.Escape(s.Description ?? string.Empty));
                }
                AnsiConsole.Write(table);
                break;
            default:
                AnsiConsole.MarkupLine("[red]Unknown target.[/] Use platforms | games | saves.");
                return 1;
        }
        return 0;
    }

    private static void Print(string title, IReadOnlyList<string> items)
    {
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(title)}[/]");
        if (items.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey](none)[/]");
            return;
        }
        foreach (string item in items)
        {
            AnsiConsole.MarkupLine($"  {Markup.Escape(item)}");
        }
    }
}
