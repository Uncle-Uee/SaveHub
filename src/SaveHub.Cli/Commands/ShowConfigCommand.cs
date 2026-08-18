using SaveHub.Core.Configuration;
using SaveHub.GitHub;
using SaveHub.GoogleDrive;
using SaveHub.Supabase;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SaveHub.Cli.Commands;

/// <summary>Prints the current configuration (secrets redacted).</summary>
internal sealed class ShowConfigCommand : Command<GlobalSettings>
{
    protected override int Execute(CommandContext context, GlobalSettings settings, CancellationToken cancellationToken)
    {
        SaveHubConfigStore store = CliContext.ResolveStore(settings.ConfigPath);
        AnsiConsole.MarkupLine($"[bold]Config file:[/] {store.Path}");
        if (!store.Exists)
        {
            AnsiConsole.MarkupLine("[yellow]No config file yet. Run 'savehub config github|supabase|google'.[/]");
            return 0;
        }

        SaveHubConfig config = store.Load();
        AnsiConsole.MarkupLine($"[bold]Active provider:[/] {config.ActiveProvider}");

        GitHubProviderSettings? gh = GitHubProviderFactory.ReadSettings(config);
        if (gh is not null)
        {
            Table table = new Table().Border(TableBorder.Rounded).Title("GitHub");
            table.AddColumns("Setting", "Value");
            table.AddRow("Owner", gh.Owner);
            table.AddRow("Repository", gh.Repository);
            table.AddRow("Branch", string.IsNullOrWhiteSpace(gh.Branch) ? "(default)" : gh.Branch);
            table.AddRow("Auto-merge", gh.AutoMerge ? "enabled" : "disabled");
            table.AddRow("Token", gh.ResolveToken() is null ? "[red]not set[/]" : "[green]configured[/]");
            AnsiConsole.Write(table);
        }

        SupabaseProviderSettings? sb = SupabaseProviderFactory.ReadSettings(config);
        if (sb is not null)
        {
            Table table = new Table().Border(TableBorder.Rounded).Title("Supabase");
            table.AddColumns("Setting", "Value");
            table.AddRow("URL", sb.Url);
            table.AddRow("Bucket", sb.Bucket);
            table.AddRow("Owner", sb.IsOwner ? "yes" : "no");
            table.AddRow("Key", sb.ResolveKey() is null ? "[red]not set[/]" : "[green]configured[/]");
            AnsiConsole.Write(table);
        }

        GoogleDriveProviderSettings? gd = GoogleDriveProviderFactory.ReadSettings(config);
        if (gd is not null)
        {
            Table table = new Table().Border(TableBorder.Rounded).Title("Google Drive");
            table.AddColumns("Setting", "Value");
            table.AddRow("Root folder id", gd.RootFolderId);
            table.AddRow("Client id", string.IsNullOrWhiteSpace(gd.ClientId) ? "[red]not set[/]" : gd.ClientId);
            table.AddRow("Owner", gd.IsOwner ? "yes" : "no");
            table.AddRow("Secret", gd.ResolveClientSecret() is null ? "[red]not set[/]" : "[green]configured[/]");
            table.AddRow("Session", GoogleDriveSession.HasActiveSession ? "[green]signed in[/]" : "not signed in");
            AnsiConsole.Write(table);
        }
        return 0;
    }
}
