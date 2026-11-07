namespace Nectar.Services;

/// <summary>Stand-in for the app's logger so the compiled sources do not need WinUI.</summary>
internal static class AppLogger
{
    internal static void Log(string message)
    {
    }

    internal static void Log(string message, Exception exception)
    {
    }
}
