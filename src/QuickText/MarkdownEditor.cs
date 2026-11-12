using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using System.Xml;

namespace QuickText;

public sealed class MarkdownEditor : TextEditor, IDisposable
{
    private readonly DispatcherTimer _spellingDelay = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly BackgroundSpelling _spelling;
    private readonly HashSet<string> _ignoredWords = new(StringComparer.OrdinalIgnoreCase);
    private SpellingIssue[] _issues = [];
    private int _revision;
    private bool _disposed;
    private bool _spellingAvailable = true;

    public event EventHandler<Exception>? SpellingFailed;
    public bool IsSpellingPending { get; private set; }

    public MarkdownEditor()
    {
        using var stream = typeof(MarkdownEditor).Assembly.GetManifestResourceStream("QuickText.Resources.Markdown.xshd")
            ?? throw new InvalidOperationException("The Markdown highlighting definition is missing.");
        using var reader = XmlReader.Create(stream);
        SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        WordWrap = true;
        Options.ConvertTabsToSpaces = false;
        Options.EnableHyperlinks = false;
        Options.EnableEmailHyperlinks = false;
        _spelling = new BackgroundSpelling(PublishSpelling, ReportSpellingFailure);
        _spellingDelay.Tick += (_, _) =>
        {
            _spellingDelay.Stop();
            _spelling.Check(_revision, Text, Language.IetfLanguageTag);
        };
        TextChanged += (_, _) =>
        {
            if (_disposed) return;
            _revision++;
            _spelling.Invalidate(_revision);
            _issues = [];
            if (!_spellingAvailable) return;
            IsSpellingPending = true;
            _spellingDelay.Stop();
            _spellingDelay.Start();
        };
        TextArea.TextView.LineTransformers.Add(new SpellingUnderlines(this));
    }

    public int CaretIndex { get => CaretOffset; set => CaretOffset = value; }

    public int GetCharacterIndexFromPoint(Point point, bool snapToText)
    {
        var position = GetPositionFromPoint(point);
        return position is { } location ? Document.GetOffset(location.Location) : -1;
    }

