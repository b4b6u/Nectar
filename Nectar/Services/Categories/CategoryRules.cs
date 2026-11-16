namespace Nectar.Services.Categories;

/// <summary>
/// The free tier's category cap, kept free of WinUI and of the database so the rule can be tested on
/// its own. Three is the line: enough to separate work, home and the rest, not enough to organise a
/// life. A licence lifts it entirely.
/// </summary>
internal static class CategoryRules
{
    internal const int FreeLimit = 3;

    /// <summary>Longer than this stops fitting a chip without truncating mid-word.</summary>
    internal const int MaxNameLength = 32;

    internal static int LimitFor(bool isPro) => isPro ? int.MaxValue : FreeLimit;

    internal static bool CanAdd(int existing, bool isPro) => existing < LimitFor(isPro);

    internal static bool IsAtLimit(int existing, bool isPro) => existing >= LimitFor(isPro);

    /// <summary>What the panel says as the limit approaches, and after it is reached.</summary>
    internal static string LimitMessage(int existing, bool isPro) => isPro
        ? $"You have {existing} categories."
        : !CanAdd(existing, isPro)
            ? $"Free stops at {FreeLimit} categories. Nectar Pro removes the limit."
            : $"{existing} of {FreeLimit} free categories used.";

    /// <summary>
    /// Trims, collapses inner whitespace and clamps the length; empty means "no name", which the
    /// caller must refuse rather than storing a blank chip.
    /// </summary>
    internal static string NormalizeName(string? name)
    {
        var collapsed = string.Join(' ', (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= MaxNameLength ? collapsed : collapsed[..MaxNameLength].TrimEnd();
    }
}
