using System.Windows;
using System.IO;

namespace QuickText;

public partial class App : Application
{
#if DEBUG
    private const string MutexName = "QuickText.SingleInstance.Dev";
    private const string ShowEventName = "QuickText.Show.Dev";
#else
    private const string MutexName = "QuickText.SingleInstance";
    private const string ShowEventName = "QuickText.Show";
#endif

    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _showEvent;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Own identity for the taskbar, so Windows takes the icon from the window instead of a stale per-exe cache entry.
        NativeMethods.SetCurrentProcessExplicitAppUserModelID("BlazeR.QuickText");
        NativeMethods.UseDarkMenus();

        _mutex = new Mutex(true, MutexName, out _ownsMutex);
        if (!_ownsMutex)
        {
            // Another instance is running: ask it to show itself instead of opening a second window.
            try
            {
                NativeMethods.AllowSetForegroundWindow(-1);
                EventWaitHandle.OpenExisting(ShowEventName).Set();
            }
            catch { /* the other instance is shutting down */ }
            Shutdown();
            return;
        }

        // A crash must never take the note with it: log it and save what is in the editor.
        DispatcherUnhandledException += (_, args) =>
        {
            Storage.LogCrash(args.Exception);
            (MainWindow as MainWindow)?.SaveNow();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) Storage.LogCrash(ex);
        };

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        new Thread(() =>
        {
            while (_showEvent.WaitOne())
                Dispatcher.BeginInvoke(() => (MainWindow as MainWindow)?.ShowAndActivate());
        })
        { IsBackground = true }.Start();

        MainWindow window;
        try
        {
            window = new MainWindow();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Storage.LogCrash(ex);
            MessageBox.Show($"QuickText could not open its note files.\n\n{ex.Message}",
                "Note file error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        MainWindow = window;

        // Started by Windows at logon: create the window (hotkey and tray need it) but keep it hidden.
        if (e.Args.Contains("--hidden")) new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
        else window.Show();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        (MainWindow as MainWindow)?.SaveNow(); // logoff / shutdown: do not rely on the window closing in time
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex) _mutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
