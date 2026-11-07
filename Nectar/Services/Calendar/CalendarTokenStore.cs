using System.Text.Json;
using Nectar.Interop;

namespace Nectar.Services.Calendar;

/// <summary>
/// Persists the linked calendar credential as a DPAPI-protected blob next to the database.
/// The access/refresh tokens never reach settings.json, the log or the UI.
/// </summary>
internal sealed class CalendarTokenStore
{
    private readonly string _path;

    internal CalendarTokenStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nectar");

        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "calendar-auth.bin");
    }

    internal (CalendarProviderKind Provider, CalendarTokens Tokens)? Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var plain = DataProtection.Unprotect(File.ReadAllBytes(_path));
            if (plain is null)
            {
                AppLogger.Log("Saved calendar credential could not be decrypted; ignoring it.");
                return null;
            }

            var saved = JsonSerializer.Deserialize<SavedConnection>(plain);
            if (saved is null || string.IsNullOrWhiteSpace(saved.AccessToken))
            {
                return null;
            }

            var provider = Enum.TryParse<CalendarProviderKind>(saved.Provider, ignoreCase: true, out var parsed)
                ? parsed
                : CalendarProviderKind.None;

            if (provider == CalendarProviderKind.None)
            {
                return null;
            }

            return (provider, new CalendarTokens(
                saved.AccessToken!,
                saved.RefreshToken ?? string.Empty,
                saved.ExpiresAt,
                saved.ClientId ?? string.Empty,
                string.IsNullOrWhiteSpace(saved.Tenant) ? "common" : saved.Tenant!));
        }
        catch (Exception ex)
        {
            AppLogger.Log("Saved calendar credential could not be read", ex);
            return null;
        }
    }

    internal void Save(CalendarProviderKind provider, CalendarTokens tokens)
    {
        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(new SavedConnection
            {
                Provider = provider.ToString(),
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                ExpiresAt = tokens.ExpiresAt,
                ClientId = tokens.ClientId,
                Tenant = tokens.Tenant,
            });

            var sealedBytes = DataProtection.Protect(payload);
            if (sealedBytes is null)
            {
                AppLogger.Log("Calendar credential could not be encrypted; it was not saved.");
                return;
            }

            AtomicFile.WriteAllBytes(_path, sealedBytes);
        }
        catch (Exception ex)
        {
            AppLogger.Log("Calendar credential could not be saved", ex);
        }
    }

    internal void Clear()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("Calendar credential could not be cleared", ex);
        }
    }

    private sealed class SavedConnection
    {
        public string Provider { get; set; } = "None";

        public string? AccessToken { get; set; }

        public string? RefreshToken { get; set; }

        public DateTimeOffset ExpiresAt { get; set; }

        public string? ClientId { get; set; }

        public string? Tenant { get; set; }
    }
}
