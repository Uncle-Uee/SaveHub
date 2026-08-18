using SaveHub.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SaveHub.Cli.Commands;

/// <summary>Shows product, version, attribution and support information.</summary>
internal sealed class InfoCommand : Command<GlobalSettings>
{
    protected override int Execute(CommandContext context, GlobalSettings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"[bold]{SaveHubInfo.Product}[/] v{SaveHubInfo.Version}");
        AnsiConsole.MarkupLine($"Project: [blue]{SaveHubInfo.ProjectUrl}[/]");
        AnsiConsole.MarkupLine($"Support this project: [blue]{SaveHubInfo.DonateUrl}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Open source under the MIT License. See LICENSE.[/]");
        return 0;
    }
}
