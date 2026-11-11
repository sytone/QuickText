using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace QuickText;

public sealed class AppSettings
{
    public bool Autosave { get; set; } = true;
    public string NoteFontFamily { get; set; } = "Segoe UI";
    public string CopyHotkey { get; set; } = "CTRL+SHIFT+C";
    public string CloseHotkey { get; set; } = "ESCAPE";
    public string ExitHotkey { get; set; } = "CTRL+Q";
    // Ctrl+Alt+letter is AltGr+letter on many layouts (e.g. German "@"), so the default avoids it.
    public string GlobalHotkey { get; set; } = "CTRL+SHIFT+SPACE";
    public double Opacity { get; set; } = 1.0;
    public double Width { get; set; } = 520;
    public double Height { get; set; } = 280;
    public double? Left { get; set; }
    public double? Top { get; set; }
}

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
    public static NoteStore CreateNoteStore() => new(Dir);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

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
        return settings;
    }

    public static void SaveSettings(AppSettings settings) =>
        AtomicWrite(SettingsPath, JsonSerializer.Serialize(settings, Json));

    // Write to a temp file first so a crash mid-write can never leave a half-written note.
    private static void AtomicWrite(string path, string content)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, content);
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to write {path}: {ex.Message}");
        }
    }

    /// <summary>Appends an unexpected error to crash.log next to the settings, so users can attach it to a bug report.</summary>
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
