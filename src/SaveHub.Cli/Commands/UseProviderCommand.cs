using System.ComponentModel;
using SaveHub.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SaveHub.Cli.Commands;

/// <summary>Sets the active provider (github | supabase | googledrive).</summary>
internal sealed class UseProviderCommand : Command<UseProviderCommand.Settings>
{
    internal sealed class Settings : GlobalSettings
    {
        [CommandArgument(0, "<PROVIDER>")]
        [Description("Provider to activate: github | supabase | googledrive.")]
        public string Provider { get; init; } = string.Empty;
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        string provider = settings.Provider.Trim().ToLowerInvariant();
        if (provider is not ("github" or "supabase" or "googledrive"))
        {
            AnsiConsole.MarkupLine("[red]Unknown provider.[/] Use github | supabase | googledrive.");
            return 1;
        }
        SaveHubConfigStore store = CliContext.ResolveStore(settings.ConfigPath);
        SaveHubConfig config = store.Load();
        config.ActiveProvider = provider;
        store.Save(config);
        AnsiConsole.MarkupLine($"[green]Active provider set to[/] {provider}.");
        return 0;
    }
}
