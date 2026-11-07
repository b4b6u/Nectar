using System.Text.Json;

namespace Nectar.Settings;

/// <summary>One workspace card: identity plus whether it is currently shown.</summary>
public sealed class CardPreference
{
    public string Id { get; set; } = string.Empty;

    public bool Visible { get; set; } = true;
}

/// <summary>
/// User preferences. Persisted as JSON next to the database (settings.json) so the schema stays
/// independent of the SQLite tables - a preference change can never corrupt task data.
/// </summary>
public sealed class WorkspaceSettings
{
    public const string TasksCard = "tasks";
    public const string TimerCard = "timer";
    public const string NotesCard = "notes";
    public const string EventsCard = "events";

    /// <summary>Alt+N, the shipping gesture.</summary>
    public const int DefaultHotKeyModifiers = 0x0001;

    public const int DefaultHotKeyKey = 0x4E;

    public List<CardPreference> Cards { get; set; } = CreateDefaultCards();

    public int DefaultFocusMinutes { get; set; } = 25;

    /// <summary>
    /// Whether a finished focus session plays the bird call. This is the single completion
    /// signal; the timer service itself plays nothing.
    /// </summary>
    public bool CompletionSound { get; set; } = true;

    /// <summary>
    /// Whether a due calendar reminder rings. Independent of <see cref="CompletionSound"/> so
    /// either signal can be silenced without touching the other.
    /// </summary>
    public bool ReminderSound { get; set; } = true;

    /// <summary>Chime loudness for both sounds, 0-100; 0 is silent.</summary>
    public int ChimeVolume { get; set; } = 80;

    /// <summary>Whether 00:00 may drop the compact expiry tray. The chime is unaffected.</summary>
    public bool ExpiryTray { get; set; } = true;

    /// <summary>The extend chip last chosen on the tray (5, 10 or 15 minutes).</summary>
    public int ExtendMinutes { get; set; } = 5;

    /// <summary>Win32 modifier mask for the open gesture (MOD_ALT / MOD_CONTROL / MOD_SHIFT / MOD_WIN).</summary>
    public int HotKeyModifiers { get; set; } = DefaultHotKeyModifiers;

    /// <summary>Virtual-key code for the open gesture.</summary>
    public int HotKeyKey { get; set; } = DefaultHotKeyKey;

    /// <summary>Whether entering the top-edge trigger zone opens the workspace.</summary>
    public bool HoverToOpen { get; set; } = true;

    /// <summary>Whether the collapsed notch pill is shown. Off keeps Nectar entirely hidden.</summary>
    public bool ShowPill { get; set; } = true;

    /// <summary>Public OAuth client id for the linked calendar (never a secret).</summary>
    public string CalendarClientId { get; set; } = string.Empty;

    /// <summary>Microsoft tenant: "common", a tenant id or a domain.</summary>
    public string CalendarTenant { get; set; } = "common";

    public static List<CardPreference> CreateDefaultCards() =>
        new()
        {
            new CardPreference { Id = TasksCard, Visible = true },
            new CardPreference { Id = TimerCard, Visible = true },
            new CardPreference { Id = NotesCard, Visible = true },
            new CardPreference { Id = EventsCard, Visible = true },
        };

    /// <summary>
    /// Resets the workspace customisation only. The open shortcut and the linked calendar are
    /// deliberately kept: they describe the app's plumbing, not how the cards look.
    /// </summary>
    public void Reset()
    {
        Cards = CreateDefaultCards();
        DefaultFocusMinutes = 25;
        CompletionSound = true;
        ReminderSound = true;
        ChimeVolume = 80;
        ExpiryTray = true;
        ExtendMinutes = 5;
        ShowPill = true;
    }

    /// <summary>Repairs anything a hand-edited or older settings file might be missing.</summary>
    public void Normalize()
    {
        var known = new[] { TasksCard, TimerCard, NotesCard, EventsCard };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var repaired = new List<CardPreference>();

        foreach (var card in Cards ?? new List<CardPreference>())
        {
            if (card is null || string.IsNullOrWhiteSpace(card.Id) || !known.Contains(card.Id))
            {
                continue;
            }

            if (!seen.Add(card.Id))
            {
                continue;
            }

            repaired.Add(new CardPreference { Id = card.Id, Visible = card.Visible });
        }

        foreach (var id in known)
        {
            if (seen.Add(id))
            {
                repaired.Add(new CardPreference { Id = id, Visible = true });
            }
        }

        Cards = repaired;
        DefaultFocusMinutes = Math.Clamp(DefaultFocusMinutes, 1, 180);
        ChimeVolume = Math.Clamp(ChimeVolume, 0, 100);

        if (ExtendMinutes is not (5 or 10 or 15))
        {
            ExtendMinutes = 5;
        }

        // A gesture needs at least one modifier and a real key; anything else falls back to Alt+N.
        HotKeyModifiers &= 0x000F;
        if (HotKeyModifiers == 0 || HotKeyKey is < 1 or > 254)
        {
            HotKeyModifiers = DefaultHotKeyModifiers;
            HotKeyKey = DefaultHotKeyKey;
        }

        CalendarClientId = (CalendarClientId ?? string.Empty).Trim();
        if (CalendarClientId.Length > 512)
        {
            CalendarClientId = CalendarClientId[..512];
        }

        CalendarTenant = string.IsNullOrWhiteSpace(CalendarTenant) ? "common" : CalendarTenant.Trim();
        if (CalendarTenant.Length > 253)
        {
            CalendarTenant = CalendarTenant[..253];
        }
    }
}

/// <summary>Loads and saves <see cref="WorkspaceSettings"/>. Failures fall back to defaults.</summary>
internal sealed class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;

    internal SettingsStore()
    {
        var folder = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nectar");

        Directory.CreateDirectory(folder);
        _path = System.IO.Path.Combine(folder, "settings.json");
    }

    internal string Path => _path;

    internal WorkspaceSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new WorkspaceSettings();
            }

            var json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize<WorkspaceSettings>(json) ?? new WorkspaceSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception ex)
        {
            Services.AppLogger.Log("Settings could not be read; using defaults", ex);
            return new WorkspaceSettings();
        }
    }

    internal bool TrySave(WorkspaceSettings settings)
    {
        try
        {
            settings.Normalize();
            Services.AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(settings, SerializerOptions));
            return true;
        }
        catch (Exception ex)
        {
            Services.AppLogger.Log("Settings could not be saved", ex);
            return false;
        }
    }
}
