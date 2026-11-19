# Development approach

These rules apply to the whole repository.

## Delivery

- Start in an isolated peer worktree on a `dev/<alias>/<topic>` branch.
  Fetch `origin/main` and base the work on its latest commit. Preserve unrelated
  local changes; do not switch or overwrite the user's main working tree.
- Keep changes focused. Make small, reviewable commits using Conventional
  Commits, such as `feat:`, `fix:`, `test:`, and `docs:`.
- Finish requested development work through validation, pushing the dev branch,
  and opening a pull request against `main`, unless the user requests local-only
  work or another delivery scope. Do not merge without approval, except for the
  narrow release-request approval below.
- Use the repository's pull request template and include exact validation
  commands with their results. Update the Unreleased changelog and related docs.

## Release requests

Trigger this procedure when the user says "do a release", "prepare a release", or
asks to release the app. Updating release tooling is not itself a release request.

- Follow the canonical [agent release procedure](CONTRIBUTING.md#release-with-an-agent-first).
  Work in a fresh isolated `dev/*` worktree from current `origin/main`.
- Use `scripts\Get-ReleasePlan.ps1` for the version and review baseline. Do not
  invent a version or use a commit count. The scripts own the changelog-based
  major/minor/patch rules and detect already prepared versions.
- Read the actual changes since the previous tag, the Unreleased changelog, and
  `docs\RELEASE_NOTES.md`. Update the prose and any inaccurate changelog categories.
  Structure checks cannot prove that the notes describe the changes. Only supply
  `-NotesReviewed` after checking their accuracy, coverage, upgrade steps, and
  limitations. Re-run the plan after edits.
- Use `scripts\Invoke-Release.ps1` with that plan's `SourceCommit`. The script owns
  version/changelog preparation, validation, commits, pushes, the preparation PR,
  green-check merging, exact-commit tagging, workflow waits, and draft verification.
  Do not replace these actions with improvised shell sequences.
- An explicit release request authorizes merging **only its release-preparation
  PR**, after its exact-head checks pass. This does not authorize merging ordinary
  feature PRs, waiving reviews, using admin overrides, or bypassing protections.
- Never publish, overwrite, delete, force-push, or move an existing release/tag.
  Stop on stale main, unrelated changes, failed checks, conflicts, or unexpected
  draft contents. Inspect partial state before retrying; do not make a new version
  just to hide a failed attempt.
- Completion means the script verified the successful release workflow, draft
  status, reviewed description, all downloads, and SHA-256 hashes. Report the
  version, preparation PR, workflow, and draft links, and explicitly say that the
  draft is ready for the user's final review and manual publication.

## Public API comments

- Give every public class, constructor, function, and property a short XML
  documentation comment in simple English. Apply this to public members of
  internal types too.
- Explain why the API exists and why callers use it. Do not merely repeat its
  name or narrate code that is easy to read.
- Explain what happens when the logic is genuinely complex or depends on
  several calls. Mention caller responsibilities and ordering when they protect
  drafts, saved data, or native resources.
- Keep comments accurate as behavior changes. Avoid obvious inline comments.

## Validation

- Follow [CONTRIBUTING.md](CONTRIBUTING.md) for the existing build, note-store,
  native WPF, and Markdown checks. Run impacted checks first.
- Add focused regression cases for new behavior and verify real output,
  geometry, persistence, and transitions rather than only checking flags.
- Keep automated tests isolated with temporary data directories. Never test
  against installed notes or force-stop a user's running QuickText instance.
- Check external public API documentation with:

  ```powershell
  dotnet build QuickText.sln -c Release -p:GenerateDocumentationFile=true -warnaserror:CS1591
  ```

- Report anything not verified, including visual or multi-monitor behavior.
