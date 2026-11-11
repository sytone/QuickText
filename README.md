# QuickText

![QuickText icon](docs/images/icon.png)

A small scratchpad for Windows. One shortcut opens it, one hides it.
Plain text, no account, no network.

[![Build status][build-badge]][build]
[![Latest upstream release][release-badge]][upstream-release]
[![MIT license][license-badge]](LICENSE)

[Download](#download) | [Install or update](#install-or-update-from-source) |
[Getting started](#getting-started) | [Changelog](CHANGELOG.md)

![QuickText with a note, link pills, and Clear and Copy buttons][screenshot]

QuickText is made for short, temporary notes: collecting text from several
websites, rewriting a paragraph, drafting a prompt. It stays in the background,
uses about 70 MB while open and 20 to 30 MB while hidden.

This fork uses the native WPF baseline from [BlazeR-28/QuickText][upstream].
The earlier customized WebView2 version is preserved on the [BACKUP branch][backup].
Its Markdown note library and other customizations have not yet been ported.

## Features

- **Instant:** a system-wide shortcut shows or hides the window from any app,
  `Ctrl+Shift+Space` by default.
- **Plain text only:** no tabs, no history, no search. Pasted text arrives without
  formatting.
- **Safe:** with autosave enabled, the note is saved as you type and restored on
  the next start. Clear can be undone with `Ctrl+Z`.
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
2. Type or paste. Press **Copy** when you are done.
3. Press **Esc** or the minus button to hide the window. Press **Ctrl+Shift+Space**
   or click the tray icon to bring it back.
4. Quit with **Ctrl+Q**, the tray menu, or the settings.

Starting the app a second time just brings the running window to the front.

All shortcuts can be changed in the settings (gear icon): click a field, press the
keys, Backspace clears it.

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

![Settings with autosave, startup, opacity, and shortcut controls][settings]

## Your data

Everything lives in `%LOCALAPPDATA%\QuickText`: `settings.json`, and `notes.txt`
while autosave is on. Turning autosave off deletes `notes.txt`.

The native app uses one plain-text note. Markdown notes from the earlier
WebView2 fork remain on disk, but are not loaded or migrated by this version.

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
small, so features such as tabs or rich text are out of scope. Please read
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
