namespace Nectar.Services.Entitlement;

/// <summary>
/// What the app is allowed to do. The free build satisfies this with a service that is always free;
/// a Pro build swaps in the Dodo-backed implementation. Nothing outside this seam knows which one is
/// running, which is what lets the public repository compile without a line of Pro code in it.
/// </summary>
internal interface IEntitlementService
{
    /// <summary>Raised when the tier changes, so open windows can repaint their Pro marks.</summary>
    event EventHandler? Changed;

    bool IsPro { get; }

    string PlanLabel { get; }

    string StatusLabel { get; }

    string? MaskedKey { get; }

    Task<string> ActivateAsync(string rawKey, CancellationToken cancellationToken = default);

    Task RefreshAsync(CancellationToken cancellationToken = default);

    void Deactivate();
}

internal static class Entitlements
{
    /// <summary>One per process: the settings window and the view model must agree.</summary>
    internal static IEntitlementService Current { get; } =
#if NECTAR_PRO
        new Pro.Licence.ProEntitlementService();
#else
        new FreeEntitlementService();
#endif
}

/// <summary>
/// The free build's answer to every question: no, and here is where to buy one. This is MIT code
/// and it stays that way; it holds no key, makes no network call and cannot be coaxed into Pro.
/// </summary>
internal sealed class FreeEntitlementService : IEntitlementService
{
    // Nothing can change in a build with no licence code, so there is nothing to raise.
#pragma warning disable CS0067
    public event EventHandler? Changed;
#pragma warning restore CS0067

    public bool IsPro => false;

    public string PlanLabel => "Nectar Free";

    public string StatusLabel => "This build is the free tier. Pro keys are activated in the Pro build.";

    public string? MaskedKey => null;

    public Task<string> ActivateAsync(string rawKey, CancellationToken cancellationToken = default) =>
        Task.FromResult("This is the free build, so there is no licence to activate.");

    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Deactivate()
    {
    }
}
