using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace QuickText;

public sealed class NoteStore
{
    private const string FileNameFormat = "'note-'yyyy-MM-dd-HH-mm-ss'.md'";
    private readonly string directory;
    private string ActiveNotePath => Path.Combine(directory, "active-note.txt");
    private string savedText = "";

    public string CurrentFileName { get; private set; } = "";
    public string CurrentText { get; private set; } = "";
    public bool HasUnsavedChanges => CurrentText != savedText;

    public NoteStore(string directory)
    {
        this.directory = directory;
    }

    public string[] ListNotes()
    {
        return Directory.EnumerateFiles(directory, "note-*.md")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(IsNoteFileName)
            .OrderByDescending(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    public void Initialize()
    {
        Directory.CreateDirectory(directory);
        bool hasActiveNote = File.Exists(ActiveNotePath);
        string activeNote = hasActiveNote ? File.ReadAllText(ActiveNotePath).Trim() : "";
        if (hasActiveNote && !IsNoteFileName(activeNote))
            throw new InvalidDataException("The saved active note filename is invalid.");

        string[] notes = ListNotes();
        if (notes.Length == 0)
        {
            string legacyPath = Path.Combine(directory, "notes.txt");
            string text = !hasActiveNote && File.Exists(legacyPath) ? File.ReadAllText(legacyPath) : "";
            Activate(CreateFile(text), text);
        }
        else
        {
            Select(notes.Contains(activeNote, StringComparer.Ordinal) ? activeNote : notes[0]);
        }
    }

    public void UpdateText(string fileName, string text, bool autosave)
    {
        if (fileName != CurrentFileName)
            throw new InvalidOperationException("The edited note is no longer selected.");
        CurrentText = text;
        if (autosave) Save();
    }

    public void Save()
    {
        string path = GetNotePath(CurrentFileName);
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, CurrentText);
            File.Move(temporaryPath, path, true);
            savedText = CurrentText;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public void Create()
    {
        Activate(CreateFile(""), "");
    }

    public void Select(string fileName)
    {
        string text = File.ReadAllText(GetNotePath(fileName));
        Activate(fileName, text);
    }

    public void DeleteCurrent()
    {
        string path = GetNotePath(CurrentFileName);
        string? next = ListNotes().FirstOrDefault(name => name != CurrentFileName);
        string nextText = next == null ? "" : File.ReadAllText(GetNotePath(next));
        next ??= CreateFile("");
        // Persist the replacement first; failure must not delete the user's current note.
        File.WriteAllText(ActiveNotePath, next);
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            File.WriteAllText(ActiveNotePath, CurrentFileName);
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            File.WriteAllText(ActiveNotePath, CurrentFileName);
            throw;
        }
        CurrentFileName = next;
        CurrentText = savedText = nextText;
    }

    private void Activate(string fileName, string text)
    {
        File.WriteAllText(ActiveNotePath, fileName);
        CurrentFileName = fileName;
        CurrentText = savedText = text;
    }

    private string CreateFile(string text)
    {
        DateTime timestamp = DateTime.Now;
        string? latest = ListNotes().FirstOrDefault();
        if (latest != null)
        {
            DateTime latestTime = DateTime.ParseExact(latest, FileNameFormat, CultureInfo.InvariantCulture);
            if (timestamp <= latestTime.AddSeconds(1))
                timestamp = latestTime.AddSeconds(1);
        }
        while (true)
        {
            string name = timestamp.ToString(FileNameFormat, CultureInfo.InvariantCulture);
            FileStream stream;
            try
            {
                stream = new FileStream(GetNotePath(name), FileMode.CreateNew, FileAccess.Write);
            }
            catch (IOException) when (File.Exists(GetNotePath(name)))
            {
                timestamp = timestamp.AddSeconds(1);
                continue;
            }
            using (stream)
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(text);
            return name;
        }
    }

    private string GetNotePath(string fileName)
    {
        if (!IsNoteFileName(fileName))
            throw new ArgumentException("Invalid note filename.", nameof(fileName));
        return Path.Combine(directory, fileName);
    }

    private static bool IsNoteFileName(string name)
    {
        return DateTime.TryParseExact(name, FileNameFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _);
    }
}
