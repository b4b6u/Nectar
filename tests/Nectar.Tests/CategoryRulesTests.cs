using Nectar.Services.Categories;
using Xunit;

namespace Nectar.Tests;

/// <summary>
/// The free build's category ceiling. Three is a product decision, so it is asserted here rather than
/// left to a property that could drift: free gets three, a licence lifts the limit.
/// </summary>
public sealed class CategoryRulesTests
{
    [Fact]
    public void FreeStopsAtThree()
    {
        Assert.True(CategoryRules.CanAdd(0, isPro: false));
        Assert.True(CategoryRules.CanAdd(1, isPro: false));
        Assert.True(CategoryRules.CanAdd(2, isPro: false));
        Assert.False(CategoryRules.CanAdd(3, isPro: false));
        Assert.False(CategoryRules.CanAdd(9, isPro: false));
    }

    [Fact]
    public void AKeyLiftsTheCeiling()
    {
        Assert.True(CategoryRules.CanAdd(3, isPro: true));
        Assert.True(CategoryRules.CanAdd(40, isPro: true));
        Assert.False(CategoryRules.IsAtLimit(40, isPro: true));
        Assert.True(CategoryRules.IsAtLimit(3, isPro: false));
        Assert.False(CategoryRules.IsAtLimit(2, isPro: false));
    }

    [Fact]
    public void TheSummaryNamesTheWallBeforeItIsHit()
    {
        Assert.Equal("1 of 3 free categories used.", CategoryRules.LimitMessage(1, isPro: false));
        Assert.Equal("Free stops at 3 categories. Nectar Pro removes the limit.", CategoryRules.LimitMessage(3, isPro: false));
        Assert.Equal("You have 6 categories.", CategoryRules.LimitMessage(6, isPro: true));
    }

    [Fact]
    public void NamesAreTrimmedCollapsedAndClamped()
    {
        Assert.Equal("Deep work", CategoryRules.NormalizeName("  Deep   work  "));
        Assert.Equal(string.Empty, CategoryRules.NormalizeName("   "));
        Assert.Equal(string.Empty, CategoryRules.NormalizeName(null));

        Assert.Equal(CategoryRules.MaxNameLength, CategoryRules.NormalizeName(new string('a', 60)).Length);
    }
}
