using System.ComponentModel;
using SaveHub.Bitbucket;
using SaveHub.Core.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SaveHub.Cli.Commands;

/// <summary>Creates or updates the Bitbucket connection and makes it the active provider.</summary>
internal sealed class ConfigureBitbucketCommand : Command<ConfigureBitbucketCommand.Settings>
{
    internal sealed class Settings : GlobalSettings
    {
        [CommandOption("-w|--workspace <WORKSPACE>")]
        [Description("Workspace (repository owner) ID.")]
        public string? Workspace { get; init; }

        [CommandOption("-r|--repo <REPO>")]
        [Description("Repository slug.")]
        public string? Repository { get; init; }

        [CommandOption("-b|--branch <BRANCH>")]
        [Description("Target branch. Leave empty to use the repo main branch.")]
        public string? Branch { get; init; }

        [CommandOption("-u|--username <USERNAME>")]
        [Description("Bitbucket username the app password belongs to.")]
        public string? Username { get; init; }

        [CommandOption("--app-password <PASSWORD>")]
        [Description("App password. Prefer the SAVEHUB_BITBUCKET_APP_PASSWORD env var.")]
        public string? AppPassword { get; init; }

        [CommandOption("--auto-merge")]
        [Description("Allow auto-merge when you have write access (at your own risk).")]
        public bool? AutoMerge { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        SaveHubConfigStore store = CliContext.ResolveStore(settings.ConfigPath);
        SaveHubConfig config = store.Load();
        BitbucketProviderSettings existing = BitbucketProviderFactory.ReadSettings(config) ?? new BitbucketProviderSettings();

        existing.Workspace = (settings.Workspace ?? Prompt("Workspace", existing.Workspace)).Trim();
        existing.Repository = (settings.Repository ?? Prompt("Repository", existing.Repository)).Trim();
        existing.Branch = (settings.Branch ?? Prompt("Branch (blank = main)", existing.Branch)).Trim();
        existing.Username = (settings.Username ?? Prompt("Username", existing.Username)).Trim();
        existing.AutoMerge = settings.AutoMerge
            ?? AnsiConsole.Confirm("Enable auto-merge when you have write access? [yellow](at your own risk)[/]", existing.AutoMerge);
        if (!string.IsNullOrWhiteSpace(settings.AppPassword))
        {
            existing.AppPassword = settings.AppPassword;
        }

        BitbucketProviderFactory.WriteSettings(config, existing);
        store.Save(config);
        AnsiConsole.MarkupLine($"[green]Saved Bitbucket connection[/] ({store.Path}). Active provider: bitbucket.");
        if (existing.ResolveAppPassword() is null)
        {
            AnsiConsole.MarkupLine($"[yellow]No app password set.[/] Set [bold]$env:{existing.AppPasswordEnvironmentVariable}[/] before uploading.");
        }
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