    private void PublishSpelling(int revision, SpellingIssue[] issues)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_disposed || revision != _revision) return;
            _issues = issues.Where(issue => !_ignoredWords.Contains(issue.Word)).ToArray();
            IsSpellingPending = false;
            TextArea.TextView.Redraw();
        }, DispatcherPriority.Background);
    }

    private void ReportSpellingFailure(Exception error)
    {
        Storage.LogCrash(error);
        Dispatcher.BeginInvoke(() =>
        {
            if (_disposed) return;
            _spellingAvailable = false;
            IsSpellingPending = false;
            SpellingFailed?.Invoke(this, error);
        }, DispatcherPriority.Background);
    }

    public SpellingIssue? GetSpellingError(int index) =>
        _issues.FirstOrDefault(issue => index >= issue.Start && index < issue.Start + issue.Length);

    public int GetSpellingErrorStart(int index) => GetSpellingError(index)?.Start ?? -1;
    public int GetSpellingErrorLength(int index) => GetSpellingError(index)?.Length ?? 0;

    public void IgnoreSpellingError(SpellingIssue error)
    {
        _ignoredWords.Add(error.Word);
        _issues = _issues.Where(issue => !_ignoredWords.Contains(issue.Word)).ToArray();
        TextArea.TextView.Redraw();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        IsSpellingPending = false;
        _spellingDelay.Stop();
        _spelling.Dispose();
    }

    public sealed record SpellingIssue(int Start, int Length, string Word, IReadOnlyList<string> Suggestions);

    private sealed class SpellingUnderlines(MarkdownEditor editor) : DocumentColorizingTransformer
    {
        private static readonly TextDecorationCollection Underline = CreateUnderline();

        private static TextDecorationCollection CreateUnderline()
        {
            var decorations = new TextDecorationCollection
            {
                new TextDecoration(TextDecorationLocation.Underline,
                    new Pen(new SolidColorBrush(Color.FromRgb(224, 115, 95)), 1),
                    0, TextDecorationUnit.FontRecommended, TextDecorationUnit.Pixel)
            };
            decorations.Freeze();
            return decorations;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            // Rendering only reads immutable results; it never invokes Windows spelling.
            int low = 0, high = editor._issues.Length;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                SpellingIssue issue = editor._issues[middle];
                if (issue.Start + issue.Length <= line.Offset) low = middle + 1;
                else high = middle;
            }
            for (int index = low; index < editor._issues.Length; index++)
            {
                SpellingIssue issue = editor._issues[index];
                if (issue.Start >= line.EndOffset) break;
                int start = Math.Max(issue.Start, line.Offset);
                int end = Math.Min(issue.Start + issue.Length, line.EndOffset);
                if (start >= end) continue;
                ChangeLinePart(start, end,
                    element => element.TextRunProperties.SetTextDecorations(Underline));
            }
        }
    }

    private sealed class BackgroundSpelling(
        Action<int, SpellingIssue[]> publish, Action<Exception> reportFailure) : IDisposable
    {
        private static readonly Regex Words = new(
            @"https?://\S+|(?<word>[\p{L}\p{M}]+(?:['’][\p{L}\p{M}]+)*)",
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
        private readonly object _gate = new();
        private Snapshot? _pending;
        private Thread? _thread;
        private int _revision;
        private bool _stopped;

        public void Invalidate(int revision)
        {
            lock (_gate)
            {
                _revision = revision;
                _pending = null;
            }
        }

        public void Check(int revision, string text, string language)
        {
            lock (_gate)
            {
                if (_stopped) return;
                _pending = new Snapshot(revision, text, language);
                if (_thread == null)
                {
                    _thread = new Thread(Run) { IsBackground = true, Name = "QuickText spelling" };
                    _thread.SetApartmentState(ApartmentState.STA);
                    _thread.Start();
                }
                Monitor.Pulse(_gate);
            }
        }

        private bool IsCurrent(int revision)
        {
            lock (_gate) return !_stopped && _revision == revision;
        }

        private void Run()
        {
            try
            {
                TextBox checker = new();
                SpellCheck.SetIsEnabled(checker, true);
                var cache = new Dictionary<string, WordResult?>(StringComparer.Ordinal);
                string? language = null;
                while (true)
                {
                    Snapshot snapshot;
                    lock (_gate)
                    {
                        while (!_stopped && _pending == null) Monitor.Wait(_gate);
                        if (_stopped) return;
                        snapshot = _pending!;
                        _pending = null;
                    }
                    if (language != snapshot.Language)
                    {
                        checker.Language = XmlLanguage.GetLanguage(snapshot.Language);
                        language = snapshot.Language;
                        cache.Clear();
                    }
                    var issues = new List<SpellingIssue>();
                    foreach (Match match in Words.Matches(snapshot.Text))
                    {
                        if (!IsCurrent(snapshot.Revision)) break;
                        Group word = match.Groups["word"];
                        if (!word.Success) continue;
                        string value = word.Value;
                        if (!cache.TryGetValue(value, out WordResult? result))
                        {
                            // Checking isolated words avoids WPF's expensive full-document scans.
                            checker.Text = value;
                            SpellingError? error = checker.GetSpellingError(0);
                            result = error == null ? null : new WordResult(
                                checker.GetSpellingErrorStart(0), checker.GetSpellingErrorLength(0),
                                error.Suggestions.ToArray());
                            if (cache.Count >= 4096) cache.Clear();
                            cache[value] = result;
                        }
                        if (result != null)
                            issues.Add(new SpellingIssue(word.Index + result.Start, result.Length,
                                value.Substring(result.Start, result.Length), result.Suggestions));
                    }
                    if (IsCurrent(snapshot.Revision)) publish(snapshot.Revision, issues.ToArray());
                }
            }
            catch (Exception error) when (error is COMException or InvalidOperationException or ArgumentException or NotSupportedException)
            {
                lock (_gate) _stopped = true;
                reportFailure(error);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _stopped = true;
                _pending = null;
                Monitor.Pulse(_gate);
            }
        }

        private sealed record Snapshot(int Revision, string Text, string Language);
        private sealed record WordResult(int Start, int Length, string[] Suggestions);
    }
}
