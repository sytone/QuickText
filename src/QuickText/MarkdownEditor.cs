using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using System.Xml;

namespace QuickText;

public sealed class MarkdownEditor : TextEditor
{
    private readonly TextBox _spelling = new();
    private bool _spellingDirty = true;

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
        SpellCheck.SetIsEnabled(_spelling, true);
        TextChanged += (_, _) => _spellingDirty = true;
        TextArea.TextView.LineTransformers.Add(new SpellingUnderlines(this));
    }

    public int CaretIndex { get => CaretOffset; set => CaretOffset = value; }

    public int GetCharacterIndexFromPoint(Point point, bool snapToText)
    {
        var position = GetPositionFromPoint(point);
        return position is { } location ? Document.GetOffset(location.Location) : -1;
    }

    private void SynchronizeSpelling()
    {
        if (_spelling.Language != Language)
        {
            _spelling.Language = Language;
            _spellingDirty = true;
        }
        if (!_spellingDirty) return;
        _spelling.Text = Text;
        _spellingDirty = false;
    }

    public SpellingError? GetSpellingError(int index)
    {
        SynchronizeSpelling();
        return _spelling.GetSpellingError(index);
    }

    public int GetSpellingErrorStart(int index)
    {
        SynchronizeSpelling();
        return _spelling.GetSpellingErrorStart(index);
    }

    public int GetSpellingErrorLength(int index)
    {
        SynchronizeSpelling();
        return _spelling.GetSpellingErrorLength(index);
    }

    public void IgnoreSpellingError(SpellingError error)
    {
        error.IgnoreAll();
        TextArea.TextView.Redraw();
    }

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
            editor.SynchronizeSpelling();
            int offset = line.Offset;
            while (offset < line.EndOffset)
            {
                int start = editor._spelling.GetNextSpellingErrorCharacterIndex(offset, LogicalDirection.Forward);
                if (start < 0 || start >= line.EndOffset) break;
                int length = editor._spelling.GetSpellingErrorLength(start);
                if (length <= 0) break;
                int end = Math.Min(start + length, line.EndOffset);
                ChangeLinePart(Math.Max(start, line.Offset), end,
                    element => element.TextRunProperties.SetTextDecorations(Underline));
                offset = end;
            }
        }
    }
}
