using System.ComponentModel;
using SaveHub.Core.Configuration;
using SaveHub.GitHub;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SaveHub.Cli.Commands;

/// <summary>Creates or updates the GitHub connection stored in the config file.</summary>
internal sealed class ConfigureGitHubCommand : Command<ConfigureGitHubCommand.Settings>
{
    internal sealed class Settings : GlobalSettings
    {
        [CommandOption("-o|--owner <OWNER>")]
        [Description("Repository owner (user or organization).")]
        public string? Owner { get; init; }

        [CommandOption("-r|--repo <REPO>")]
        [Description("Repository name.")]
        public string? Repository { get; init; }

        [CommandOption("-b|--branch <BRANCH>")]
        [Description("Target branch. Leave empty to use the repo default branch.")]
        public string? Branch { get; init; }

        [CommandOption("--token <TOKEN>")]
        [Description("Personal access token. Prefer the SAVEHUB_GITHUB_TOKEN env var instead.")]
        public string? Token { get; init; }

        [CommandOption("--auto-merge")]
        [Description("Allow auto-merge when you have write access (at your own risk).")]
        public bool? AutoMerge { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        SaveHubConfigStore store = CliContext.ResolveStore(settings.ConfigPath);
        SaveHubConfig config = store.Load();
        GitHubProviderSettings existing = GitHubProviderFactory.ReadSettings(config) ?? new GitHubProviderSettings();

        string owner = settings.Owner ?? Prompt("Repository owner", existing.Owner);
        string repo = settings.Repository ?? Prompt("Repository name", existing.Repository);
        string branch = settings.Branch ?? Prompt("Branch (blank = default)", existing.Branch, allowEmpty: true);

        bool autoMerge = settings.AutoMerge
            ?? AnsiConsole.Confirm(
                "Enable auto-merge when you have write access? [yellow](at your own risk)[/]", existing.AutoMerge);

        existing.Owner = owner.Trim();
        existing.Repository = repo.Trim();
        existing.Branch = branch.Trim();
        existing.AutoMerge = autoMerge;
        if (!string.IsNullOrWhiteSpace(settings.Token))
        {
            existing.Token = settings.Token;
        }

        GitHubProviderFactory.WriteSettings(config, existing);
        store.Save(config);

        AnsiConsole.MarkupLine($"[green]Saved GitHub connection to[/] {store.Path}");
        if (existing.ResolveToken() is null)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]No token found.[/] Set one with [bold]$env:{existing.TokenEnvironmentVariable}[/] before uploading.");
        }
        return 0;
    }

    private static string Prompt(string label, string current, bool allowEmpty = false)
    {
        TextPrompt<string> prompt = new TextPrompt<string>($"{label}:").AllowEmpty();
        if (!string.IsNullOrEmpty(current))
        {
            prompt.DefaultValue(current);
        }
        else if (!allowEmpty)
        {
            prompt.Validate(v => string.IsNullOrWhiteSpace(v)
                ? ValidationResult.Error("A value is required")
                : ValidationResult.Success());
        }
        return AnsiConsole.Prompt(prompt);
    }
}
