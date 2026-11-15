using Nectar.Pro.Licence;
using Nectar.Settings;
using Xunit;

namespace Nectar.Tests;

/// <summary>
/// The paid tier is decided by a Dodo key, so these cover the four states that matter: no key,
/// a key Dodo accepts, a key Dodo rejects, and a check-in that could not happen at all.
/// </summary>
public sealed class LicenceTests
{
    private sealed class FakeClient : ILicenceClient
    {
        public bool ActivationSucceeds { get; set; } = true;
        public bool ValidationResult { get; set; } = true;
        public Exception? ValidationThrows { get; set; }
        public int ValidateCalls { get; private set; }

        public Task<LicenceActivation?> ActivateAsync(string licenceKey, string deviceName, CancellationToken cancellationToken) =>
            Task.FromResult(ActivationSucceeds ? new LicenceActivation("lki_test") : null);

        public Task<bool> ValidateAsync(string licenceKey, string instanceId, CancellationToken cancellationToken)
        {
            ValidateCalls++;
            return ValidationThrows is { } error
                ? Task.FromException<bool>(error)
                : Task.FromResult(ValidationResult);
        }
    }

    private static string TempStatePath() =>
        Path.Combine(Path.GetTempPath(), $"nectar-licence-{Guid.NewGuid():n}.json");

    [Theory]
    [InlineData(ThemeIds.Paper)]
    [InlineData(ThemeIds.Meadow)]
    [InlineData(ThemeIds.Sunset)]
    public void ThreePalettesAreFree(string id) => Assert.True(ThemeIds.IsEntitled(id, isPro: false));

    [Fact]
    public void MidnightNeedsPro()
    {
        Assert.False(ThemeIds.IsEntitled(ThemeIds.Midnight, isPro: false));
        Assert.True(ThemeIds.IsEntitled(ThemeIds.Midnight, isPro: true));
    }

    [Fact]
    public void UnentitledPaletteResolvesToPaper() =>
        Assert.Equal(ThemeIds.Paper, ThemeIds.Resolve(ThemeIds.Midnight, isPro: false));

    [Theory]
    [InlineData("  pro-aaaa-bbbb-cccc  ", "PRO-AAAA-BBBB-CCCC")]
    [InlineData("proaaaaa", "PROAAAAA")]
    [InlineData("not a key", null)]
    [InlineData("PRO_AAAA_BBBB", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void KeysAreNormalizedOrRejected(string? raw, string? expected) =>
        Assert.Equal(expected, ProEntitlementService.NormalizeKey(raw));

    [Fact]
    public async Task ActivatingAKeyTurnsProOn()
    {
        var service = new ProEntitlementService(new FakeClient(), TempStatePath());

        Assert.False(service.IsPro);
        var message = await service.ActivateAsync("PRO-AAAA-BBBB-CCCC");

        Assert.True(service.IsPro);
        Assert.Contains("Activated", message);
        Assert.StartsWith("Nectar Pro", service.PlanLabel);
    }

    [Fact]
    public async Task ARefusedKeyLeavesTheAppFree()
    {
        var service = new ProEntitlementService(new FakeClient { ActivationSucceeds = false }, TempStatePath());

        var message = await service.ActivateAsync("PRO-AAAA-BBBB-CCCC");

        Assert.False(service.IsPro);
        Assert.Contains("unknown", message);
    }

    [Fact]
    public async Task AnInvalidCheckInTurnsProOff()
    {
        var client = new FakeClient();
        var service = new ProEntitlementService(client, TempStatePath());
        await service.ActivateAsync("PRO-AAAA-BBBB-CCCC");
        Assert.True(service.IsPro);

        client.ValidationResult = false;
        await service.RefreshAsync();

        Assert.False(service.IsPro);
        Assert.Equal(1, client.ValidateCalls);
    }

    [Fact]
    public async Task AnUnreachableCheckInKeepsTheGraceWindow()
    {
        var client = new FakeClient();
        var service = new ProEntitlementService(client, TempStatePath());
        await service.ActivateAsync("PRO-AAAA-BBBB-CCCC");

        // A captive portal must not cost someone the features they paid for.
        client.ValidationThrows = new HttpRequestException("offline");
        await service.RefreshAsync();

        Assert.True(service.IsPro);
    }

    [Fact]
    public async Task TheKeySurvivesARestart()
    {
        var path = TempStatePath();
        var first = new ProEntitlementService(new FakeClient(), path);
        await first.ActivateAsync("PRO-AAAA-BBBB-CCCC");

        var second = new ProEntitlementService(new FakeClient(), path);

        Assert.True(second.IsPro);
        Assert.Equal("PRO-…CCCC", second.MaskedKey);
    }

    [Fact]
    public void ForgettingTheKeyReturnsToFree()
    {
        var service = new ProEntitlementService(new FakeClient(), TempStatePath());

        service.Deactivate();

        Assert.False(service.IsPro);
        Assert.Null(service.MaskedKey);
    }

    [Fact]
    public void AGraceWindowInThePastIsNotPro()
    {
        var expired = new ProEntitlementService.LicenceState
        {
            LicenceKey = "PRO-AAAA-BBBB-CCCC",
            InstanceId = "lki_test",
            OfflineUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        Assert.False(expired.IsActive(DateTimeOffset.UtcNow));
    }
}
