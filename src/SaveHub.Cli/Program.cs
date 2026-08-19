using SaveHub.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

CommandApp app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("savehub");

    config.AddBranch("config", cfg =>
    {
        cfg.SetDescription("Manage SaveHub connections and settings.");
        cfg.AddCommand<ConfigureGitHubCommand>("github")
            .WithDescription("Create or update the GitHub connection.");
        cfg.AddCommand<ConfigureGitLabCommand>("gitlab")
            .WithDescription("Create or update the GitLab connection.");
        cfg.AddCommand<ConfigureBitbucketCommand>("bitbucket")
            .WithDescription("Create or update the Bitbucket connection.");
        cfg.AddCommand<ConfigureSupabaseCommand>("supabase")
            .WithDescription("Create or update the Supabase connection.");
        cfg.AddCommand<ConfigureGoogleCommand>("google")
            .WithDescription("Create or update the Google Drive connection.");
        cfg.AddCommand<GoogleLoginCommand>("google-login")
            .WithDescription("Sign in to Google Drive via the browser.");
        cfg.AddCommand<UseProviderCommand>("use")
            .WithDescription("Set the active provider (github | gitlab | bitbucket | supabase | googledrive).");
        cfg.AddCommand<ShowConfigCommand>("show")
            .WithDescription("Show the current configuration.");
        cfg.AddCommand<TestConnectionCommand>("test")
            .WithDescription("Verify the token authenticates and can access the repository.");
    });

    config.AddCommand<UploadCommand>("upload")
        .WithDescription("Upload a memory card or save state.");
    config.AddCommand<EditCommand>("edit")
        .WithDescription("Replace an existing save's contents and description (by index).");
    config.AddCommand<DownloadCommand>("download")
        .WithDescription("Download a save archive.");
    config.AddCommand<DeleteCommand>("delete")
        .WithDescription("Delete a save archive from the backend.");
    config.AddCommand<ListCommand>("list")
        .WithDescription("List platforms, games, or saves.");
    config.AddCommand<InfoCommand>("info")
        .WithDescription("Show product and support information.");

    config.SetExceptionHandler((ex, _) =>
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
        return 1;
    });
});

return app.Run(args);
