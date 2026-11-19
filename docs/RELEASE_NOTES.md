# Release notes

## Overview

Keep QuickText nearby without letting it cover your work. This release adds two
smaller window modes and makes the native Markdown scratchpad more comfortable
for everyday notes, snippets, and quick drafts.

## Highlights

- **Three ways to keep a note close.** Use the full editor, fold it into its title
  bar, or collapse it to a movable, always-on-top icon. The two buttons beside X
  switch modes; double-click the floating icon to return to the full editor.
- **A floating icon that suits your screen.** Use your own image or the app icon.
  Its square size follows the taskbar by default, or you can choose 24 to 128
  pixels in Settings. It remembers its own position without moving the editor.
- **Markdown that stays editable.** Headings, lists, quotes, links, and code are
  highlighted while you type. Choose your note font in Settings, and keep a few
  separate notes using the title-bar picker.
- **Your draft stays with you.** Compact modes keep the current note, selection,
  undo history, and pending autosave intact. The tray icon and global shortcut can
  also bring back the full editor.

## Fixes and improvements

- Large pastes and scrolling no longer wait for full-document spelling scans.
  Spelling suggestions still appear through Windows in the background.
- Settings-save problems stay visible and are retried instead of being silently
  ignored. Normal exit waits for pending settings to save.
- Switching notes saves pending edits first when autosave is enabled. Existing
  Markdown notes and the selected-note marker from the earlier fork still work;
  an older `notes.txt` is imported when needed and kept as a backup.

## Upgrading

Exit QuickText from its tray menu before replacing the executable. Keep it in the
same installation folder so existing shortcuts and startup registration continue
to point at it.

Notes and settings remain in `%LOCALAPPDATA%\QuickText`; replacing the executable
does not remove them. If you use `QUICKTEXT_DATA_DIR`, keep that setting unchanged.
Back up your notes before updating. There is no automatic updater.

## Known limitations

- Windows 10 or 11, x64 only.
- Markdown is highlighted source text, not a rendered preview.
- The default global shortcut is `Ctrl+Shift+Space`. If another app owns it,
  choose another shortcut in Settings or use the tray icon.
