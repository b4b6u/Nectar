namespace Nectar.Settings;

/// <summary>
/// The ids a theme can have. Kept free of WinUI types so the settings model and the tests can use
/// them without loading XAML; <see cref="Services.AppTheme"/> owns the actual colours.
/// </summary>
public static class ThemeIds
{
    public const string Paper = "paper";
    public const string Meadow = "meadow";
    public const string Sunset = "sunset";
    public const string Midnight = "midnight";

    public const string Default = Paper;

    public static readonly string[] All = { Paper, Meadow, Sunset, Midnight };

    public static bool IsKnown(string? id) =>
        id is not null && Array.Exists(All, known => string.Equals(known, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Anything hand-edited or from a future build falls back to the shipping look.</summary>
    public static string Normalize(string? id) => IsKnown(id) ? id!.ToLowerInvariant() : Default;
}
