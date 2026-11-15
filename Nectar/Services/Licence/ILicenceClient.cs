namespace Nectar.Services.Licence;

/// <summary>
/// The two Dodo endpoints a desktop build needs. Both are public on Dodo's side — no API key, no
/// client secret, nothing to leak from the binary — so the app can talk to them directly instead
/// of through a server of ours.
/// </summary>
internal interface ILicenceClient
{
    /// <summary>
    /// Records this installation against a key and returns Dodo's activation id (<c>lki_…</c>).
    /// Returns null for a key that is unknown, inactive or out of activations.
    /// </summary>
    Task<LicenceActivation?> ActivateAsync(string licenceKey, string deviceName, CancellationToken cancellationToken);

    /// <summary>True while the key is still active and this installation is still recorded.</summary>
    Task<bool> ValidateAsync(string licenceKey, string instanceId, CancellationToken cancellationToken);
}

internal sealed record LicenceActivation(string InstanceId);
