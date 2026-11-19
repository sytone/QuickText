using System.Runtime.InteropServices;

namespace QuickText;

/// <summary>
/// Notification-area icon built directly on the Shell API, so no WinForms assembly has to be loaded.
/// It is the visible anchor while the window is hidden: click opens the window, right-click shows a small menu.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    public const int CallbackMessage = 0x8001; // WM_APP + 1

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_NULL = 0x0000;

    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    private const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;
    private const uint MF_STRING = 0x0000, MF_SEPARATOR = 0x0800;
    private const uint TPM_RIGHTBUTTON = 0x0002, TPM_RETURNCMD = 0x0100, TPM_BOTTOMALIGN = 0x0020;
    private const uint MenuToggle = 1, MenuExit = 2;

    private readonly IntPtr _hwnd;
    private readonly uint _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private readonly Action _toggle;
    private readonly Action _exit;
    private IntPtr _icon;
    private string _tip = "QuickText";
    private bool _added;

    /// <summary>Keeps the tray's recovery hint consistent with the currently registered shortcut.</summary>
    public string ShortcutText { get; set; } = "";

    /// <summary>Routes tray actions through the main window so hidden and compact states share recovery.</summary>
    public TrayIcon(IntPtr hwnd, Action toggle, Action exit)
    {
        _hwnd = hwnd;
        _toggle = toggle;
        _exit = exit;

        string? exe = Environment.ProcessPath;
        if (exe != null) ExtractIconEx(exe, 0, IntPtr.Zero, out _icon, 1);
    }

    /// <summary>Registers the recovery icon initially and again if Explorer restarts.</summary>
    public void Add()
    {
        _added = Notify(NIM_ADD);
    }

    /// <summary>Keeps recovery instructions inside the shell's tooltip length limit.</summary>
    public void SetTip(string tip)
    {
        _tip = tip.Length > 127 ? tip[..127] : tip;
        if (_added) Notify(NIM_MODIFY);
    }

    /// <summary>Separates tray messages from window input and recovers the icon after an Explorer restart.</summary>
    public bool TryHandle(int msg, IntPtr lParam)
    {
        if (msg == _taskbarCreated && _taskbarCreated != 0)
        {
            Add(); // Explorer restarted, the icon has to be registered again
            return true;
        }
        if (msg != CallbackMessage) return false;

        switch ((int)(lParam.ToInt64() & 0xFFFF))
        {
            case WM_LBUTTONUP: _toggle(); break;
            case WM_RBUTTONUP: ShowMenu(); break;
        }
        return true;
    }

    private void ShowMenu()
    {
        IntPtr menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;
        try
        {
            string toggleText = ShortcutText.Length > 0 ? $"Open QuickText\t{ShortcutText}" : "Open QuickText";
            AppendMenuW(menu, MF_STRING, MenuToggle, toggleText);
            AppendMenuW(menu, MF_SEPARATOR, 0, null);
            AppendMenuW(menu, MF_STRING, MenuExit, "Exit QuickText");

            GetCursorPos(out POINT pt);
            SetForegroundWindow(_hwnd); // otherwise the menu does not close when clicking elsewhere
            uint cmd = TrackPopupMenu(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_BOTTOMALIGN, pt.x, pt.y, 0, _hwnd, IntPtr.Zero);
            PostMessageW(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            if (cmd == MenuToggle) _toggle();
            else if (cmd == MenuExit) _exit();
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private bool Notify(uint message)
    {
        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = CallbackMessage,
            hIcon = _icon,
            szTip = _tip
        };
        return Shell_NotifyIconW(message, ref data);
    }

    /// <summary>Removes the shell entry and releases the native icon so exit leaves no stale tray item.</summary>
    public void Dispose()
    {
        if (_added)
        {
            var data = new NOTIFYICONDATA { cbSize = Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = _hwnd, uID = 1 };
            Shell_NotifyIconW(NIM_DELETE, ref data);
            _added = false;
        }
        if (_icon != IntPtr.Zero)
        {
            DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x, y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, IntPtr large, out IntPtr small, uint count);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string name);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint flags, uint id, string? text);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr hMenu, uint flags, int x, int y, int reserved, IntPtr hWnd, IntPtr rect);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessageW(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
