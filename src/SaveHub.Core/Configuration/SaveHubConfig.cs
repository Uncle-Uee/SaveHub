using System.Text.Json;
using System.Text.Json.Serialization;

namespace SaveHub.Core.Configuration;

/// <summary>
/// Root configuration persisted as JSON. It records which storage provider is active and holds a
/// raw settings section per provider so new backends can be added without changing this type.
/// </summary>
public sealed class SaveHubConfig
{
    private static readonly JsonSerializerOptions SectionOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The provider key that is used by default, e.g. "github".</summary>
    public string ActiveProvider { get; set; } = "github";

    /// <summary>Per-provider settings, keyed by provider name. Each value is provider-defined.</summary>
    public Dictionary<string, JsonElement> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Deserializes the settings section for a provider, or null when it is absent.</summary>
    public T? GetProviderSettings<T>(string providerName) where T : class
    {
        return Providers.TryGetValue(providerName, out JsonElement element)
            ? element.Deserialize<T>(SectionOptions)
            : null;
    }

    /// <summary>Stores (or replaces) the settings section for a provider.</summary>
    public void SetProviderSettings<T>(string providerName, T settings) where T : class
    {
        JsonElement json = JsonSerializer.SerializeToElement(settings, SectionOptions);
        Providers[providerName] = json;
    }
}
