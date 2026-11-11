using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using QuickText;
using ICSharpCode.AvalonEdit.Highlighting;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        string directory = Path.Combine(Path.GetTempPath(), "QuickText-ui-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Environment.SetEnvironmentVariable("QUICKTEXT_DATA_DIR", directory);
        File.WriteAllText(Path.Combine(directory, "notes.txt"), "# Legacy backup");
        // Avoid registering a real global shortcut when layout creates a native handle.
        File.WriteAllText(Path.Combine(directory, "settings.json"), "{\"globalHotkey\":\"\"}");
        // Use the real resources without starting the production mutex/tray application.
        Application app = new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XDocument source = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "App.resources.xaml"));
        XElement resources = new(presentation + "ResourceDictionary",
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            source.Root!.Element(presentation + "Application.Resources")!.Elements());
        app.Resources = (ResourceDictionary)XamlReader.Parse(resources.ToString());
        try
        {
            MainWindow window = new() { Left = 0, Top = 0 };
            MarkdownEditor pad = Control<MarkdownEditor>(window, "Pad");
            ComboBox picker = Control<ComboBox>(window, "NotePicker");
            string first = (string)picker.SelectedItem;
            Check(pad.Text == "# Legacy backup", "Legacy import did not reach the editor.");
            CheckHighlighting(pad);
            pad.Text = "# Legacy backup";
            pad.Document.UndoStack.ClearAll();
            pad.Text = "# First pending edit";
            Control<Button>(window, "NewBtn").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            string second = (string)picker.SelectedItem;
            Check(first != second && pad.Text == "", "New did not create/select an empty note.");
            Check(File.ReadAllText(Path.Combine(directory, first)) == "# First pending edit",
                "Creating a note lost a pending autosave.");
            pad.Text = "# Second pending edit";
            picker.SelectedItem = first;
            Check(File.ReadAllText(Path.Combine(directory, second)) == "# Second pending edit",
                "Switching did not flush the selected note.");
            Check(pad.Text == "# First pending edit", "Dropdown did not load the selected note.");
            pad.Undo();
            Check(pad.Text == "# First pending edit", "Undo crossed note boundaries.");
            pad.Text = "# Edited older note";
            DrainAutosave();
            Check(File.ReadAllText(Path.Combine(directory, first)) == pad.Text,
                "Autosave wrote to the wrong note.");
            Check(File.ReadAllText(Path.Combine(directory, second)) == "# Second pending edit",
                "Editing the older note overwrote another note.");
            Check(picker.Items.Cast<string>().SequenceEqual(new[] { second, first }), "Dropdown order is not newest first.");

            NoteStore store = Field<NoteStore>(window, "_notes");
            foreach (string action in new[] { "CancelButton", "CloseButton", "DeleteButton" })
            {
                DeleteNoteDialog dialog = new(first)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -10000,
                    Top = -10000,
                    ShowActivated = false
                };
                Check(((TextBlock)dialog.FindName("FileNameLabel")).Text == first,
                    "Delete dialog does not identify the selected note.");
                Button cancel = (Button)dialog.FindName("CancelButton");
                Check(cancel.IsCancel && cancel.IsDefault, "Delete dialog is not cancel-safe.");
                dialog.Loaded += (_, _) =>
                    ((Button)dialog.FindName(action)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check((dialog.ShowDialog() == true) == (action == "DeleteButton"),
                    $"{action} produced the wrong deletion confirmation.");
                Check(File.Exists(Path.Combine(directory, first)) && store.CurrentFileName == first,
                    "The confirmation dialog modified notes before the caller confirmed.");
            }
            Invoke(window, "ChangeNote", (Action)store.DeleteCurrent);
            Check((string)picker.SelectedItem == second && pad.Text == "# Second pending edit",
                "Deletion did not refresh the editor and select the newest note.");
            Invoke(window, "ChangeNote", (Action)store.DeleteCurrent);
            Check(picker.Items.Count == 1 && pad.Text == "", "Deleting the last note did not create a replacement.");

            CheckBox autosave = Control<CheckBox>(window, "AutosaveSwitch");
            autosave.IsChecked = false;
            autosave.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
            pad.Text = "# Unsaved draft";
            Invoke(window, "FlushSave");
            string replacement = (string)picker.SelectedItem;
            Check(File.ReadAllText(Path.Combine(directory, replacement)) == "", "Autosave-off draft was written.");
            Check(store.HasUnsavedChanges, "Autosave-off draft was not tracked.");
            autosave.IsChecked = true;
            autosave.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
            DrainAutosave();
            Check(File.ReadAllText(Path.Combine(directory, replacement)) == "# Unsaved draft",
                "Enabling autosave lost the draft.");
            MainWindow reopened = new() { Left = 0, Top = 0 };
            Check((string)Control<ComboBox>(reopened, "NotePicker").SelectedItem == replacement &&
                Control<MarkdownEditor>(reopened, "Pad").Text == "# Unsaved draft", "Restart did not restore active note.");
            reopened.Close();

            foreach (double width in new[] { 360d, 520d, 760d, 1920d })
            {
                Grid root = (Grid)window.Content;
                root.Measure(new Size(width, 280));
                root.Arrange(new Rect(0, 0, width, 280));
                root.UpdateLayout();
                double previousRight = 0;
                foreach (string name in new[] { "NewBtn", "NotePicker", "DeleteBtn", "TitleLabel", "SettingsBtn", "CopyBtn", "CloseBtn" })
                {
                    FrameworkElement element = Control<FrameworkElement>(window, name);
                    Rect bounds = element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
                    Check(bounds.Width > 0 && bounds.Left >= previousRight && bounds.Right <= width &&
                        bounds.Top >= 0 && bounds.Bottom <= 40,
                        $"{name} is clipped at width {width}.");
                    Check(Math.Abs((bounds.Top + bounds.Bottom) / 2 - 20) <= 1,
                        $"{name} is not on the same toolbar line at width {width}.");
                    previousRight = bounds.Right;
                }
                TextBlock title = Descendants(root).OfType<TextBlock>().Single(t => t.Text == "QuickText");
                Rect titleBounds = title.TransformToAncestor(root).TransformBounds(new Rect(title.RenderSize));
                Check(Math.Abs((titleBounds.Left + titleBounds.Right) / 2 - width / 2) <= 1, "Title is not centered.");
                Check(Control<Button>(window, "CopyBtn").Content is Viewbox, "Copy is not a document icon.");
            }
            Invoke(window, "OpenSettings");
            Check(!Control<Grid>(window, "EditorPanel").IsEnabled, "Settings leaves the editor focusable.");
            ComboBox fontPicker = Control<ComboBox>(window, "NoteFontPicker");
            string noteText = pad.Text;
            pad.Select(2, 4);
            bool couldUndo = pad.Document.UndoStack.CanUndo;
            bool couldRedo = pad.Document.UndoStack.CanRedo;
            string selectedFont = fontPicker.Items.Cast<string>()
                .First(name => name != pad.FontFamily.Source);
            fontPicker.SelectedItem = selectedFont;
            Check(pad.FontFamily.Source == selectedFont &&
                Field<AppSettings>(window, "_s").NoteFontFamily == selectedFont,
                "Choosing a font did not update the editor and settings.");
            Check(pad.Text == noteText && pad.SelectionStart == 2 && pad.SelectionLength == 4 &&
                pad.Document.UndoStack.CanUndo == couldUndo &&
                pad.Document.UndoStack.CanRedo == couldRedo,
                "Changing the font changed source text, selection, or undo history.");
            Check(window.FontFamily.Source != selectedFont, "Note font changed the application UI.");
            window.SaveNow();
            MainWindow fontRestored = new() { Left = 0, Top = 0 };
            Check(Control<MarkdownEditor>(fontRestored, "Pad").FontFamily.Source == selectedFont,
                "The note font was not restored after restart.");
            fontRestored.Close();
            Field<AppSettings>(window, "_s").NoteFontFamily = "QuickText Missing Font 49183";
            Invoke(window, "ApplyNoteFont");
            Check(pad.FontFamily.Source == "Segoe UI" &&
                Control<TextBlock>(window, "FontNotice").Text.Contains("not installed"),
                "An unavailable font was not explained with a usable fallback.");
            Field<AppSettings>(window, "_s").NoteFontFamily = selectedFont;
            Invoke(window, "ApplyNoteFont");
            Invoke(window, "CloseSettings");
            Check(Control<Grid>(window, "EditorPanel").IsEnabled, "Closing Settings did not restore the editor.");
            double expandedHeight = window.Height;
            double minimumHeight = window.MinHeight;
            double baseHeight = Field<double>(window, "_baseHeight");
            string selectedNote = (string)picker.SelectedItem;
            pad.Text = "# Pending edit before collapse";
            pad.Select(2, 7);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                Check(CaptionDoubleClick(window, 2), "Caption double-click was not handled.");
                Check(window.Height == 40 && window.MinHeight == 40 &&
                    window.ResizeMode == ResizeMode.NoResize, "Collapse did not produce a toolbar-only window.");
                Check(Control<Grid>(window, "EditorPanel").Visibility == Visibility.Collapsed &&
                    Control<WrapPanel>(window, "PillPanel").Visibility == Visibility.Collapsed,
                    "Collapsed content remains visible or focusable.");
                Invoke(window, "OnUserMovedOrResized");
                window.SaveNow();
                Check(Field<AppSettings>(window, "_s").Height == baseHeight,
                    "Moving/saving the collapsed bar replaced the expanded size.");
                Check(!CaptionDoubleClick(window, 1) && window.Height == 40,
                    "A non-caption double-click toggled collapse.");
                Check(CaptionDoubleClick(window, 2), "Expand caption double-click was not handled.");
                Check(window.Height == expandedHeight && window.MinHeight == minimumHeight &&
                    window.ResizeMode == ResizeMode.CanResize, "Expansion did not restore manual resizing and height.");
                Check(pad.Text == "# Pending edit before collapse" && pad.SelectionStart == 2 &&
                    pad.SelectionLength == 7 && (string)picker.SelectedItem == selectedNote,
                    "Collapse/expand changed the draft, selection, or active note.");
            }
            Check(File.ReadAllText(Path.Combine(directory, selectedNote)) == pad.Text,
                "Autosave did not preserve the collapsed draft.");
            CaptionDoubleClick(window, 2);
            Invoke(window, "OpenSettings");
            Check(!Field<bool>(window, "_collapsed") &&
                Control<Grid>(window, "SettingsPanel").Visibility == Visibility.Visible,
                "Opening Settings did not expand the collapsed window.");
            CaptionDoubleClick(window, 2);
            Check(Control<Grid>(window, "SettingsPanel").Visibility == Visibility.Collapsed &&
                window.Height == 40, "Collapsing Settings left an overlay visible.");
            CaptionDoubleClick(window, 2);
            IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
            SendMessage(handle, 0x00A3, new IntPtr(2), IntPtr.Zero);
            Check(Field<bool>(window, "_collapsed") && window.Height == 40,
                "The native caption message did not collapse through WindowChrome.");
            SendMessage(handle, 0x00A3, new IntPtr(2), IntPtr.Zero);
            Check(!Field<bool>(window, "_collapsed") && window.WindowState == WindowState.Normal,
                "The native caption message maximized instead of expanding.");
            window.Close();
            Check(File.ReadAllText(Path.Combine(directory, "notes.txt")) == "# Legacy backup", "Legacy backup changed.");
            Console.WriteLine("PASS: Markdown spans/fences, native spelling and corrections, font selection/persistence/fallback, native notes, themed confirmation, toolbar layout, collapse/expand and Settings transitions.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            app.Shutdown();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static T Control<T>(MainWindow window, string name) where T : FrameworkElement =>
        (T)window.FindName(name);

    private static T Field<T>(MainWindow window, string name) =>
        (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;

    private static void Invoke(MainWindow window, string name, params object[] arguments) =>
        typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, arguments);

    private static bool CaptionDoubleClick(MainWindow window, int hitTest)
    {
        object[] arguments = { IntPtr.Zero, 0x00A3, new IntPtr(hitTest), IntPtr.Zero, false };
        typeof(MainWindow).GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, arguments);
        return (bool)arguments[4];
    }

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void DrainAutosave()
    {
        DispatcherFrame frame = new();
        DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(600) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void CheckHighlighting(MarkdownEditor editor)
    {
        string source = "# Heading spelinggg\n- [x] Task\n> Quote\n`inline code`\n**strong** and *emphasis*\n" +
            "[label](https://example.com)\n```text\n# literal heading\n```\n# Heading again";
        editor.Text = source;
        using (var highlighter = new DocumentHighlighter(editor.Document, editor.SyntaxHighlighting))
        {
            foreach (var (line, color) in new[]
            {
                (1, "Heading"), (2, "Marker"), (3, "Quote"), (4, "Code"),
                (5, "Strong"), (5, "Emphasis"), (6, "Link"), (8, "Code"), (10, "Heading")
            })
            {
                Check(highlighter.HighlightLine(line).Sections.Any(section => section.Color.Name == color),
                    $"Markdown line {line} does not contain expected {color} highlighting.");
            }
            Check(!highlighter.HighlightLine(8).Sections.Any(section => section.Color.Name == "Heading"),
                "Heading syntax leaked into fenced code.");
        }
        Check(editor.Text == source, "Highlighting changed Markdown source.");
        editor.Measure(new Size(640, 280));
        editor.Arrange(new Rect(0, 0, 640, 280));
        editor.UpdateLayout();
        editor.TextArea.TextView.EnsureVisualLines();
        Check(editor.TextArea.TextView.VisualLines.SelectMany(line => line.Elements).Any(element =>
            element.TextRunProperties.ForegroundBrush is SolidColorBrush brush &&
            brush.Color == Color.FromRgb(226, 173, 59)),
            "The rendered editor did not apply the heading accent color.");
        Check(editor.TextArea.TextView.VisualLines.SelectMany(line => line.Elements).Any(element =>
            element.TextRunProperties.TextDecorations is { Count: > 0 }),
            "Native spelling underlines were not rendered.");
        editor.CaretIndex = editor.Text.Length;
        editor.Document.Insert(editor.CaretIndex, "\nMore source");
        editor.Undo();
        Check(editor.Text == source, "Highlighting broke text undo.");
        editor.Redo();
        Check(editor.Text.EndsWith("\nMore source"), "Highlighting broke text redo.");
        editor.Text = "hello spelinggg\nanother spelinggg";
        Check(editor.GetSpellingError(8) is not null &&
            editor.GetSpellingErrorStart(8) == 6 && editor.GetSpellingErrorLength(8) == 9,
            "The editor did not retain Windows spelling errors and offsets.");
        editor.Document.Replace(6, 9, "spelling");
        Check(editor.Text == "hello spelling\nanother spelinggg", "Spelling correction changed unrelated text.");
        editor.Undo();
        Check(editor.Text == "hello spelinggg\nanother spelinggg", "Spelling correction cannot be undone.");
        var error = editor.GetSpellingError(8)!;
        editor.IgnoreSpellingError(error);
        Check(editor.GetSpellingError(8) is null, "Ignore all did not update native spelling.");
    }

}
