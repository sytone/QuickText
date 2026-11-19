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
The native checks exercise control events and layout with isolated test data.
Delete confirmation, Cancel, and close are tested in offscreen dialogs;
unsaved-discard prompts and visual appearance still need manual verification.
Floating-mode recovery checks briefly show the test window. They cover native
double-click restore, square pixel sizing, independent editor/icon geometry,
draft and undo preservation, image loading/fallback, settings persistence, and
visible settings-save errors while compact.
The same suite checks Markdown spans and code fences, editor undo/redo, native
spell checking, and font selection/persistence without changing note content.
It also checks paste/layout under two seconds and 30 scroll/layout steps under
three seconds, plus rejecting superseded background spelling results. To run
those checks with a local Markdown fixture instead of the generated sample:

```powershell
dotnet run --project .\tests\QuickText.Notes.Tests -c Release -- C:\path\sample.md
```

## Markdown

With Node.js installed, lint every Markdown file from the repository root:

```powershell
npx --yes markdownlint-cli2@0.24.0 "**/*.md" "#**/bin/**" "#**/obj/**"
```

The shared configuration keeps all default rules enabled. Changelog category
headings may repeat under different versions, but duplicate sibling headings
still fail lint.

## Pull requests

- Follow [AGENTS.md](AGENTS.md) for the shared development, commit, delivery,
  and public API comment conventions.
- Keep a change to one topic and describe what it does and how you tried it.
- Follow the style of the surrounding code (nullable enabled, short comments
  that explain why).
- Make sure `dotnet build QuickText.sln -c Release` finishes without warnings.
- Add a line to `CHANGELOG.md` under "Unreleased".

## Releases

The [Release workflow](.github/workflows/release.yml) turns a version tag into a
**draft**, never an automatically published release. A separate write-enabled job
creates the draft only after the read-only build and tests pass.

### Release with an agent first

