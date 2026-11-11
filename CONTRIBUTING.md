# Contributing

Thanks for taking an interest in QuickText. Bug reports and small, focused pull
requests are welcome.

## Scope

QuickText is deliberately small: one plain-text note, a global shortcut, low
memory use. Features such as tabs, history, search or rich text are out of scope.
If you are unsure whether an idea fits, open an issue first.

## Building

You need Windows 10 or 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build QuickText.sln -c Debug
dotnet run --project .\src\QuickText
```

Debug builds keep their data in `%LOCALAPPDATA%\QuickText-dev`. Set
`QUICKTEXT_DATA_DIR` to use any other folder; this also overrides Debug isolation.

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
