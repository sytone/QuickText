using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace QuickText;

/// <summary>Owns the single system-wide hotkey and reports why a registration failed.</summary>
internal sealed class GlobalHotkey
{
    private IntPtr _hwnd;
    private int _id;

    /// <summary>Tracks ownership so cleanup releases only this app's active registration.</summary>
    public bool IsRegistered { get; private set; }

    /// <summary>Protects normal typing and explains conflicts before claiming a system-wide shortcut.</summary>
    public bool TryRegister(IntPtr hwnd, int id, Gesture gesture, out string? error)
    {
        Unregister();
        error = null;

        if (!gesture.HasModifier)
        {
            error = "A global shortcut needs at least one of Ctrl, Alt, Shift or Win.";
            return false;
        }

        // A system-wide shortcut would swallow the key in every other app, so it must not be a typing combination.
        if (gesture.TypingConflict is string conflict)
        {
            error = conflict;
            return false;
        }

        uint mods = NativeMethods.MOD_NOREPEAT;
        if (gesture.Modifiers.HasFlag(ModifierKeys.Control)) mods |= NativeMethods.MOD_CONTROL;
        if (gesture.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= NativeMethods.MOD_SHIFT;
        if (gesture.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= NativeMethods.MOD_ALT;
        if (gesture.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= NativeMethods.MOD_WIN;

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(gesture.Key);
        if (vk == 0)
        {
            error = "This key cannot be used as a global shortcut.";
            return false;
        }

        if (!NativeMethods.RegisterHotKey(hwnd, id, mods, vk))
        {
            int code = Marshal.GetLastWin32Error();
            error = code == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED
                ? "This shortcut is already used by another app."
                : "Windows refused this shortcut: " + new Win32Exception(code).Message;
            return false;
        }

        _hwnd = hwnd;
        _id = id;
        IsRegistered = true;
        return true;
    }

    /// <summary>Releases the old shortcut before reassignment or exit so other apps can use it.</summary>
    public void Unregister()
    {
        if (IsRegistered) NativeMethods.UnregisterHotKey(_hwnd, _id);
        IsRegistered = false;
    }
}
