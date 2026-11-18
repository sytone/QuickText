using Microsoft.Win32;

namespace QuickText;

/// <summary>"Start with Windows" through the per-user Run key. Needs no admin rights and leaves nothing behind when switched off.</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QuickText";

    /// <summary>Lets Settings reflect the real Windows startup entry rather than a stale local preference.</summary>
    public static bool IsEnabled
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string;
            }
            catch { return false; }
        }
    }

    /// <summary>Lets the UI report denied registry writes instead of claiming startup was changed.</summary>
    public static bool TrySet(bool enabled)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
            {
                string? exe = Environment.ProcessPath;
                if (exe == null) return false;
                key.SetValue(ValueName, $"\"{exe}\" --hidden"); // start quietly in the tray, no window popping up at logon
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
