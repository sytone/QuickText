# Contributing

Thanks for taking an interest in QuickText. Bug reports and small, focused pull
requests are welcome.

## Scope

QuickText is deliberately small: quick Markdown source notes, a global shortcut,
and low memory use. Preserve compatibility with the earlier fork's note filenames
and selected-note marker. If you are unsure whether an idea fits, open an issue.

## Building

You need Windows 10 or 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build QuickText.sln -c Debug
dotnet run --project .\src\QuickText
```

Debug builds keep their data in `%LOCALAPPDATA%\QuickText-dev`. Set
`QUICKTEXT_DATA_DIR` to use any other folder; this also overrides Debug isolation.

## Note workflow tests

Run the reused note-store tests in PowerShell 7 or later:

```powershell
pwsh -NoProfile -File .\scripts\Test-NoteStore.ps1
```

Run the native WPF integration checks on Windows:

```powershell
dotnet run --project .\tests\QuickText.Notes.Tests -c Release
```

Both use temporary data directories and leave installed notes untouched.
The native checks exercise control events and layout without showing the main
window. Delete confirmation, Cancel, and close are tested in offscreen dialogs;
unsaved-discard prompts and visual appearance still need manual verification.

## Markdown

With Node.js installed, lint every Markdown file from the repository root:

```powershell
npx --yes markdownlint-cli2@0.24.0 "**/*.md" "#**/bin/**" "#**/obj/**"
```

The shared configuration keeps all default rules enabled. Changelog category
headings may repeat under different versions, but duplicate sibling headings
still fail lint.

## Pull requests

- Keep a change to one topic and describe what it does and how you tried it.
- Follow the style of the surrounding code (nullable enabled, short comments
  that explain why).
- Make sure `dotnet build QuickText.sln -c Release` finishes without warnings.
- Add a line to `CHANGELOG.md` under "Unreleased".

## Releases

Releases are built by GitHub Actions: pushing a tag like `v2.0.1` builds the
self-contained and the small variant and attaches them, with checksums, to a
GitHub release.
