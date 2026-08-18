using System.ComponentModel;
using SaveHub.Core.Configuration;
using SaveHub.Supabase;
using Spectre.Console;
using Spectre.Console.Cli;

namespace SaveHub.Cli.Commands;

/// <summary>Creates or updates the Supabase connection and makes it the active provider.</summary>
internal sealed class ConfigureSupabaseCommand : Command<ConfigureSupabaseCommand.Settings>
{
    internal sealed class Settings : GlobalSettings
    {
        [CommandOption("-u|--url <URL>")]
        [Description("Project URL, e.g. https://YOUR-PROJECT.supabase.co.")]
        public string? Url { get; init; }

        [CommandOption("-b|--bucket <BUCKET>")]
        [Description("Storage bucket name.")]
        public string? Bucket { get; init; }

        [CommandOption("--key <KEY>")]
        [Description("API key. Prefer the SAVEHUB_SUPABASE_KEY env var.")]
        public string? Key { get; init; }

        [CommandOption("--owner")]
        [Description("You own the bucket (publish directly instead of pending/).")]
        public bool? Owner { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        SaveHubConfigStore store = CliContext.ResolveStore(settings.ConfigPath);
        SaveHubConfig config = store.Load();
        SupabaseProviderSettings existing = SupabaseProviderFactory.ReadSettings(config) ?? new SupabaseProviderSettings();

        existing.Url = (settings.Url ?? Prompt("Project URL", existing.Url)).Trim();
        existing.Bucket = (settings.Bucket ?? Prompt("Bucket", string.IsNullOrEmpty(existing.Bucket) ? "saves" : existing.Bucket)).Trim();
        existing.IsOwner = settings.Owner ?? AnsiConsole.Confirm("Do you own this bucket (publish directly)?", existing.IsOwner);
        if (!string.IsNullOrWhiteSpace(settings.Key))
        {
            existing.ApiKey = settings.Key;
        }

        SupabaseProviderFactory.WriteSettings(config, existing);
        store.Save(config);
        AnsiConsole.MarkupLine($"[green]Saved Supabase connection[/] ({store.Path}). Active provider: supabase.");
        if (existing.ResolveKey() is null)
        {
            AnsiConsole.MarkupLine($"[yellow]No key set.[/] Set [bold]$env:{existing.ApiKeyEnvironmentVariable}[/] before uploading.");
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
