using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using QuickText;

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
            TextBox pad = Control<TextBox>(window, "Pad");
            ComboBox picker = Control<ComboBox>(window, "NotePicker");
            string first = (string)picker.SelectedItem;
            Check(pad.Text == "# Legacy backup", "Legacy import did not reach the editor.");
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
                Control<TextBox>(reopened, "Pad").Text == "# Unsaved draft", "Restart did not restore active note.");
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
            Invoke(window, "CloseSettings");
            Check(Control<Grid>(window, "EditorPanel").IsEnabled, "Closing Settings did not restore the editor.");
            window.Close();
            Check(File.ReadAllText(Path.Combine(directory, "notes.txt")) == "# Legacy backup", "Legacy backup changed.");
            Console.WriteLine("PASS: native New/dropdown/autosave/delete/restart, undo isolation, autosave toggle, centered title and icons at 360-1920px, Settings focus isolation.");
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

}
