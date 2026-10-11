# Aayam — Claude Code Instructions

## 1. Project Identity

This repository is a personal project owned by Kalpan.

### Repository
- Expected path: `/Users/kalpan/Desktop/Kalpan/Aayam`
- Remote: `https://github.com/kalpan2302/Aayam.git`
- Base branch: `dev` (CI also runs on `main`)

### Personal Identity

| Purpose | Personal |
|---|---|
| Claude profile | `claude-personal` |
| Claude config | `/Users/kalpan/.claude-personal` |
| Claude account | `kalpan.s.23@gmail.com` |
| Git commit name | `kalpan bariya` |
| Git commit email | `kalpan.s.23@gmail.com` |
| GitHub account | `kalpan2302` |

Work repositories use a separate Claude profile and GitHub account (`kalpan-hemut`).

- Aayam must use the personal Git identity.
- Work repositories must remain on their work identity.

Inspect the actual local Git configuration (`git config --local --list`) when you need to
confirm it, rather than trusting a stale snapshot.

### Identity Distinction

Git commit identity and GitHub authentication are separate.

- `user.name` / `user.email` determine the author identity recorded in Git commits.
- GitHub authentication determines which GitHub account is allowed to access the remote repository.
- Do not assume that matching commit email means the correct GitHub account is authenticated.

This is particularly important because this machine has both personal and work GitHub
identities.

---

## 2. Identity Isolation — CRITICAL

This repository must remain completely isolated from work repositories and work credentials.

### Claude

When working in Aayam:

- Use the personal Claude profile only.
- Do not switch to or use the work Claude profile.
- Do not modify `~/.claude`.
- Do not modify `/Users/kalpan/.claude-personal` unless explicitly required for Claude configuration.
- Do not copy Claude configuration, credentials, instructions, or project context between personal and work environments.

### GitHub

Aayam uses GitHub account:

`kalpan2302`

Do NOT use:

`kalpan-hemut`

for GitHub authentication in this repository.

### Repository Boundary

Claude must only operate inside:

`/Users/kalpan/Desktop/Kalpan/Aayam`

Do not:

- modify files in work repositories
- copy code from work repositories
- inspect unrelated work repositories
- move files between personal and work repositories
- modify Git configuration for other repositories
- modify global Git configuration unless explicitly instructed

Before performing repository-related operations, verify:

```bash
pwd
git remote -v
```

### Mandatory Identity Check

Before any operation that could modify files, execute commands, or interact with Git/GitHub:

1. Verify the current working directory.
2. Verify the Git remote.
3. Verify that the repository is Aayam.
4. If the path or remote is unexpected, STOP immediately.
5. Do not attempt to repair, switch, or infer the correct identity automatically.

Never assume that the current Claude profile, GitHub account, or Git configuration is
correct merely because the command succeeded.

---

## 3. GitHub / gh CLI — MANUAL ONLY

Claude must NOT perform GitHub account or GitHub CLI operations.

Do NOT execute:

- `gh`
- `gh auth login`
- `gh auth switch`
- `gh auth logout`
- `gh auth token`
- `gh api`

Do NOT:

- change the active gh account
- modify gh authentication
- read GitHub tokens/PATs
- inspect GitHub credential stores
- create GitHub repositories
- create, modify, merge, or close pull requests
- create, modify, or close issues
- modify GitHub Actions through gh
- perform GitHub API operations

All GitHub CLI and GitHub web operations are performed manually by the developer.

Claude must not attempt to work around these restrictions.

---

## 4. Git Safety

Claude may use read-only Git commands for analysis:

- `git status`
- `git diff`
- `git diff --cached`
- `git log`
- `git show`
- `git branch`
- `git remote -v`
- `git rev-parse`
- `git ls-files`

Claude must NOT perform remote or destructive Git operations.

Do NOT execute:

- `git push`
- `git pull`
- `git fetch`
- `git clone`
- `git merge`
- `git rebase`
- `git reset --hard`
- `git clean`
- `git commit`

Also do not execute commands that intentionally discard uncommitted work.

The developer performs commits, pushes, pulls, merges, rebases, and other remote/destructive
Git operations manually.

Claude may prepare code and explain the exact Git commands the developer should run.

---

## 5. Secrets and Credentials

Never read, print, expose, or modify:

- GitHub PATs
- SSH private keys
- API keys
- OAuth tokens
- Claude authentication tokens
- `.env` files containing secrets
- macOS Keychain credentials
- credential stores
- GitHub CLI authentication files
- unrelated credentials

Do not search the filesystem for credentials.

Never include secrets in:

- source code
- logs
- commits
- documentation
- terminal output
- generated files

If credentials are required, tell the developer what needs to be configured manually.

---

## 6. Coding Rules

Before making changes:

- Understand the existing architecture.
- Inspect the relevant files.
- Prefer the smallest correct change.
- Do not rewrite unrelated code.
- Do not introduce unnecessary dependencies.
- Follow existing project conventions.
- Preserve existing functionality unless the task explicitly requires changing it.

After making changes:

- Review the diff.
- Run the most relevant tests/checks available.
- Report what changed.
- Report any tests/checks that could not be run.

Do not claim that tests passed unless they were actually executed.

---

