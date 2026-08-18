using System.Text.Json;

namespace SaveHub.Core.Configuration;

/// <summary>Loads and saves <see cref="SaveHubConfig"/> from a JSON file on disk.</summary>
public sealed class SaveHubConfigStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    /// <summary>The resolved configuration file path.</summary>
    public string Path { get; }

    /// <summary>
    /// The default per-user config location:
    /// <c>%APPDATA%/SaveHub/savehub.config.json</c> (or the XDG equivalent on non-Windows).
    /// </summary>
    public static string DefaultPath
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(root))
            {
                root = Environment.CurrentDirectory;
            }
            return System.IO.Path.Combine(root, "SaveHub", "savehub.config.json");
        }
    }

    public bool Exists => File.Exists(Path);

    public SaveHubConfigStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
    }

    /// <summary>Loads the config, or returns a fresh default when the file does not exist.</summary>
    public SaveHubConfig Load()
    {
        if (!File.Exists(Path))
        {
            return new SaveHubConfig();
        }

        string json = File.ReadAllText(Path);
        return JsonSerializer.Deserialize<SaveHubConfig>(json, Options) ?? new SaveHubConfig();
    }

    /// <summary>Writes the config to disk, creating the directory if needed.</summary>
    public void Save(SaveHubConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        string? directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(Path, JsonSerializer.Serialize(config, Options));
    }
}
