using Nectar.Settings;
using Xunit;

namespace Nectar.Tests;

/// <summary>
/// A theme id comes from settings.json, which a user can hand-edit and an older build can write.
/// Whatever it says, the app has to end up on a look it actually ships.
/// </summary>
public sealed class ThemeIdsTests
{
    [Fact]
    public void ShippingLookIsPaper()
    {
        Assert.Equal("paper", ThemeIds.Default);
        Assert.Equal(ThemeIds.Default, new WorkspaceSettings().Theme);
    }

    [Theory]
    [InlineData("paper")]
    [InlineData("meadow")]
    [InlineData("sunset")]
    [InlineData("midnight")]
    public void EveryOfferedThemeSurvivesNormalization(string id)
    {
        Assert.Contains(id, ThemeIds.All);
        Assert.Equal(id, ThemeIds.Normalize(id));
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("PAPER ")]
    [InlineData("")]
    [InlineData(null)]
    public void UnknownThemeFallsBackToPaper(string? id)
    {
        Assert.Equal(ThemeIds.Default, ThemeIds.Normalize(id));
    }

    [Fact]
    public void IdsAreCaseInsensitive()
    {
        Assert.Equal("midnight", ThemeIds.Normalize("Midnight"));
    }

    [Fact]
    public void ResetRestoresTheShippingLook()
    {
        var settings = new WorkspaceSettings { Theme = ThemeIds.Sunset };

        settings.Reset();

        Assert.Equal(ThemeIds.Default, settings.Theme);
    }
}
