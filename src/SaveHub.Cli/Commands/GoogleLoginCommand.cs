using Google.Apis.Util.Store;
using SaveHub.Core.Configuration;
using SaveHub.GoogleDrive;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SaveHub.Cli.Commands;

/// <summary>Signs in to Google Drive via the browser and caches the token for future CLI commands.</summary>
internal sealed class GoogleLoginCommand : AsyncCommand<GlobalSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, GlobalSettings settings, CancellationToken cancellationToken)
    {
        SaveHubConfigStore store = CliContext.ResolveStore(settings.ConfigPath);
        SaveHubConfig config = store.Load();
        GoogleDriveProviderSettings? gd = GoogleDriveProviderFactory.ReadSettings(config);
        if (gd is null || string.IsNullOrWhiteSpace(gd.ClientId))
        {
            AnsiConsole.MarkupLine("[red]Google Drive is not configured.[/] Run [bold]savehub config google[/] first.");
            return 1;
        }

        AnsiConsole.MarkupLine("[grey]Opening your browser to sign in...[/]");
        GoogleDriveSession session = await GoogleDriveAuthenticator.SignInAsync(gd, new FileDataStore("SaveHub.GoogleDrive"), cancellationToken: cancellationToken);
        AnsiConsole.MarkupLine($"[green]Signed in{(session.AccountEmail is null ? "" : $" as {session.AccountEmail}")}.[/] Token cached for future commands.");
        return 0;
    }
}
