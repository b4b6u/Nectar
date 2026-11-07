namespace Nectar.Services;

/// <summary>
/// Writes small state files (settings, reminders, the encrypted calendar credential) through a
/// sibling temporary file and a replace. A crash or a full disk can then leave the previous file
/// intact instead of a half-written one that the next launch cannot parse.
/// </summary>
internal static class AtomicFile
{
    internal static void WriteAllText(string path, string contents) =>
        Write(path, temp => File.WriteAllText(temp, contents));

    internal static void WriteAllBytes(string path, byte[] bytes) =>
        Write(path, temp => File.WriteAllBytes(temp, bytes));

    private static void Write(string path, Action<string> write)
    {
        var temp = path + ".tmp";

        try
        {
            write(temp);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch (Exception)
            {
                // The caller reports the original failure; a stale temp file is harmless.
            }

            throw;
        }
    }
}
