using System.Windows.Input;

namespace QuickText;

/// <summary>Shares shortcut parsing and typing-safety rules between recording, settings, and registration.</summary>
/// <param name="Modifiers">Distinguishes shortcuts from ordinary text-entry keys.</param>
/// <param name="Key">Identifies the non-modifier key shared by local and global handlers.</param>
internal readonly record struct Gesture(ModifierKeys Modifiers, Key Key)
{
    public const string NotSet = "not set";

    /// <summary>Normalises WPF's alternate key reports so recorded and executed shortcuts agree.</summary>
    public static Gesture? FromEvent(KeyEventArgs e)
    {
        Key key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key
        };

        if (key is Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return null;

        return new Gesture(Keyboard.Modifiers, key);
    }

    /// <summary>Accepts saved shortcut spellings without requiring users to rewrite older settings.</summary>
    public static bool TryParse(string? text, out Gesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text) ||
            text.Equals(NotSet, StringComparison.OrdinalIgnoreCase) ||
            text.Equals("None", StringComparison.OrdinalIgnoreCase))
            return false;

        ModifierKeys mods = ModifierKeys.None;
        Key? key = null;

        foreach (string raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (raw.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL": mods |= ModifierKeys.Control; break;
                case "SHIFT": mods |= ModifierKeys.Shift; break;
                case "ALT": mods |= ModifierKeys.Alt; break;
                case "WIN": mods |= ModifierKeys.Windows; break;
                default:
                    if (key != null) return false;
                    key = ParseKey(raw);
                    if (key == null) return false;
                    break;
            }
        }

        if (key == null) return false;
        gesture = new Gesture(mods, key.Value);
        return true;
    }

    private static Key? ParseKey(string name)
    {
        string upper = name.ToUpperInvariant();
        switch (upper)
        {
            case "ESCAPE" or "ESC": return Key.Escape;
            case "ENTER": return Key.Return;
            case "BACKSPACE": return Key.Back;
        }

        if (upper.Length == 1 && upper[0] is >= '0' and <= '9')
            return Key.D0 + (upper[0] - '0');

        if (!char.IsDigit(upper[0]) && Enum.TryParse(upper, true, out Key parsed) && parsed != Key.None)
            return parsed;

        return null;
    }

    private static string KeyName(Key key) => key switch
    {
        Key.Escape => "ESCAPE",
        Key.Return => "ENTER",
        Key.Back => "BACKSPACE",
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        _ => key.ToString().ToUpperInvariant()
    };

    /// <summary>Lets global registration reject bare keys that would be swallowed in every app.</summary>
    public bool HasModifier => Modifiers != ModifierKeys.None;

    /// <summary>Keys that produce text or edit it. Binding one of these without a real modifier would break typing.</summary>
    public bool IsTypingKey => Key is (>= Key.A and <= Key.Z) or (>= Key.D0 and <= Key.D9)
        or (>= Key.NumPad0 and <= Key.Divide) or Key.Space or Key.Tab or Key.Return or Key.Back or Key.Delete
        or (>= Key.OemSemicolon and <= Key.OemTilde) or (>= Key.OemOpenBrackets and <= Key.OemBackslash);

    /// <summary>Why this combination would get in the way of normal typing, or null when it is fine.</summary>
    public string? TypingConflict
    {
        get
        {
            if (!IsTypingKey) return null;

            bool real = Modifiers.HasFlag(ModifierKeys.Control) || Modifiers.HasFlag(ModifierKeys.Alt) || Modifiers.HasFlag(ModifierKeys.Windows);
            if (!real)
                return "Letters, digits and Space need Ctrl, Alt or Win in the combination, otherwise you could no longer type them.";

            // AltGr is reported as Ctrl+Alt on many layouts (e.g. German: AltGr+Q is "@").
            if (Modifiers.HasFlag(ModifierKeys.Control) && Modifiers.HasFlag(ModifierKeys.Alt) && !Modifiers.HasFlag(ModifierKeys.Windows))
                return "Ctrl+Alt+key is AltGr on many keyboard layouts and would block typing characters like @ or the euro sign.";

            return null;
        }
    }

    /// <summary>Keeps saved shortcuts stable regardless of how the UI displays their names.</summary>
    public override string ToString()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("CTRL");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("SHIFT");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("ALT");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("WIN");
        parts.Add(KeyName(Key));
        return string.Join("+", parts);
    }

    /// <summary>Keeps hints and buttons readable without changing the stable settings format.</summary>
    public string Pretty => string.Join("+", ToString().Split('+').Select(part => part switch
    {
        "ESCAPE" => "Esc",
        { Length: > 1 } => char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant(),
        _ => part
    }));

    /// <summary>Gives all shortcut fields the same readable label when a binding is absent or invalid.</summary>
    public static string Display(string stored) =>
        TryParse(stored, out var g) ? g.Pretty : "Not set";
}
