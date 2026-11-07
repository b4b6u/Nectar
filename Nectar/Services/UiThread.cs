using Microsoft.UI.Dispatching;

namespace Nectar.Services;

/// <summary>
/// The one supported way to move work from a background thread or a native callback onto the UI
/// thread.
///
/// <see cref="DispatcherQueue.TryEnqueue(DispatcherQueueHandler)"/> is fire-and-forget: if the
/// callback throws, the exception is not handed to <c>App.UnhandledException</c> in a way the app
/// can survive, and the process can disappear with nothing in the log. Every hop therefore goes
/// through <see cref="Post"/>, which names the call site and records the failure.
/// </summary>
internal static class UiThread
{
    /// <summary>
    /// Queues <paramref name="action"/> on <paramref name="queue"/>, catching anything it throws.
    /// Returns false when the queue refused the work (typically during shutdown), which callers can
    /// ignore: dropping a UI refresh as the app exits is correct behaviour.
    /// </summary>
    internal static bool Post(DispatcherQueue? queue, Action action, string context)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (queue is null)
        {
            AppLogger.Log($"{context}: no DispatcherQueue available; update dropped.");
            return false;
        }

        var queued = queue.TryEnqueue(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                // Marshalled work runs outside any caller's try/catch, so this is the last net.
                AppLogger.Log($"{context}: UI update failed", ex);
                CrashReporter.Write($"{context} (queued UI callback)", ex);
            }
        });

        if (!queued)
        {
            AppLogger.Log($"{context}: DispatcherQueue rejected the update (shutting down?).");
        }

        return queued;
    }
}