After the release tooling is merged, start a new agent session in this repository
and say **"I want to do a release."** The repository's
[agent instructions](AGENTS.md#release-requests) route the request here.
You need Windows, PowerShell 7, the .NET 10 SDK, Git, and an authenticated
`gh` CLI with permission to push branches/tags, merge PRs, read Actions, and drafts.
Repository protections still apply; the agent must stop rather than bypass them.

1. Fetch current `origin/main` and create a fresh isolated `dev/*` peer worktree.
   Leave the user's other worktrees and installed notes alone.
2. Run the read-only planner:

   ```powershell
   pwsh -NoProfile -File .\scripts\Get-ReleasePlan.ps1
   ```

   Its JSON records the source/main commits, prior stable tag, next version,
   reasoning, Unreleased entries, and dirty files. The version baseline is the
   highest stable tag or dated changelog version. Non-empty `### Breaking changes`
   or a `BREAKING CHANGE:` bullet means major; non-empty `### Added` means minor;
   other changes mean patch. A higher version already declared by the project
   is retained. An empty Unreleased section with a prepared version is not bumped
   again. No tag or release is created by planning.
3. Compare the changes since `PreviousTag` with the changelog and
   [release notes](docs/RELEASE_NOTES.md). If there is no prior tag, review the
   current source and full changelog. Read relevant merged PRs/diffs as needed.
   Fix missing or stale prose and inaccurate changelog categories. Keep notes
   user-focused: explain benefits, how to use features, upgrade/data compatibility,
   and limitations, not commit subjects. The scripts validate structure; the agent
   must validate meaning. Re-run the planner after edits.
4. With the reviewed source commit from the plan, run:

   ```powershell
   .\scripts\Invoke-Release.ps1 -ExpectedCommit <SourceCommit> -NotesReviewed
   ```

   Replace `<SourceCommit>` with the actual SHA. `-WhatIf` checks preconditions
   and describes the operation without preparing, merging, tagging, or releasing.
   Only reviewed edits to the notes and changelog are accepted; unrelated changes
   stop the process. The source must still equal current `origin/main`.
5. The script updates the version, dates shipped changelog entries in UTC, builds
   with warnings as errors, commits, pushes, and opens a template-based preparation
   PR. It waits for the exact-head checks, merges that PR without admin overrides,
   checks the merge tree and main build, and pushes an annotated version tag for
   that exact merge commit. A prepared, clean main commit skips duplicate preparation.
6. It waits for the matching tag's push workflow, checks the release remains a
   draft and its description matches the reviewed notes, downloads all assets, and
   verifies the manifest hashes and expected files. Success JSON includes the
   version, commit, preparation PR, workflow URL, and draft URL.

An explicit release request authorizes the release-preparation PR merge after
green checks. It does **not** authorize publishing, bypassing required reviews,
merging unrelated feature PRs, or changing an existing tag/release.

On failure, stop and inspect the reported PR/tag/workflow. Do not force-push, move
tags, overwrite drafts, or invent another version to evade the failure.
An existing open preparation PR must be handled before starting another.
If preparation already merged, use a fresh worktree at the prepared main commit;
an existing tag must point at that same commit. A matching existing draft can be
verified again. A published release or mismatched tag/description is a blocker,
not permission to modify it. An already prepared release requires a clean
worktree; fix stale prepared notes through a reviewed PR before trying to tag.

### Release manually second

1. Update [the reviewed release notes](docs/RELEASE_NOTES.md). This file describes
   the next release, and its history is preserved by each version tag.
   Keep all five sections: Overview, Highlights, Fixes and improvements,
   Upgrading, and Known limitations.
2. Write for someone using the app: explain what is better, why it matters, and
   how to use it. Group related work into a few highlights. Include practical
   upgrade steps, data compatibility, and real limitations. Do not paste commit
   subjects, internal cleanup lists, or unverified claims. Remove placeholders.
3. Set `Version` in [QuickText.csproj](src/QuickText/QuickText.csproj) to the intended
   stable `major.minor.patch` version. Move shipped changes from Unreleased into
   a dated version section in [CHANGELOG.md](CHANGELOG.md).
4. Validate and merge the preparation PR to `main`. Review the notes with the
   code; the workflow adds versioned download links and verification instructions.
5. Tag the reviewed commit on `origin/main`, for example:

   ```powershell
   git fetch origin
   git tag -a v2.1.0 origin/main -m "QuickText v2.1.0"
   git push origin v2.1.0
   ```

The tag must exactly match the project version, including the leading `v`.
Only stable three-part versions are supported; prerelease tags and leading zeros
are rejected. A tag outside `main` is rejected too.

### What the workflow produces

The workflow builds with warnings as errors, runs release-note, note-store, and
native WPF checks, then publishes both Windows x64 variants. It checks each
executable's embedded version before collecting:

- `QuickText.exe`: self-contained; no separate .NET runtime needed.
- `QuickText-needs-dotnet.exe`: requires the .NET 10 Desktop Runtime.
- `LICENSE` and `THIRD-PARTY-NOTICES.md`.
- `SHA256SUMS.txt`: SHA-256 hashes for all four downloads above.

Both variants, the generated description, and checksums are also kept in a
workflow artifact for seven days. The checksums are not code signing.
Failed validation or packaging cannot create a draft. A rerun never overwrites
an existing draft or published release: inspect any partial draft after a failed
upload and resolve it manually.

### Preview without releasing

From **Actions > Release > Run workflow**, select a branch to build and upload
the preview artifact. Manual runs never create or publish a GitHub release,
even when run against a tag.

For a local preview with PowerShell 7 and the .NET 10 SDK:

```powershell
pwsh -NoProfile -File .\scripts\Test-Release.ps1
pwsh -NoProfile -File .\scripts\Publish-Release.ps1
```

The local output is `publish\release`, which must be empty or absent. Use
`-OutputDirectory` for another empty destination rather than mixing old and new
downloads. Neither script touches installed notes, startup settings, or releases.
The regular Build workflow also runs the release-note regression tests on PRs.

### Publish after review

Open the draft in **Releases**. Read the rendered description, check the version,
download both executables and the checksum manifest, and verify their hashes.
Try the downloads with isolated test data, not personal notes. Check that the
upgrade guidance is accurate and the license files are attached.
Publish the draft only when that final review is complete.
