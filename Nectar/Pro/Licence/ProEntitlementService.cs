// Nectar Pro - proprietary. Not licensed under the repository's MIT licence.
// Copyright (c) 2026 b4b6u. All rights reserved. See PRO-LICENCE.md.

using System.Text.Json;
using Nectar.Interop;
using Nectar.Services;
using Nectar.Services.Entitlement;

namespace Nectar.Pro.Licence;

/// <summary>
/// What the app is allowed to do, decided by a Dodo license key rather than by a flag.
///
/// The key is the account: Dodo holds the customer, the subscription and the portal; Nectar holds a
/// key, an activation id and the last time it checked in. Once activated, Pro stays on for a grace
/// week so a laptop on a plane is not downgraded mid-flight, and a launch-time check refreshes that
/// window. A network failure never revokes — only an explicit "invalid" from Dodo does, because a
/// captive portal must not cost someone the features they paid for.
/// </summary>
internal sealed class ProEntitlementService : IEntitlementService
{
    private const int GraceDays = 7;

    private readonly ILicenceClient _client;
    private readonly string _statePath;
    private LicenceState _state;

    internal ProEntitlementService(ILicenceClient? client = null, string? statePath = null)
    {
        // NECTAR_DODO_TEST=1 points activation at test.dodopayments.com, so a key can be
        // exercised before the live product exists. It never mixes: one base URL per process.
        _client = client ?? new DodoLicenceClient(
            useTestMode: string.Equals(
                Environment.GetEnvironmentVariable("NECTAR_DODO_TEST"), "1", StringComparison.Ordinal));
        _statePath = statePath ?? DefaultStatePath;
        _state = Load(_statePath);
    }

    internal static string DefaultStatePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Nectar",
        "licence.json");

    /// <summary>Raised after a tier change so open windows can repaint the Pro marks.</summary>
    public event EventHandler? Changed;

    public bool IsPro => _state.IsActive(DateTimeOffset.UtcNow);

    public string PlanLabel => IsPro ? "Nectar Pro" : "Nectar Free";

    public string StatusLabel => _state.LicenceKey is null
        ? "No key activated — three themes and the local workspace."
        : IsPro
            ? $"Pro until {_state.OfflineUntilUtc!.Value.ToLocalTime():d MMM yyyy} without a check-in."
            : "The last check-in said this key is no longer active.";

    public string? MaskedKey => _state.LicenceKey is { Length: > 8 } key
        ? $"{key[..4]}…{key[^4..]}"
        : _state.LicenceKey;

    /// <summary>
    /// Binds a key to this installation. Returns a message for the settings window; Pro is on only
    /// when Dodo accepted the activation.
    /// </summary>
    public async Task<string> ActivateAsync(string rawKey, CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(rawKey);
        if (key is null)
        {
            return "That does not look like a licence key.";
        }

        LicenceActivation? activation;
        try
        {
            activation = await _client.ActivateAsync(key, DeviceName(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLogger.Log("Licence activation could not reach Dodo", ex);
            return "Dodo could not be reached. Check the connection and try again.";
        }

        if (activation is null)
        {
            return "That key is unknown, inactive, or already activated on too many devices.";
        }

        _state = new LicenceState
        {
            LicenceKey = key,
            InstanceId = activation.InstanceId,
            DeviceName = DeviceName(),
            ValidatedAtUtc = DateTimeOffset.UtcNow,
            OfflineUntilUtc = DateTimeOffset.UtcNow.AddDays(GraceDays)
        };

        Save();
        Changed?.Invoke(this, EventArgs.Empty);
        AppLogger.Log("Nectar Pro activated on this device.");
        return $"Activated. {PlanLabel} is on.";
    }

    /// <summary>Launch-time refresh. Silent: a failure leaves the existing grace window alone.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_state.LicenceKey is not { } key || _state.InstanceId is not { } instance)
        {
            return;
        }

        try
        {
            var valid = await _client.ValidateAsync(key, instance, cancellationToken).ConfigureAwait(false);
            if (!valid)
            {
                _state = _state with { OfflineUntilUtc = null };
                Save();
                Changed?.Invoke(this, EventArgs.Empty);
                AppLogger.Log("Licence is no longer valid; Pro features are off.");
                return;
            }

            _state = _state with
            {
                ValidatedAtUtc = DateTimeOffset.UtcNow,
                OfflineUntilUtc = DateTimeOffset.UtcNow.AddDays(GraceDays)
            };
            Save();
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // Offline, proxy, DNS, TLS: keep whatever grace is left and try again next launch.
            AppLogger.Log("Licence check skipped: Dodo was unreachable", ex);
        }
    }

    /// <summary>Forgets the key locally. Dodo's deactivate endpoint is not called: the device slot
    /// is freed from the customer portal, and calling it here would free a slot on a machine the
    /// user may still be using.</summary>
    public void Deactivate()
    {
        _state = LicenceState.Empty;
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal static string? NormalizeKey(string? raw)
    {
        var key = raw?.Trim();
        if (string.IsNullOrEmpty(key) || key.Length is < 8 or > 64)
        {
            return null;
        }

        // Keys are printed in four groups; anything with spaces or punctuation past the dash is a
        // paste accident, not a key.
        return key.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ? key.ToUpperInvariant() : null;
    }

    private static string DeviceName()
    {
        try
        {
            return $"Nectar on {Environment.MachineName}";
        }
        catch (Exception)
        {
            return "Nectar on this PC";
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
            var json = JsonSerializer.SerializeToUtf8Bytes(_state);
            var sealedBytes = DataProtection.Protect(json, "Nectar licence");
            if (sealedBytes is null)
            {
                AppLogger.Log("Licence state could not be sealed; nothing was written.");
                return;
            }

            AtomicFile.WriteAllBytes(_statePath, sealedBytes);
        }
        catch (Exception ex)
        {
            AppLogger.Log("Licence state could not be saved", ex);
        }
    }

    private static LicenceState Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return LicenceState.Empty;
            }

            var json = DataProtection.Unprotect(File.ReadAllBytes(path));
            return json is null
                ? LicenceState.Empty
                : JsonSerializer.Deserialize<LicenceState>(json) ?? LicenceState.Empty;
        }
        catch (Exception ex)
        {
            AppLogger.Log("Licence state could not be read", ex);
            return LicenceState.Empty;
        }
    }

    /// <summary>What is stored on disk, sealed with DPAPI for the current Windows account.</summary>
    internal sealed record LicenceState
    {
        internal static LicenceState Empty { get; } = new();

        public string? LicenceKey { get; init; }
        public string? InstanceId { get; init; }
        public string? DeviceName { get; init; }
        public DateTimeOffset? ValidatedAtUtc { get; init; }
        public DateTimeOffset? OfflineUntilUtc { get; init; }

        internal bool IsActive(DateTimeOffset now) =>
            LicenceKey is not null && InstanceId is not null && OfflineUntilUtc is { } until && until > now;
    }
}
