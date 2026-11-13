using System.Reflection;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;

namespace Nectar.Services;

/// <summary>
/// Gives the packaged app a desktop shortcut. An MSIX cannot run install-time scripts, so the
/// shortcut is created on first launch instead; the Start-menu entry comes from the manifest.
///
/// The shortcut targets <c>explorer.exe shell:AppsFolder\&lt;PackageFamily&gt;!Nectar</c>, which is
/// the documented way to launch a packaged app from a .lnk. Nothing happens in the portable build,
/// where there is no package identity to point at.
/// </summary>
internal static class DesktopShortcut
{
    private const string ShortcutName = "Nectar.lnk";

    internal static void Ensure()
    {
        if (!TryGetLaunchTarget(out var aumid, out var iconPath))
        {
            return;
        }

        object? shell = null;
        object? shortcut = null;

        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var path = Path.Combine(desktop, ShortcutName);
            if (File.Exists(path))
            {
                return;
            }

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return;
            }

            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: new object[] { path });

            if (shortcut is null)
            {
                return;
            }

            var shortcutType = shortcut.GetType();
            Set(shortcutType, shortcut, "TargetPath", "explorer.exe");
            Set(shortcutType, shortcut, "Arguments", $"shell:AppsFolder\\{aumid}");
            Set(shortcutType, shortcut, "Description", "Nectar workspace");
            Set(shortcutType, shortcut, "WorkingDirectory", Path.GetDirectoryName(iconPath) ?? string.Empty);
            if (File.Exists(iconPath))
            {
                Set(shortcutType, shortcut, "IconLocation", iconPath);
            }

            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            AppLogger.Log($"Desktop shortcut created at {path}.");
        }
        catch (Exception ex)
        {
            // A shortcut is a convenience; it must never stop the app from starting.
            AppLogger.Log("Could not create the desktop shortcut", ex);
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
            {
                Marshal.ReleaseComObject(shortcut);
            }

            if (shell is not null && Marshal.IsComObject(shell))
            {
                Marshal.ReleaseComObject(shell);
            }
        }
    }

    private static void Set(Type type, object target, string property, string value) =>
        type.InvokeMember(property, BindingFlags.SetProperty, null, target, new object[] { value });

    private static bool TryGetLaunchTarget(out string aumid, out string iconPath)
    {
        aumid = string.Empty;
        iconPath = string.Empty;

        try
        {
            var package = Package.Current;
            aumid = $"{package.Id.FamilyName}!Nectar";
            iconPath = Path.Combine(package.InstalledLocation.Path, "Assets", "nectar.ico");
            return true;
        }
        catch (Exception)
        {
            // Unpackaged: Package.Current throws. There is no identity to launch by AUMID.
            return false;
        }
    }
}
