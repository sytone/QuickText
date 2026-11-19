using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace QuickText;

/// <summary>Keeps user choices separate from note files so restoring a workspace never changes its text.</summary>
public sealed class AppSettings
{
    /// <summary>Lets users keep temporary drafts without automatically replacing saved notes.</summary>
    public bool Autosave { get; set; } = true;
    /// <summary>Remembers a readable note font without changing the app's controls.</summary>
    public string NoteFontFamily { get; set; } = "Segoe UI";
    /// <summary>Allows copying a note without interfering with the editor's normal copy command.</summary>
    public string CopyHotkey { get; set; } = "CTRL+SHIFT+C";
    /// <summary>Provides a quick way back to other apps while QuickText stays available.</summary>
    public string CloseHotkey { get; set; } = "ESCAPE";
    /// <summary>Keeps quitting distinct from hiding so a quick dismissal does not stop the app.</summary>
    public string ExitHotkey { get; set; } = "CTRL+Q";
    /// <summary>Recovers QuickText from any app; the default avoids AltGr combinations used for typing.</summary>
    public string GlobalHotkey { get; set; } = "CTRL+SHIFT+SPACE";
    /// <summary>Lets the editor stay visible without fully covering the work behind it.</summary>
    public double Opacity { get; set; } = 1.0;
    /// <summary>Preserves the user's editor width independently of compact modes.</summary>
    public double Width { get; set; } = 520;
    /// <summary>Preserves the preferred editor height rather than temporary growth for long notes.</summary>
    public double Height { get; set; } = 280;
    /// <summary>Restores the editor's horizontal position; null lets Windows centre the first launch.</summary>
    public double? Left { get; set; }
    /// <summary>Restores the editor's vertical position without requiring a position on first launch.</summary>
    public double? Top { get; set; }

    /// <summary>Keeps a personal floating image without changing the app or tray identity; empty uses the app icon.</summary>
    public string FloatingIconPath { get; set; } = "";

    /// <summary>Allows a readable square in physical pixels; zero follows the current monitor's taskbar size.</summary>
    public double FloatingIconSize { get; set; }

    /// <summary>Keeps the floating shortcut where it was dropped without moving the editor.</summary>
    public double? FloatingIconLeft { get; set; }

    /// <summary>Remembers the shortcut's vertical position independently of the editor.</summary>
    public double? FloatingIconTop { get; set; }
}

/// <summary>Shares the data location and safe settings writes across normal saving and error recovery.</summary>
internal static class Storage
{
    // Debug builds use their own folder so development never touches real notes or hotkeys.
    // QUICKTEXT_DATA_DIR redirects everything (portable use, testing).
    private static readonly string Dir =
        Environment.GetEnvironmentVariable("QUICKTEXT_DATA_DIR") is { Length: > 0 } custom ? custom : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
#if DEBUG
        "QuickText-dev");
#else
        "QuickText");
#endif

    private static string SettingsPath => Path.Combine(Dir, "settings.json");
    /// <summary>Keeps notes in the same isolated data folder as their settings and diagnostics.</summary>
    public static NoteStore CreateNoteStore() => new(Dir);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>Restores usable preferences while retaining a corrupt file for troubleshooting.</summary>
    public static AppSettings LoadSettings()
    {
        var settings = new AppSettings();
        try
        {
            if (File.Exists(SettingsPath))
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), Json) ?? settings;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load settings: {ex.Message}");
            TryKeepCorruptCopy(SettingsPath);
        }

        settings.Opacity = double.IsFinite(settings.Opacity) ? Math.Clamp(settings.Opacity, 0.2, 1.0) : 1.0;
        if (!double.IsFinite(settings.Width) || settings.Width < 100) settings.Width = 520;
        if (!double.IsFinite(settings.Height) || settings.Height < 100) settings.Height = 280;
        if (string.IsNullOrWhiteSpace(settings.NoteFontFamily)) settings.NoteFontFamily = "Segoe UI";
        if (!double.IsFinite(settings.FloatingIconSize) ||
            (settings.FloatingIconSize != 0 && settings.FloatingIconSize is < 24 or > 128))
        {
            Debug.WriteLine("Invalid floating icon size; using the taskbar size.");
            settings.FloatingIconSize = 0;
        }
        settings.FloatingIconPath ??= "";
        if (settings.FloatingIconLeft is double x && !double.IsFinite(x) ||
            settings.FloatingIconTop is double y && !double.IsFinite(y))
        {
            Debug.WriteLine("Invalid floating icon position; using the editor position.");
            settings.FloatingIconLeft = settings.FloatingIconTop = null;
        }
        return settings;
    }

    /// <summary>Replaces settings only after a complete write so failed saves retain the last good copy.</summary>
    public static void SaveSettings(AppSettings settings) =>
        AtomicWrite(SettingsPath, JsonSerializer.Serialize(settings, Json));

    // Replace only after writing succeeds, keeping the previous settings on failure.
    private static void AtomicWrite(string path, string content)
    {
        Directory.CreateDirectory(Dir);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>Keeps bounded local diagnostics so users can explain failures without network reporting.</summary>
    public static void LogCrash(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            string path = Path.Combine(Dir, "crash.log");
            if (File.Exists(path) && new FileInfo(path).Length > 200_000) File.Delete(path);
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] QuickText {typeof(Storage).Assembly.GetName().Version}\n{ex}\n\n");
        }
        catch { /* nothing left to do */ }
    }

    private static void TryKeepCorruptCopy(string path)
    {
        try { File.Copy(path, path + ".bad", overwrite: true); } catch { /* best effort */ }
    }
}
