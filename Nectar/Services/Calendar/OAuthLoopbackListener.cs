using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Nectar.Services.Calendar;

/// <summary>
/// Single-use loopback redirect for the OAuth code. A plain <see cref="TcpListener"/> is used
/// instead of HttpListener so no URL reservation (and therefore no administrator prompt) is
/// needed; only the one request the browser makes after consent is read.
/// </summary>
internal sealed class OAuthLoopbackListener : IDisposable
{
    private readonly TcpListener _listener;
    private readonly int _port;

    private OAuthLoopbackListener(TcpListener listener, int port)
    {
        _listener = listener;
        _port = port;
    }

    /// <summary>
    /// RFC 8252 loopback redirect: IP literal, no path. Google's desktop client accepts any
    /// loopback port, and Entra ignores the port once <c>http://127.0.0.1</c> is registered, so
    /// one form works for both providers without a fixed port.
    /// </summary>
    internal string RedirectUri => $"http://127.0.0.1:{_port}/";

    internal static OAuthLoopbackListener Start()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return new OAuthLoopbackListener(listener, port);
    }

    /// <summary>
    /// Blocks until the browser comes back with a code for <paramref name="expectedState"/>.
    /// Unrelated requests (favicon, health probes) are answered and ignored.
    /// </summary>
    internal async Task<string> WaitForCodeAsync(
        string expectedState,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        while (true)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("The browser did not finish the sign-in in time.");
            }

            using (client)
            {
                // One connection may not hold the sign-in hostage: anything that does not send a
                // request line within a few seconds is dropped and the listener keeps waiting.
                string requestLine;
                using (var connection = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token))
                {
                    connection.CancelAfter(TimeSpan.FromSeconds(5));

                    try
                    {
                        requestLine = await ReadRequestLineAsync(client, connection.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!deadline.IsCancellationRequested)
                    {
                        continue;
                    }
                }

                var target = requestLine.Split(' ').ElementAtOrDefault(1) ?? "/";
                var query = ParseQuery(target);

                if (query.TryGetValue("error", out var error))
                {
                    await RespondAsync(client, "Sign-in was cancelled. You can close this tab.").ConfigureAwait(false);
                    throw new InvalidOperationException($"The provider refused the sign-in: {error}.");
                }

                if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
                {
                    await RespondAsync(client, "Waiting for Nectar to finish linking…").ConfigureAwait(false);
                    continue;
                }

                if (!query.TryGetValue("state", out var state) || state != expectedState)
                {
                    await RespondAsync(client, "This sign-in response did not match the request. You can close this tab.").ConfigureAwait(false);
                    continue;
                }

                await RespondAsync(client, "Nectar is linked. You can close this tab and return to the app.").ConfigureAwait(false);
                return code;
            }
        }
    }

    /// <summary>
    /// Reads the request line a byte at a time, stopping at CR/LF or at a hard length cap, so a
    /// local process cannot force a large allocation by sending an endless line.
    /// </summary>
    private static async Task<string> ReadRequestLineAsync(TcpClient client, CancellationToken cancellationToken)
    {
        const int MaxRequestLine = 2048;

        var stream = client.GetStream();
        var buffer = new byte[1];
        var line = new StringBuilder(128);

        while (line.Length < MaxRequestLine)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            if (read == 0 || buffer[0] == (byte)'\n')
            {
                break;
            }

            if (buffer[0] != (byte)'\r')
            {
                line.Append((char)buffer[0]);
            }
        }

        return line.ToString();
    }

    private static async Task RespondAsync(TcpClient client, string message)
    {
        var html = "<!doctype html><html><head><meta charset=\"utf-8\"><title>Nectar</title></head>" +
                   "<body style=\"margin:0;display:grid;place-items:center;height:100vh;background:#000;" +
                   "color:#f2f2f2;font:15px 'Segoe UI',sans-serif\">" + WebUtility.HtmlEncode(message) +
                   "</body></html>";

        var bytes = Encoding.UTF8.GetBytes(html);
        var header = "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n" +
                     $"Content-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";

        try
        {
            var stream = client.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes(header)).ConfigureAwait(false);
            await stream.WriteAsync(bytes).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException)
        {
            // The browser may close the tab before the reply lands.
        }
    }

    private static Dictionary<string, string> ParseQuery(string target)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var question = target.IndexOf('?');
        if (question < 0)
        {
            return result;
        }

        foreach (var pair in target[(question + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..equals]);
            var value = Uri.UnescapeDataString(pair[(equals + 1)..].Replace('+', ' '));
            result[key] = value;
        }

        return result;
    }

    public void Dispose()
    {
        try
        {
            _listener.Stop();
        }
        catch (Exception)
        {
            // Nothing to release beyond the socket.
        }
    }
}
