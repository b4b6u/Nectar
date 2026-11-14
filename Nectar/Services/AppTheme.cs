using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Nectar.Settings;
using Windows.UI;

namespace Nectar.Services;

/// <summary>
/// One look for the whole app: the shell behind the notch, the four card colours, and the ink that
/// sits on them. A theme is applied by mutating the brushes the XAML already references, so every
/// window that is open — and every window opened later — repaints without being rebuilt.
/// </summary>
public sealed record ThemePalette(
    string Id,
    string Name,
    string Hint,
    Color Shell,
    Color Mint,
    Color Lilac,
    Color Butter,
    Color Sky,
    Color Ink,
    Color Track,
    Color Active,
    Color ChartBar,
    Color ChartDone,
    Color Grid,
    Color Danger,
    Color BrandA,
    Color BrandB,
    Color DeepGreen);

public static class AppTheme
{
    public const string Paper = ThemeIds.Paper;
    public const string Meadow = ThemeIds.Meadow;
    public const string Sunset = ThemeIds.Sunset;
    public const string Midnight = ThemeIds.Midnight;
    public const string Default = ThemeIds.Default;

    public static readonly IReadOnlyList<ThemePalette> Options = new[]
    {
        new ThemePalette(
            Paper,
            "Paper",
            "The shipping look: pastel cards on the black notch.",
            Color.FromArgb(0xFF, 0x00, 0x00, 0x00),
            Color.FromArgb(0xFF, 0x8D, 0xBA, 0x98),
            Color.FromArgb(0xFF, 0xA3, 0x9C, 0xD0),
            Color.FromArgb(0xFF, 0xD2, 0xCA, 0x72),
            Color.FromArgb(0xFF, 0x8C, 0xB1, 0xCD),
            Color.FromArgb(0xF2, 0x1B, 0x24, 0x1C),
            Color.FromArgb(0xFF, 0x1C, 0x1F, 0x24),
            Color.FromArgb(0xFF, 0x3B, 0x42, 0x4B),
            Color.FromArgb(0xE6, 0x23, 0x25, 0x30),
            Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A),
            Color.FromArgb(0x1A, 0x00, 0x00, 0x00),
            Color.FromArgb(0xFF, 0xC0, 0x4B, 0x53),
            Color.FromArgb(0xFF, 0xBD, 0xF3, 0xD3),
            Color.FromArgb(0xFF, 0x5F, 0xC1, 0x89),
            Color.FromArgb(0xFF, 0x2E, 0x6B, 0x4A)),
        new ThemePalette(
            Meadow,
            "Meadow",
            "Greener cards, a colder black, brand in moss.",
            Color.FromArgb(0xFF, 0x07, 0x13, 0x0E),
            Color.FromArgb(0xFF, 0x9C, 0xC9, 0xA4),
            Color.FromArgb(0xFF, 0xB6, 0xC9, 0x8F),
            Color.FromArgb(0xFF, 0xE0, 0xCB, 0x86),
            Color.FromArgb(0xFF, 0x93, 0xB6, 0xA8),
            Color.FromArgb(0xF2, 0x16, 0x24, 0x1B),
            Color.FromArgb(0xFF, 0x15, 0x24, 0x1B),
            Color.FromArgb(0xFF, 0x2F, 0x4A, 0x38),
            Color.FromArgb(0xE6, 0x1C, 0x2A, 0x22),
            Color.FromArgb(0xFF, 0x14, 0x1F, 0x17),
            Color.FromArgb(0x1A, 0x0A, 0x1A, 0x12),
            Color.FromArgb(0xFF, 0xD8, 0x6B, 0x6B),
            Color.FromArgb(0xC8, 0xE6, 0xF2, 0xB6),
            Color.FromArgb(0x66, 0x9C, 0xC9, 0x6A),
            Color.FromArgb(0xFF, 0x3F, 0x7A, 0x4F)),
        new ThemePalette(
            Sunset,
            "Sunset",
            "Warm cards, ink browned, an orange bar.",
            Color.FromArgb(0xFF, 0x14, 0x0A, 0x0C),
            Color.FromArgb(0xFF, 0xE7, 0xA8, 0x81),
            Color.FromArgb(0xFF, 0xD9, 0xA2, 0xB4),
            Color.FromArgb(0xFF, 0xEF, 0xC9, 0x8A),
            Color.FromArgb(0xFF, 0xC1, 0xA7, 0xD6),
            Color.FromArgb(0xF2, 0x2A, 0x14, 0x18),
            Color.FromArgb(0xFF, 0x24, 0x14, 0x16),
            Color.FromArgb(0xFF, 0x4A, 0x2A, 0x2C),
            Color.FromArgb(0xE6, 0x30, 0x18, 0x1A),
            Color.FromArgb(0xFF, 0x1E, 0x10, 0x12),
            Color.FromArgb(0x1A, 0x20, 0x08, 0x0A),
            Color.FromArgb(0xFF, 0xD9, 0x5A, 0x4E),
            Color.FromArgb(0xFF, 0xF3, 0xB4, 0x7E),
            Color.FromArgb(0xFF, 0xF6, 0x57, 0x2D),
            Color.FromArgb(0xFF, 0x9A, 0x3B, 0x33)),
        new ThemePalette(
            Midnight,
            "Midnight",
            "Dark cards and light ink, for a dim room.",
            Color.FromArgb(0xFF, 0x04, 0x07, 0x0A),
            Color.FromArgb(0xFF, 0x24, 0x30, 0x39),
            Color.FromArgb(0xFF, 0x2C, 0x2A, 0x3C),
            Color.FromArgb(0xFF, 0x37, 0x33, 0x2A),
            Color.FromArgb(0xFF, 0x23, 0x34, 0x3F),
            Color.FromArgb(0xF2, 0xEE, 0xF2, 0xEC),
            Color.FromArgb(0xFF, 0x12, 0x17, 0x1C),
            Color.FromArgb(0xFF, 0x2A, 0x32, 0x3C),
            Color.FromArgb(0xE6, 0xD8, 0xE2, 0xEA),
            Color.FromArgb(0xFF, 0xF2, 0xF6, 0xFA),
            Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF),
            Color.FromArgb(0xFF, 0xFF, 0x8C, 0x8C),
            Color.FromArgb(0xFF, 0x7F, 0xD3, 0xFF),
            Color.FromArgb(0xFF, 0x4A, 0x7B, 0xD0),
            Color.FromArgb(0xFF, 0x9C, 0xC9, 0xA4))
    };

    public static ThemePalette? Find(string? id) =>
        Options.FirstOrDefault(option => string.Equals(option.Id, id, StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string? id) => ThemeIds.Normalize(id);

    public static string NameOf(string? id) => Find(id)?.Name ?? "Paper";

    /// <summary>
    /// Repaints every themed brush in place. Unknown ids fall back to Paper so a hand-edited
    /// settings file can never leave the app half-themed.
    /// </summary>
    public static void Apply(string? id)
    {
        var palette = Find(id) ?? Options[0];
        var ink = palette.Ink;

        Set("NectarShellBrush", palette.Shell);
        Set("NectarShellTextBrush", Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
        Set("NectarShellTextMutedBrush", Color.FromArgb(0x7A, 0xFF, 0xFF, 0xFF));
        Set("NectarHoverBrush", Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));

        Set("NectarMintCardBrush", palette.Mint);
        Set("NectarLilacCardBrush", palette.Lilac);
        Set("NectarButterCardBrush", palette.Butter);
        Set("NectarSkyCardBrush", palette.Sky);

        Set("NectarInkBrush", ink);
        Set("NectarInkMutedBrush", WithAlpha(ink, 0x8A));
        Set("NectarInkFaintBrush", WithAlpha(ink, 0x59));

        Set("NectarRowBrush", Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF));
        Set("NectarRowStrokeBrush", Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
        Set("NectarChipBrush", Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
        Set("NectarChipActiveBrush", Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF));

        Set("NectarTabTrackBrush", palette.Track);
        Set("NectarTabActiveBrush", palette.Active);

        Set("NectarChartBarBrush", palette.ChartBar);
        Set("NectarChartCompletedBrush", palette.ChartDone);
        Set("NectarChartPlannedBrush", WithAlpha(ink, 0x33));
        Set("NectarChartGridBrush", palette.Grid);

        Set("NectarPillTrackBrush", Color.FromArgb(0x26, 0x00, 0x00, 0x00));
        Set("NectarDangerBrush", palette.Danger);
        Set("NectarDeepGreenBrush", palette.DeepGreen);
        Set("NectarDeepGreenSoftBrush", WithAlpha(palette.DeepGreen, 0x2E));
        Set("NectarTransparentBrush", Color.FromArgb(0x00, 0x00, 0x00, 0x00));

        if (Lookup("NectarBrandBrush") is LinearGradientBrush brand && brand.GradientStops.Count >= 2)
        {
            brand.GradientStops[0].Color = palette.BrandA;
            brand.GradientStops[1].Color = palette.BrandB;
        }
    }

    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static void Set(string key, Color color)
    {
        if (Lookup(key) is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    /// <summary>
    /// Finds a resource in the app's dictionary, merged dictionaries included — StaticResource in
    /// XAML resolves to those same brush instances, so changing one changes every user of it.
    /// </summary>
    private static object? Lookup(string key)
    {
        if (Application.Current is null)
        {
            return null;
        }

        return Lookup(Application.Current.Resources, key);
    }

    private static object? Lookup(ResourceDictionary dictionary, string key)
    {
        if (dictionary.TryGetValue(key, out var direct))
        {
            return direct;
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            var found = Lookup(merged, key);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
