using SaveHub.Core;
using SaveHub.Core.Abstractions;
using SaveHub.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SaveHub.Cli.Commands;

/// <summary>Verifies the configured token authenticates and can access the target repository.</summary>
internal sealed class TestConnectionCommand : AsyncCommand<GlobalSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, GlobalSettings settings, CancellationToken cancellationToken)
    {
        SaveHubConfigStore store = CliContext.ResolveStore(settings.ConfigPath);
        if (!store.Exists)
        {
            AnsiConsole.MarkupLine("[red]No configuration found.[/] Run [bold]savehub config github[/] first.");
            return 1;
        }

        SaveHubClient client = await CliContext.CreateClientAsync(store.Load(), cancellationToken);

        ConnectionTestResult result = await AnsiConsole.Status()
            .StartAsync("Testing connection...", async _ => await client.TestConnectionAsync(cancellationToken));

        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]Failed:[/] {Markup.Escape(result.Message)}");
            return 1;
        }

        Table table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Check");
        table.AddColumn("Result");
        table.AddRow("Authenticated as", Markup.Escape(result.AuthenticatedAs ?? "(unknown)"));
        table.AddRow("Repository", Markup.Escape(result.Target ?? "(unknown)"));
        table.AddRow("Write access", result.HasWriteAccess ? "[green]yes[/]" : "[yellow]no (fork + PR)[/]");
        table.AddRow("Auto-merge effective", result.AutoMergeEffective ? "[green]yes[/]" : "no");
        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[green]{Markup.Escape(result.Message)}[/]");
        return 0;
    }
}
