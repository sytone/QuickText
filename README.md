# QuickText

![QuickText icon](docs/images/icon.png)

A small scratchpad for Windows. One shortcut opens it, one hides it.
Plain text, no account, no network.

[![Build status][build-badge]][build]
[![Latest upstream release][release-badge]][upstream-release]
[![MIT license][license-badge]](LICENSE)

[Download](#download) | [Install or update](#install-or-update-from-source) |
[Getting started](#getting-started) | [Changelog](CHANGELOG.md)

![QuickText with the note toolbar and highlighted Markdown source][screenshot]

QuickText is made for short, temporary notes: collecting text from several
websites, rewriting a paragraph, drafting a prompt. It stays in the background,
uses about 70 MB while open and 20 to 30 MB while hidden.

This fork uses the native WPF baseline from [BlazeR-28/QuickText][upstream].
The earlier customized WebView2 version is preserved on the [BACKUP branch][backup].
The Markdown note library is now ported to native WPF using the same files,
selection marker, and migration process as that version.

## Features

- **Instant:** a system-wide shortcut shows or hides the window from any app,
  `Ctrl+Shift+Space` by default.
- **Quick Markdown notes:** create, switch, and delete notes from the title bar.
  Edit highlighted source text directly, including headings, lists, quotes,
  emphasis, links, and inline/fenced code. Markdown is not rendered.
- **Your note font:** choose an installed font in Settings. It applies immediately
  to every note and is remembered across restarts without changing your files.
- **Safe:** with autosave enabled, edits save to the selected note, including
  pending edits before switching. The selected note is restored on the next start.
- **Links as pills:** URLs in your note show up at the bottom. Hover highlights
  the link in the text, a click opens it in your browser.
- **Spell checking** with suggestions in the right-click menu.
- **Yours to adjust:** shortcuts, window opacity, and an optional quiet start
  with Windows. Size and position are remembered.
- **Private:** no network code, no telemetry. Everything stays in one folder on
  your machine.

## Download

1. Download **`QuickText.exe`** from the [latest upstream release][upstream-release].
2. Double-click it. There is nothing to install and no runtime to add.

QuickText needs Windows 10 or 11, 64-bit.

> [!NOTE]
> The file is not code-signed, so Windows may show "Windows protected your PC".
> Verify the download source and compare its SHA-256 hash with `SHA256SUMS.txt`
> from the same release before deciding whether to run it.

| File | Size | Needs |
| :--- | :--- | :--- |
| `QuickText.exe` | about 125 MB | nothing |
| `QuickText-needs-dotnet.exe` | about 0.3 MB | [.NET 10 Desktop Runtime][runtime] |

For this fork's current source, use the installer below instead of the upstream
download.

## Install or update from source

You need Windows 10 or 11 (64-bit), PowerShell 5.1 or later, and the
[.NET 10 SDK][sdk]. From PowerShell in this repository's root:

```powershell
.\scripts\Install-QuickText.ps1
```

For updates, pull the desired branch and run the same script again. Exit QuickText
from its tray menu first. The script can gracefully close a visible installed
instance, but stops rather than force-killing an instance it cannot close safely.

The script:

- Publishes the native WPF project as a self-contained executable before changing
  the installed app. No separate .NET Desktop Runtime is needed to run it.
- Installs to `%LOCALAPPDATA%\Programs\QuickText` without administrator rights.
- Leaves notes and settings untouched.
- Enables **Start with Windows** through the app's per-user registry entry,
  starting quietly in the tray. A matching Startup shortcut from the older
  installer is removed to avoid duplicate launches.
- Starts QuickText when installation succeeds.

To install without launching immediately:

```powershell
.\scripts\Install-QuickText.ps1 -NoStart
```

`-NoStart` still enables **Start with Windows**. You can turn that off in settings;
running the installer again re-enables it.

To uninstall, turn off **Start with Windows**, exit QuickText, and delete the
executable or its installation directory. Delete `%LOCALAPPDATA%\QuickText` only
if you also want to permanently remove your notes and settings.

## Getting started

1. Start QuickText. A tray icon appears in the notification area. On Windows 11
   new icons first sit under the arrow, drag it out to keep it visible.
2. Type or paste. Use the document-copy icon to copy the whole note.
3. Press **Esc** or the **X** button to hide the window. Press **Ctrl+Shift+Space**
   or click the tray icon to bring it back.
4. Quit with **Ctrl+Q**, the tray menu, or the settings.

Starting the app a second time just brings the running window to the front.

Double-click the title or empty title-bar space to collapse to the toolbar;
double-click again to expand. The note, selection, and pending autosave stay
intact. Opening Settings expands the window. Manual resizing and automatic
growth remain available when expanded; moving the collapsed bar does not replace
your saved expanded size.

### Managing notes

- The **New** icon creates and selects an empty `note-YYYY-MM-DD-HH-MM-SS.md`.
  Rapid clicks still produce unique filenames without overwriting notes.
- The dropdown selects the note being viewed, edited, and autosaved. Notes are
  sorted by filename, newest first. Pending autosave edits are flushed before
  creating or switching notes.
- The trash icon asks before permanently deleting the selected note, then opens
  the newest remaining note. Deleting the last note creates an empty replacement.
- Turning autosave off never deletes saved files. Unsaved drafts require
  confirmation before switching, creating, deleting, or quitting. Turning
  autosave back on saves the current draft.
- File access failures are reported without silently discarding the open draft.

All shortcuts can be changed in the settings (gear icon): click a field, press the
keys, Backspace clears it.

Use **Settings > Note font** to select an installed font. The choice affects the
editor only, not the toolbar or Settings. If the saved font is unavailable,
QuickText explains the fallback to Segoe UI; select another font to replace it.
Windows spelling suggestions and undo/redo remain available in the highlighted
editor. Spelling runs in the background after a short typing pause; underlines
and suggestions appear when checking finishes without blocking scrolling.
Highlighting covers common Markdown syntax, not a full Markdown preview
or language-specific highlighting inside code fences.

| Action | Default | Works |
| :--- | :--- | :--- |
| Show / hide | `Ctrl+Shift+Space` | in every app |
| Hide window | `Esc` | when the window is focused |
| Copy note | `Ctrl+Shift+C` | when the window is focused |
| Exit | `Ctrl+Q` | when the window is focused |

Combinations that would get in the way of typing are refused, for example a bare
letter or `Ctrl+Alt+letter` (AltGr on many keyboard layouts). If another program
already owns the system-wide shortcut, the settings say so, and the tray icon
still works.

![Settings with note font, autosave, startup, opacity, and shortcut controls][settings]

The screenshots show a customized note font and global shortcut; the defaults
are described above.

## Your data

Everything lives in `%LOCALAPPDATA%\QuickText`: `settings.json`, Markdown notes
named `note-YYYY-MM-DD-HH-MM-SS.md`, and `active-note.txt`, which remembers the
selected note across restarts.

Existing Markdown notes and the selected filename from the WebView2 fork work
unchanged. On first use, if there are no Markdown notes or selection marker,
`notes.txt` is imported into a Markdown note and retained unchanged as a backup.
Only correctly timestamped note files appear in the dropdown; unrelated Markdown
files are left alone.

- `QUICKTEXT_DATA_DIR` moves that folder, for example next to the exe on a USB stick.
- Spell checking follows your Windows display language.
  `QUICKTEXT_SPELLCHECK_LANGUAGE` (for example `en-US`) sets another one.

## Troubleshooting

- **The window is gone.** Click the tray icon, press the global shortcut, or start
  `QuickText.exe` again.
- **The old taskbar icon still shows after an update.** Windows caches icons.
  Restart Explorer or sign out and in.
- **The installer cannot close QuickText.** Exit using the tray menu and run the
  script again. It will not force-stop the app and risk losing unsaved edits.
- **Something crashed.** Details are in `%LOCALAPPDATA%\QuickText\crash.log`.
  Please attach it to an [issue][issues].

## Build from source

You need the [.NET 10 SDK][sdk].

```powershell
dotnet publish .\src\QuickText\QuickText.csproj -c Release -o .\out
```

This creates the self-contained `out\QuickText.exe`. Add `-p:SelfContained=false`
for the small variant. Debug builds keep their data in
`%LOCALAPPDATA%\QuickText-dev`, unless `QUICKTEXT_DATA_DIR` overrides it.
Pushing a tag like `v2.0.0` makes GitHub Actions build both variants and attach
them to a release.

QuickText is a plain WPF application without an embedded browser. The global
shortcut, tray icon, window opacity and rounded corners use the Win32 and DWM
APIs directly.

## Contributing

Bug reports and small, focused pull requests are welcome. QuickText is deliberately
small; the native Markdown library builds on this fork's previous workflow.
Please read
[CONTRIBUTING.md](CONTRIBUTING.md) first. Security problems go through
[SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE). Third-party components bundled in the release are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

[build-badge]: https://github.com/sytone/QuickText/actions/workflows/build.yml/badge.svg
[build]: https://github.com/sytone/QuickText/actions/workflows/build.yml
[release-badge]: https://img.shields.io/github/v/release/BlazeR-28/QuickText?style=flat
[upstream-release]: https://github.com/BlazeR-28/QuickText/releases/latest
[license-badge]: https://img.shields.io/github/license/sytone/QuickText?style=flat
[upstream]: https://github.com/BlazeR-28/QuickText
[backup]: https://github.com/sytone/QuickText/tree/BACKUP
[screenshot]: docs/images/screenshot.png
[settings]: docs/images/settings.png
[runtime]: https://dotnet.microsoft.com/download/dotnet/10.0/runtime
[sdk]: https://dotnet.microsoft.com/download/dotnet/10.0
[issues]: https://github.com/sytone/QuickText/issues/new/choose
