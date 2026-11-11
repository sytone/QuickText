# Changelog

All notable changes to QuickText are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Native WPF Markdown note library reusing the previous fork's note store:
  timestamped filenames, selected-note persistence, newest-first dropdown,
  legacy import, and confirmed deletion with an empty last-note replacement.
- Single-line toolbar: New, note dropdown, delete, centered QuickText title,
  settings, document-copy, and X icons.
- Storage regression tests and native WPF note workflow/layout checks.
- Restored the per-user install/update script for the native WPF project, with
  graceful shutdown and registry-based startup.
- Shared Markdown lint configuration and contributor validation instructions.

### Changed

- Autosave targets the selected Markdown note and flushes before switching.
  Disabling autosave preserves files and warns before discarding drafts.
- Updated the README for this fork's native baseline and installer.
- Cleaned up Markdown formatting throughout the repository.

## [2.0.2] - 2026-11-03

### Fixed

- Right-clicking a misspelled word offered corrections for a word a few characters
  to the left, so the wrong word could be changed. The click position is now taken
  relative to the text box.
- A right click no longer moves the caret or changes the selection.
  Picking a correction keeps the caret where you were typing.

## [2.0.1] - 2026-10-30

### Added

- `QUICKTEXT_SPELLCHECK_LANGUAGE` environment variable to spell-check in another
  language than the Windows display language.

### Changed

- README rewritten with screenshots; repository documentation updated.

## [2.0.0] - 2026-10-29

A complete rewrite in plain WPF. The embedded browser is gone.

### Added

- Tray icon, default global shortcut `Ctrl+Shift+Space`, `Esc` hides the window.
- Optional "Start with Windows" (quietly in the tray).
- Single instance: starting QuickText again brings the running window forward.
- Window size and position are remembered; the window grows with the text.
- Dark right-click menu with spelling suggestions.
- `QUICKTEXT_DATA_DIR` environment variable for portable use.
- Self-contained single-file release that runs without installing .NET.
- GitHub Actions for build and release.

### Changed

- New graphite and amber look and a new icon.
- Memory use drops from about 310 MB to about 73 MB, and to roughly 20-30 MB while
  hidden.
- Settings are reduced to autosave, start with Windows, opacity and shortcuts.
  Window size is set by dragging the edges.
- Targets .NET 10.

### Fixed

- Clear can be undone with `Ctrl+Z`.
- Shortcut registration errors are shown instead of failing silently.
- Shortcuts that would block typing (a bare letter, `Ctrl+Alt+letter`) are refused.
- Notes and settings are written atomically; crashes are logged to `crash.log`.
- Links open only as http or https.

## [1.1.0] - 2026-06-08

### Added

- Configurable window size limits in the settings panel.

## [1.0.0] - 2026-06-06

First release: WPF shell with a WebView2 interface, autosave, link pills,
configurable shortcuts and window opacity.

[Unreleased]: https://github.com/sytone/QuickText/compare/main...HEAD
[2.0.2]: https://github.com/BlazeR-28/QuickText/compare/v2.0.1...v2.0.2
[2.0.1]: https://github.com/BlazeR-28/QuickText/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/BlazeR-28/QuickText/releases/tag/v2.0.0
[1.1.0]: https://github.com/BlazeR-28/QuickText/releases/tag/v1.1.0
[1.0.0]: https://github.com/BlazeR-28/QuickText/releases/tag/v1.0.0
