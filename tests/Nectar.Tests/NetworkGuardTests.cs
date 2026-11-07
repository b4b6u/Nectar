using System.Diagnostics;
using System.Net.Http;
using Nectar.Services;
using Xunit;

namespace Nectar.Tests;

/// <summary>
/// The guard is the runtime proof of the offline promise: calendar hosts pass, everything else is
/// logged. These tests exercise the same subscription path HttpClient uses, without opening a socket.
/// </summary>
public sealed class NetworkGuardTests
{
    [Theory]
    [InlineData("https://accounts.google.com/o/oauth2/v2/auth")]
    [InlineData("https://oauth2.googleapis.com/token")]
    [InlineData("https://www.googleapis.com/calendar/v3/calendars/primary/events")]
    [InlineData("https://login.microsoftonline.com/common/oauth2/v2.0/token")]
    [InlineData("https://graph.microsoft.com/v1.0/me/calendarView")]
    public void CalendarHosts_AreAllowed(string url)
    {
        Assert.True(NetworkGuard.IsAllowed(new Uri(url)));
    }

    [Theory]
    [InlineData("https://example.com/collect")]
    [InlineData("https://api.github.com/repos/x/y/releases/latest")]
    [InlineData("https://nectar.app/download")]
    [InlineData("https://google.com/calendar")]
    [InlineData("https://www.googleapis.com.evil.test/steal")]
    [InlineData("https://graph.microsoft.com.evil.test/steal")]
    [InlineData("http://www.googleapis.com/calendar")] // calendar traffic must be https
    public void AnythingElse_IsNotAllowed(string url)
    {
        Assert.False(NetworkGuard.IsAllowed(new Uri(url)));
    }

    [Fact]
    public void HttpClientDiagnostics_WarnForUnknownHosts_AndStayQuietForCalendarHosts()
    {
        var warnings = new List<string>();
        using var listener = new DiagnosticListener("HttpHandlerDiagnosticListener");

        // Mirrors App.OnLaunched: subscribe once, before any request.
        NetworkGuard.Start(warnings.Add);

        using var allowed = new HttpRequestMessage(
            HttpMethod.Get,
            "https://www.googleapis.com/calendar/v3/calendars/primary/events?key=not-logged");
        listener.Write("System.Net.Http.HttpRequestOut.Start", new { Request = allowed });
        Assert.Empty(warnings);

        using var unexpected = new HttpRequestMessage(
            HttpMethod.Post,
            "https://telemetry.example.test/v1/events?token=secret");
        listener.Write("System.Net.Http.HttpRequestOut.Start", new { Request = unexpected });

        var warning = Assert.Single(warnings);
        Assert.Contains("telemetry.example.test", warning);
        Assert.DoesNotContain("secret", warning);
        Assert.DoesNotContain("?", warning);
    }
}
