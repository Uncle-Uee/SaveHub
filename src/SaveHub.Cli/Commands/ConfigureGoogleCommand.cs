using System.ComponentModel;
using Google.Apis.Util.Store;
using SaveHub.Core.Configuration;
using SaveHub.GoogleDrive;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SaveHub.Cli.Commands;

/// <summary>Creates or updates the Google Drive connection and makes it the active provider.</summary>
internal sealed class ConfigureGoogleCommand : Command<ConfigureGoogleCommand.Settings>
{
    internal sealed class Settings : GlobalSettings
    {
        [CommandOption("--folder-name <NAME>")]
        [Description("Name of the folder SaveHub creates in your Drive (default: SaveHub).")]
        public string? FolderName { get; init; }

        [CommandOption("-r|--root <FOLDER_ID>")]
        [Description("Advanced: an explicit app-created folder id (usually leave empty).")]
        public string? RootFolderId { get; init; }

        [CommandOption("--client-id <ID>")]
        [Description("OAuth client id (Desktop app) from your own Google Cloud project.")]
        public string? ClientId { get; init; }

        [CommandOption("--secret <SECRET>")]
        [Description("OAuth client secret. Prefer the SAVEHUB_GDRIVE_CLIENT_SECRET env var.")]
        public string? ClientSecret { get; init; }

        [CommandOption("--owner")]
        [Description("You own the Drive folder (publish directly instead of pending/).")]
        public bool? Owner { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        SaveHubConfigStore store = CliContext.ResolveStore(settings.ConfigPath);
        SaveHubConfig config = store.Load();
        GoogleDriveProviderSettings existing = GoogleDriveProviderFactory.ReadSettings(config) ?? new GoogleDriveProviderSettings();

        existing.RootFolderName = (settings.FolderName ?? Prompt("Folder name (created in your Drive)",
            string.IsNullOrEmpty(existing.RootFolderName) ? "SaveHub" : existing.RootFolderName)).Trim();
        existing.ClientId = (settings.ClientId ?? Prompt("OAuth client id", existing.ClientId)).Trim();
        existing.IsOwner = settings.Owner ?? AnsiConsole.Confirm("Do you own this Drive folder (publish directly)?", existing.IsOwner);
        if (!string.IsNullOrWhiteSpace(settings.RootFolderId))
        {
            existing.RootFolderId = settings.RootFolderId.Trim();
        }
        if (!string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            existing.ClientSecret = settings.ClientSecret;
        }

        GoogleDriveProviderFactory.WriteSettings(config, existing);
        store.Save(config);
        AnsiConsole.MarkupLine($"[green]Saved Google Drive connection[/] ({store.Path}). Active provider: googledrive.");
        AnsiConsole.MarkupLine("Uses the [bold]drive.file[/] scope — SaveHub only touches the folder it creates.");
        AnsiConsole.MarkupLine("Run [bold]savehub config google-login[/] to sign in.");
        return 0;
    }

    private static string Prompt(string label, string current)
    {
        TextPrompt<string> prompt = new TextPrompt<string>($"{label}:").AllowEmpty();
        if (!string.IsNullOrEmpty(current))
        {
            prompt.DefaultValue(current);
        }
        return AnsiConsole.Prompt(prompt);
    }
}
