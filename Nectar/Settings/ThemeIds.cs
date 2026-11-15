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

    /// <summary>The three palettes that ship free, forever.</summary>
    public static readonly string[] Free = { Paper, Meadow, Sunset };

    public static bool IsEntitled(string? id, bool isPro) =>
        isPro || (id is not null && Array.Exists(Free, free => string.Equals(free, id, StringComparison.OrdinalIgnoreCase)));

    /// <summary>A Pro palette on a free install resolves to Paper rather than repainting oddly.</summary>
    public static string Resolve(string? id, bool isPro)
    {
        var known = Normalize(id);
        return IsEntitled(known, isPro) ? known : Default;
    }

    public static bool IsKnown(string? id) =>
        id is not null && Array.Exists(All, known => string.Equals(known, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Anything hand-edited or from a future build falls back to the shipping look.</summary>
    public static string Normalize(string? id) => IsKnown(id) ? id!.ToLowerInvariant() : Default;
}
