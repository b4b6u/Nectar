using System.Runtime.InteropServices;
using System.Text;

namespace Nectar.Services;

/// <summary>
/// Writes fatal and near-fatal exceptions to %LOCALAPPDATA%\Nectar\crashlog.txt.
///
/// This is deliberately separate from <see cref="AppLogger"/>: nectar.log rolls over at 1 MB and is
/// noisy, while crashlog.txt is append-only and reserved for exceptions, so a user can hand over one
/// short file. Nothing here may throw — a failure in diagnostics must never mask the real crash.
/// </summary>
internal static class CrashReporter
{
    private const long MaxCrashLogBytes = 512_000;

    private static readonly object Gate = new();

    internal static string CrashLogPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nectar",
            "crashlog.txt");

    internal static void Write(string context, Exception? exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(CrashLogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var report = new StringBuilder();
            report.AppendLine($"===== {DateTimeOffset.Now:O} =====");
            report.AppendLine($"context : {context}");
            report.AppendLine(
                $"build   : {typeof(CrashReporter).Assembly.GetName().Version} " +
                $"({RuntimeInformation.ProcessArchitecture})");
            report.AppendLine($"os      : {Environment.OSVersion.VersionString}");

            if (exception is null)
            {
                report.AppendLine("exception: (none supplied)");
            }
            else
            {
                var depth = 0;
                for (var current = exception; current is not null; current = current.InnerException)
                {
                    var label = depth == 0 ? "exception" : $"inner[{depth}]";
                    report.AppendLine($"{label} : {current.GetType().FullName}: {current.Message}");
                    report.AppendLine(current.StackTrace ?? "  (no stack trace)");
                    depth++;
                }
            }

            report.AppendLine();

            lock (Gate)
            {
                try
                {
                    var info = new FileInfo(CrashLogPath);
                    if (info.Exists && info.Length > MaxCrashLogBytes)
                    {
                        File.Delete(CrashLogPath);
                    }
                }
                catch (Exception)
                {
                    // Keep appending if the old file cannot be removed.
                }

                File.AppendAllText(CrashLogPath, report.ToString());
            }
        }
        catch (Exception)
        {
            // Diagnostics are best effort only.
        }
    }
}
