using System.Diagnostics;
using System.Net.Http;

namespace Nectar.Services;

/// <summary>
/// Watches every <see cref="HttpClient"/> request the process starts and logs a warning when the
/// destination is not a calendar provider. Nectar is local-only by design; this makes an accidental
/// telemetry, update or analytics call visible in nectar.log instead of silent.
///
/// It is a tripwire, not a firewall: the request is not blocked. Blocking here would break the
/// calendar silently, while a warning names the host and keeps the unexpected call diagnosable.
/// </summary>
internal static class NetworkGuard
{
    /// <summary>Exactly the hosts the calendar code is allowed to use; no suffix or wildcard matching.</summary>
    private static readonly string[] AllowedHosts =
    {
        "accounts.google.com",
        "oauth2.googleapis.com",
        "www.googleapis.com",
        "login.microsoftonline.com",
        "graph.microsoft.com",
    };

    private static readonly object Gate = new();

    private static bool _started;
    private static Action<string> _warn = AppLogger.Log;
    private static IDisposable? _httpListener;

    /// <summary>
    /// Subscribes once per process. Safe to call before any HttpClient exists: AllListeners
    /// replays existing listeners to a new subscriber.
    /// </summary>
    internal static void Start(Action<string>? warn = null)
    {
        lock (Gate)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            if (warn is not null)
            {
                _warn = warn;
            }

            DiagnosticListener.AllListeners.Subscribe(new ListenerObserver());
        }
    }

    /// <summary>True only for https requests to a known calendar host.</summary>
    internal static bool IsAllowed(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps &&
        AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);

    internal static void Inspect(object? payload)
    {
        var request = payload as HttpRequestMessage ?? ReadRequest(payload);
        if (request?.RequestUri is not { } uri || IsAllowed(uri))
        {
            return;
        }

        // Host only: a URL can carry a query string, and this line goes to disk.
        _warn($"Network guard: unexpected request to {uri.Scheme}://{uri.Host} — only calendar providers should be contacted.");
    }

    /// <summary>HttpClient's diagnostic payload is an anonymous <c>{ Request }</c> object.</summary>
    private static HttpRequestMessage? ReadRequest(object? payload)
    {
        if (payload is null)
        {
            return null;
        }

        try
        {
            return payload.GetType().GetProperty("Request")?.GetValue(payload) as HttpRequestMessage;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private sealed class ListenerObserver : IObserver<DiagnosticListener>
    {
        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name != "HttpHandlerDiagnosticListener")
            {
                return;
            }

            lock (Gate)
            {
                _httpListener?.Dispose();
                _httpListener = listener.Subscribe(new RequestObserver());
            }
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }

    private sealed class RequestObserver : IObserver<KeyValuePair<string, object?>>
    {
        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == "System.Net.Http.HttpRequestOut.Start")
            {
                Inspect(value.Value);
            }
        }

        public void OnError(Exception error)
        {
        }

        public void OnCompleted()
        {
        }
    }
}
