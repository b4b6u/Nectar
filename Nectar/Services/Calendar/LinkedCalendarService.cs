using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Nectar.Services.Calendar;

/// <summary>
/// The one linked calendar: Google Calendar or Microsoft Calendar, never both. Linking runs the
/// OAuth code flow in the user's browser and returns through a loopback port; afterwards the
/// service refreshes its own token and reads today's events from the provider's REST API.
/// </summary>
public sealed class LinkedCalendarService
{
    /// <summary>Calendar text is provider-controlled, so it is trimmed before it is stored or shown.</summary>
    private const int MaxTextLength = 200;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly CalendarTokenStore _store = new();
    private CalendarTokens? _tokens;
    private CalendarProviderKind _provider = CalendarProviderKind.None;

    public LinkedCalendarService()
    {
        if (_store.Load() is { } saved)
        {
            _tokens = saved.Tokens;
            _provider = saved.Provider;
        }
    }

    public CalendarProviderKind Provider => _provider;

    public bool IsLinked => _provider != CalendarProviderKind.None && _tokens is not null;

    public string ProviderName => CalendarOAuth.ProviderName(_provider);

    /// <summary>Last read problem; null while the last read succeeded or no calendar is linked.</summary>
    public string? LastError { get; private set; }

    /// <summary>Whether a client id is already stored, so the panel can pre-fill it.</summary>
    public string SavedClientId => _tokens?.ClientId ?? string.Empty;

    public string SavedTenant => _tokens?.Tenant ?? "common";

    /// <summary>Opens the browser and waits for the provider to return a code.</summary>
    public async Task<CalendarConnectResult> ConnectAsync(
        CalendarProviderKind provider,
        string clientId,
        string tenant,
        CancellationToken cancellationToken = default)
    {
        if (provider is not (CalendarProviderKind.Google or CalendarProviderKind.Microsoft))
        {
            return new CalendarConnectResult(false, "Choose Google or Microsoft first.");
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            return new CalendarConnectResult(false, "Paste the OAuth client ID for the app registration first.");
        }

        var name = CalendarOAuth.ProviderName(provider);

        try
        {
            using var listener = OAuthLoopbackListener.Start();
            var (verifier, challenge) = CalendarOAuth.CreatePkce();
            var state = CalendarOAuth.CreateState();
            var redirectUri = listener.RedirectUri;

            var url = CalendarOAuth.BuildAuthorizeUrl(provider, clientId.Trim(), tenant, redirectUri, state, challenge);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            AppLogger.Log($"{name} sign-in opened in the browser.");

            var code = await listener.WaitForCodeAsync(state, TimeSpan.FromMinutes(3), cancellationToken).ConfigureAwait(false);
            var tokens = await CalendarOAuth
                .ExchangeCodeAsync(provider, clientId.Trim(), tenant, redirectUri, verifier, code, cancellationToken)
                .ConfigureAwait(false);

            _store.Save(provider, tokens);
            _tokens = tokens;
            _provider = provider;
            LastError = null;

            AppLogger.Log($"{name} calendar linked.");
            return new CalendarConnectResult(true, $"{name} linked.");
        }
        catch (Exception ex)
        {
            AppLogger.Log($"{name} calendar could not be linked", ex);
            return new CalendarConnectResult(false, ex.Message);
        }
    }

    public void Disconnect()
    {
        _store.Clear();
        _tokens = null;
        _provider = CalendarProviderKind.None;
        LastError = null;
        AppLogger.Log("Calendar disconnected.");
    }

