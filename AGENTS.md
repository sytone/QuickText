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
  work or another delivery scope. Do not merge without approval.
- Use the repository's pull request template and include exact validation
  commands with their results. Update the Unreleased changelog and related docs.

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
