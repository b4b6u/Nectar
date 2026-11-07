namespace Nectar.Services.Calendar;

/// <summary>The calendars Nectar can link. One at a time: connecting another replaces the first.</summary>
public enum CalendarProviderKind
{
    None,
    Google,
    Microsoft,
}

/// <summary>One appointment read from a linked calendar.</summary>
public sealed record CalendarEvent(
    string Id,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Subject,
    string Location);

/// <summary>OAuth material kept between launches. RefreshToken is never logged.</summary>
public sealed record CalendarTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    string ClientId,
    string Tenant);

/// <summary>Outcome of a link attempt; <see cref="Message"/> is safe to show in the panel.</summary>
public sealed record CalendarConnectResult(bool Ok, string Message);
