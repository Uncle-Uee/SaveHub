using System.ComponentModel;
using SaveHub.Core.Configuration;
using SaveHub.GitLab;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SaveHub.Cli.Commands;

/// <summary>Creates or updates the GitLab connection and makes it the active provider.</summary>
internal sealed class ConfigureGitLabCommand : Command<ConfigureGitLabCommand.Settings>
{
    internal sealed class Settings : GlobalSettings
    {
        [CommandOption("--base-url <URL>")]
        [Description("GitLab instance URL. Defaults to https://gitlab.com.")]
        public string? BaseUrl { get; init; }

        [CommandOption("-o|--owner <OWNER>")]
        [Description("Project namespace (user or group path).")]
        public string? Owner { get; init; }

        [CommandOption("-r|--repo <REPO>")]
        [Description("Project path (repository slug).")]
        public string? Repository { get; init; }

        [CommandOption("-b|--branch <BRANCH>")]
        [Description("Target branch. Leave empty to use the project default branch.")]
        public string? Branch { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("Personal access token (api scope). Prefer the SAVEHUB_GITLAB_TOKEN env var.")]
        public string? Token { get; init; }

        [CommandOption("--auto-merge")]
        [Description("Allow auto-merge when you have Maintainer access (at your own risk).")]
        public bool? AutoMerge { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        SaveHubConfigStore store = CliContext.ResolveStore(settings.ConfigPath);
        SaveHubConfig config = store.Load();
        GitLabProviderSettings existing = GitLabProviderFactory.ReadSettings(config) ?? new GitLabProviderSettings();

        existing.BaseUrl = (settings.BaseUrl ?? Prompt("Instance URL", string.IsNullOrEmpty(existing.BaseUrl) ? "https://gitlab.com" : existing.BaseUrl)).Trim();
        existing.Owner = (settings.Owner ?? Prompt("Owner/Group", existing.Owner)).Trim();
        existing.Repository = (settings.Repository ?? Prompt("Repository", existing.Repository)).Trim();
        existing.Branch = (settings.Branch ?? Prompt("Branch (blank = default)", existing.Branch)).Trim();
        existing.AutoMerge = settings.AutoMerge
            ?? AnsiConsole.Confirm("Enable auto-merge when you can merge? [yellow](at your own risk)[/]", existing.AutoMerge);
        if (!string.IsNullOrWhiteSpace(settings.Token))
        {
            existing.Token = settings.Token;
        }

        GitLabProviderFactory.WriteSettings(config, existing);
        store.Save(config);
        AnsiConsole.MarkupLine($"[green]Saved GitLab connection[/] ({store.Path}). Active provider: gitlab.");
        if (existing.ResolveToken() is null)
        {
            AnsiConsole.MarkupLine($"[yellow]No token set.[/] Set [bold]$env:{existing.TokenEnvironmentVariable}[/] before uploading.");
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