## 7. Unity Project

This is a Unity project.

Current versions:
- Unity: `6000.6.3f1` (see `ProjectSettings/ProjectVersion.txt`; editor installed locally at
  `/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app`)
- AR Foundation: `6.6.2` + ARKit + ARCore (iOS **and** Android both in scope as of
  2026-10-11 — `com.unity.xr.arcore` and `com.unity.xr.openxr` are in
  `Packages/manifest.json`; this corrects the earlier "iOS-only" note here, which was
  stale by the time Phase 3 planning started)

The Unity project is located at the **repository root** (`Assets/`, `Packages/`,
`ProjectSettings/`). Do not assume there is a separate `unity/` directory.

Follow the existing project structure, assembly definitions, modules, tests, and
documentation:

- `Assets/_Project/Scripts/<Module>/Aayam.<Module>.asmdef` — one asmdef per module:
  `AR` (placement/interaction/session), `UI`, `Core`, `Models`, `Services`, `Utilities`.
- `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/` — NUnit tests, own asmdefs
  (`Aayam.Tests.EditMode`, `Aayam.Tests.PlayMode`).
- `Assets/Editor/` — editor-only tooling (`URPConfigValidator.cs`, `ProjectStructureCreator.cs`).
- `docs/phases/phase-N-*.md` track the roadmap phase by phase; `git log` shows which phase
  is actually done (currently Phase 1, AR Foundation) — trust that over the roadmap doc.

---

## 8. Testing

When implementing a feature or fixing a bug:

- Identify relevant tests before changing code.
- Add/update tests when appropriate.
- Prefer targeted tests before full test suites.
- Do not modify tests simply to make them pass unless the expected behavior itself has changed.
- Clearly report test results.

For Unity-related changes, consider:

- EditMode tests
- PlayMode tests
- assembly compilation
- Unity project validation
- platform-specific constraints

No local headless Unity CLI test run has been verified in-session yet. CI uses
`game-ci/unity-test-runner@v4` with `projectPath: unity` (see the CI/CD mismatch below —
fix the path before relying on CI). Unity's batchmode test runner
(`-runTests -testPlatform EditMode|PlayMode -testResults <path>`) can be run locally against
the repo root, but the exact invocation hasn't been verified from this machine — confirm it
works before trusting its output.

---

## 9. Graphify

This repository uses Graphify for codebase visualization and analysis.

Rules:

- Use Graphify when architectural/codebase visualization is useful.
- Prefer scoped Graphify queries for codebase questions when available.
- Query/inspect the graph before significant architectural changes when useful.
- Update generated graph data after significant code changes when appropriate.
- `graphify-out/` is generated output and must not be committed.
- Do not modify Graphify configuration unless explicitly required.
- Do not regenerate Graphify unnecessarily for trivial changes.

Current graph stats, setup history, and extraction details live in
`docs/graphify-notes.md`, not here — that's project reference, not a behavioral rule, and
it goes stale.

---

## 10. CI/CD

The repository contains CI workflows.

Before changing CI:

- inspect the existing workflow
- verify the actual Unity project location
- verify Unity version
- verify package/dependency assumptions

Known issue to keep in mind, measured 2026-10-10: `.github/workflows/test.yml` and
`unity-build.yml` both set `projectPath: unity` / `UNITY_PROJECT_PATH: unity` and cache
`unity/Library`, but `Assets/`, `Packages/`, and `ProjectSettings/` all live at the repo
root. This is left over from the original planned layout in the root `README.md`
(`unity/ # Unity AR project (future)`); the Phase 1 consolidation commit moved the project
to the root without updating CI. Until fixed, `game-ci/unity-test-runner` in CI will fail to
find the project.

Also stale as of the same check: `unity-build.yml`'s C# lint step is a placeholder ("Add
dotnet-format or other linting when solution files are available") — `Aayam.slnx` now
exists, so dotnet-format is wired up and could run.

Do not silently change CI configuration to work around this. Explain the mismatch and make
the smallest intentional fix when requested.

---

## 11. Dependency Management

Before adding a dependency:

- Check whether the functionality already exists.
- Check existing package/dependency configuration.
- Prefer existing dependencies.
- Avoid unnecessary packages.
- Explain why a new dependency is required.

Do not upgrade major dependencies or Unity packages unless explicitly requested or required
to fix the task.

---

## 12. Communication

For implementation tasks:

- Explain the approach briefly before making significant changes.
- Keep changes focused.
- Clearly state files changed.
- Clearly state tests/checks performed.
- Mention known limitations or follow-up work.

If requirements are ambiguous and the decision could materially affect architecture, data,
security, or behavior, ask before implementing.

Do not make broad architectural changes without approval.

---

## 13. Security Boundary

Treat the following as hard boundaries:

```
Personal Aayam
|
+-- Claude: claude-personal
+-- Claude config: /Users/kalpan/.claude-personal
+-- GitHub: kalpan2302
+-- Remote: https://github.com/kalpan2302/Aayam.git
|
+-- GitHub operations: MANUAL
+-- gh CLI: FORBIDDEN
+-- Secrets/credentials: FORBIDDEN
```

Never cross this boundary into work repositories, work credentials, or the work GitHub
identity.
