namespace SaveHub.Core;

/// <summary>
/// Attribution and support links surfaced by the library and any frontend built on it.
/// Update these when you fork/deploy your own instance.
/// </summary>
public static class SaveHubInfo
{
    public const string Product = "SaveHub";
    public const string Version = "0.1.0";

    /// <summary>Project home / source.</summary>
    public static string ProjectUrl { get; set; } = "https://github.com/uncle-uee/SaveHub";

    /// <summary>Support / donation link ("buy me a coffee", Ko-fi, PayPal, GitHub Sponsors, ...).</summary>
    public static string DonateUrl { get; set; } = "https://pay.yoco.com/savehub";

    /// <summary>Short attribution string frontends can display.</summary>
    public static string Attribution => $"Powered by {Product} - {ProjectUrl} (support: {DonateUrl})";
}
