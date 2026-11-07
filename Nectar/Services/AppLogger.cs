namespace Nectar.Services;

/// <summary>
/// Tiny append-only diagnostic log at %LOCALAPPDATA%\Nectar\nectar.log.
/// Logging must never throw: a crash in diagnostics would be worse than the failure itself.
/// </summary>
public static class AppLogger
{
    /// <summary>Roll over above this size so a long-running install cannot fill the disk.</summary>
    private const long MaxLogBytes = 1_000_000;

    private static readonly object Gate = new();

    private static string LogPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nectar",
            "nectar.log");

    public static void Log(string message) => Write(message, exception: null);

    public static void Log(string message, Exception exception) => Write(message, exception);

    private static void Write(string message, Exception? exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = exception is null
                ? $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}"
                : $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}: {exception.GetType().Name}: {exception.Message}{Environment.NewLine}{exception.StackTrace}{Environment.NewLine}";

            lock (Gate)
            {
                try
                {
                    var info = new FileInfo(LogPath);
                    if (info.Exists && info.Length > MaxLogBytes)
                    {
                        File.Delete(LogPath);
                    }
                }
                catch (Exception)
                {
                    // If the old log cannot be removed, keep appending to it.
                }

                File.AppendAllText(LogPath, line);
            }
        }
        catch (Exception)
        {
            // Diagnostics are best effort only.
        }
    }
}