    /// <summary>Today's events in local time, sorted; empty (with <see cref="LastError"/>) on failure.</summary>
    public async Task<IReadOnlyList<CalendarEvent>> GetTodayAsync(CancellationToken cancellationToken = default)
    {
        if (!IsLinked)
        {
            LastError = null;
            return Array.Empty<CalendarEvent>();
        }

        try
        {
            var accessToken = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

            var events = _provider == CalendarProviderKind.Google
                ? await FetchGoogleAsync(accessToken, cancellationToken).ConfigureAwait(false)
                : await FetchMicrosoftAsync(accessToken, cancellationToken).ConfigureAwait(false);

            LastError = null;
            return events.OrderBy(e => e.Start).ToList();
        }
        catch (Exception ex)
        {
            LastError = $"Calendar could not be read: {ex.Message}";
            AppLogger.Log("Calendar events could not be read", ex);
            return Array.Empty<CalendarEvent>();
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var tokens = _tokens ?? throw new InvalidOperationException("No calendar is linked.");

        if (tokens.ExpiresAt > DateTimeOffset.Now.AddMinutes(2))
        {
            return tokens.AccessToken;
        }

        var refreshed = await CalendarOAuth.RefreshAsync(_provider, tokens, cancellationToken).ConfigureAwait(false);
        _tokens = refreshed;
        _store.Save(_provider, refreshed);
        return refreshed.AccessToken;
    }

    private static async Task<List<CalendarEvent>> FetchGoogleAsync(string accessToken, CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(DateTime.Today);
        var end = start.AddDays(1);

        var url = "https://www.googleapis.com/calendar/v3/calendars/primary/events" +
                  "?singleEvents=true&orderBy=startTime&maxResults=50" +
                  $"&timeMin={Uri.EscapeDataString(start.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture))}" +
                  $"&timeMax={Uri.EscapeDataString(end.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture))}";

        var body = await GetJsonAsync(url, accessToken, cancellationToken).ConfigureAwait(false);
        var results = new List<CalendarEvent>();

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (!TryReadGoogleTime(item, "start", out var eventStart) ||
                !TryReadGoogleTime(item, "end", out var eventEnd))
            {
                continue;
            }

            if (eventStart >= end || eventEnd <= start)
            {
                continue;
            }

            var id = item.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            var subject = item.TryGetProperty("summary", out var summary) ? summary.GetString() : null;
            var location = item.TryGetProperty("location", out var place) ? place.GetString() : null;

            results.Add(new CalendarEvent(
                string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id!,
                eventStart,
                eventEnd,
                ClampText(subject, "(untitled)"),
                ClampText(location, string.Empty)));
        }

        return results;
    }

    private static async Task<List<CalendarEvent>> FetchMicrosoftAsync(string accessToken, CancellationToken cancellationToken)
    {
        var start = DateTime.Today;
        var end = start.AddDays(1);

        var url = "https://graph.microsoft.com/v1.0/me/calendarView" +
                  $"?startDateTime={Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture))}" +
                  $"&endDateTime={Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture))}" +
                  "&$select=id,subject,start,end,location&$top=50";

        var body = await GetJsonAsync(url, accessToken, cancellationToken).ConfigureAwait(false);
        var results = new List<CalendarEvent>();

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("value", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return results;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (!TryReadGraphTime(item, "start", out var eventStart) ||
                !TryReadGraphTime(item, "end", out var eventEnd))
            {
                continue;
            }

            if (eventStart >= end || eventEnd <= start)
            {
                continue;
            }

            var id = item.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            var subject = item.TryGetProperty("subject", out var subjectElement) ? subjectElement.GetString() : null;

            var location = string.Empty;
            if (item.TryGetProperty("location", out var place) &&
                place.TryGetProperty("displayName", out var displayName))
            {
                location = displayName.GetString() ?? string.Empty;
            }

            results.Add(new CalendarEvent(
                string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id!,
                eventStart,
                eventEnd,
                ClampText(subject, "(untitled)"),
                ClampText(location, string.Empty)));
        }

        return results;
    }

    private static string ClampText(string? value, string fallback)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return fallback;
        }

        return text.Length > MaxTextLength ? text[..MaxTextLength] : text;
    }

    private static bool TryReadGoogleTime(JsonElement item, string property, out DateTimeOffset value)
    {
        value = default;
        if (!item.TryGetProperty(property, out var block))
        {
            return false;
        }

        if (block.TryGetProperty("dateTime", out var dateTime) &&
            DateTimeOffset.TryParse(dateTime.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            value = parsed.ToLocalTime();
            return true;
        }

        // All-day events carry a bare date.
        if (block.TryGetProperty("date", out var date) &&
            DateTime.TryParseExact(date.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            value = new DateTimeOffset(day);
            return true;
        }

        return false;
    }

    private static bool TryReadGraphTime(JsonElement item, string property, out DateTimeOffset value)
    {
        value = default;
        if (!item.TryGetProperty(property, out var block) ||
            !block.TryGetProperty("dateTime", out var dateTime))
        {
            return false;
        }

        // graph returns UTC when no Prefer: outlook.timezone header is sent.
        if (!DateTimeOffset.TryParse(
                dateTime.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return false;
        }

        value = parsed.ToLocalTime();
        return true;
    }

    private static async Task<string> GetJsonAsync(string url, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var snippet = body.Length > 280 ? body[..280] : body;
            throw new InvalidOperationException($"{(int)response.StatusCode} {response.ReasonPhrase}: {snippet}");
        }

        return body;
    }
}
