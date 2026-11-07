using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nectar.Services.Calendar;

/// <summary>
/// OAuth 2.0 authorization-code + PKCE against Google and Microsoft, for a desktop app on the
/// loopback redirect. Both providers accept public clients (no secret), which is the only kind a
/// local app can hold honestly.
/// </summary>
internal static class CalendarOAuth
{
    private const string GoogleScope = "https://www.googleapis.com/auth/calendar.readonly";
    private const string MicrosoftScope = "offline_access Calendars.Read";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    internal static string ProviderName(CalendarProviderKind provider) => provider switch
    {
        CalendarProviderKind.Google => "Google",
        CalendarProviderKind.Microsoft => "Microsoft",
        _ => "No calendar",
    };

    /// <summary>
    /// Tenant names are host names (contoso.onmicrosoft.com) or ids, and they are interpolated
    /// into a URL path. Only host characters are kept, ".." is refused so the path cannot be
    /// walked, and over-long input falls back to the safe default.
    /// </summary>
    internal static string SanitizeTenant(string? tenant)
    {
        var cleaned = new string((tenant ?? string.Empty)
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '.')
            .ToArray())
            .Trim('.');

        if (cleaned.Length == 0 || cleaned.Length > 253 || cleaned.Contains("..", StringComparison.Ordinal))
        {
            return "common";
        }

        return cleaned;
    }

    internal static (string Verifier, string Challenge) CreatePkce()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    internal static string CreateState() => Base64Url(RandomNumberGenerator.GetBytes(16));

    internal static string BuildAuthorizeUrl(
        CalendarProviderKind provider,
        string clientId,
        string tenant,
        string redirectUri,
        string state,
        string challenge) => provider switch
    {
        CalendarProviderKind.Google => "https://accounts.google.com/o/oauth2/v2/auth?" + Query(
            ("client_id", clientId),
            ("redirect_uri", redirectUri),
            ("response_type", "code"),
            ("scope", GoogleScope),
            ("code_challenge", challenge),
            ("code_challenge_method", "S256"),
            ("access_type", "offline"),
            ("prompt", "consent"),
            ("state", state)),

        CalendarProviderKind.Microsoft => $"https://login.microsoftonline.com/{SanitizeTenant(tenant)}/oauth2/v2.0/authorize?" + Query(
            ("client_id", clientId),
            ("redirect_uri", redirectUri),
            ("response_type", "code"),
            ("response_mode", "query"),
            ("scope", MicrosoftScope),
            ("code_challenge", challenge),
            ("code_challenge_method", "S256"),
            ("state", state)),

        _ => throw new ArgumentOutOfRangeException(nameof(provider)),
    };

    internal static Task<CalendarTokens> ExchangeCodeAsync(
        CalendarProviderKind provider,
        string clientId,
        string tenant,
        string redirectUri,
        string verifier,
        string code,
        CancellationToken cancellationToken) =>
        RequestTokensAsync(
            provider,
            tenant,
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = verifier,
                ["scope"] = provider == CalendarProviderKind.Google ? GoogleScope : MicrosoftScope,
            },
            clientId,
            tenant,
            previousRefreshToken: string.Empty,
            cancellationToken);

    internal static Task<CalendarTokens> RefreshAsync(
        CalendarProviderKind provider,
        CalendarTokens tokens,
        CancellationToken cancellationToken) =>
        RequestTokensAsync(
            provider,
            tokens.Tenant,
            new Dictionary<string, string>
            {
                ["client_id"] = tokens.ClientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = tokens.RefreshToken,
                ["scope"] = provider == CalendarProviderKind.Google ? GoogleScope : MicrosoftScope,
            },
            tokens.ClientId,
            tokens.Tenant,
            tokens.RefreshToken,
            cancellationToken);

    private static async Task<CalendarTokens> RequestTokensAsync(
        CalendarProviderKind provider,
        string tenant,
        Dictionary<string, string> form,
        string clientId,
        string tenantName,
        string previousRefreshToken,
        CancellationToken cancellationToken)
    {
        var endpoint = provider == CalendarProviderKind.Google
            ? "https://oauth2.googleapis.com/token"
            : $"https://login.microsoftonline.com/{SanitizeTenant(tenant)}/oauth2/v2.0/token";

        using var content = new FormUrlEncodedContent(form);
        using var response = await Http.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(DescribeError(body) ?? $"The sign-in server answered {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        var accessToken = root.TryGetProperty("access_token", out var access) ? access.GetString() : null;
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException("The sign-in server did not return an access token.");
        }

        var refreshToken = root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null;
        var expiresIn = root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var seconds)
            ? seconds
            : 3600;

        return new CalendarTokens(
            accessToken!,
            string.IsNullOrEmpty(refreshToken) ? previousRefreshToken : refreshToken!,
            DateTimeOffset.Now.AddSeconds(Math.Max(60, expiresIn - 60)),
            clientId,
            SanitizeTenant(tenantName));
    }

    /// <summary>Extracts Google's / Microsoft's error description without leaking credentials.</summary>
    private static string? DescribeError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            foreach (var field in new[] { "error_description", "error", "message" })
            {
                if (root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON: fall through to the generic message.
        }

        return null;
    }

    private static string Query(params (string Key, string Value)[] pairs) =>
        string.Join("&", pairs.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
