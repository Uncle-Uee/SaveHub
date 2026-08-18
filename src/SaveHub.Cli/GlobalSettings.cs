using Spectre.Console.Cli;
using System.ComponentModel;

namespace SaveHub.Cli;

/// <summary>Options shared by all commands.</summary>
internal class GlobalSettings : CommandSettings
{
    [CommandOption("-c|--config <PATH>")]
    [Description("Path to the SaveHub config file. Defaults to the per-user location.")]
    public string? ConfigPath { get; init; }
}
