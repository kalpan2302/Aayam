# Aayam

Base branch: **`dev`** (CI runs on `dev` and `main`; branch from `dev`, PR back into `dev`).

AI-powered AR app (Unity) for door-hardware visualization — scan a door, place/visualize
products in AR, check compatibility. Full product context: `docs/PROJECT_CONTEXT.md`,
`docs/ARCHITECTURE.md`. This file carries only what's specific to working in this repo;
everything else (how to answer, git workflow, evidence bar) is the user-level harness.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).

Built 2026-10-10 from code only (`graphify extract . --code-only`): 65 C# files → 1208
nodes / 2131 edges / 56 communities, god nodes `ARHUDController`, `PlacementReticle`,
`ObjectSelector`, `ObjectPlacementController`, `ARSessionController`. The 27 docs + 2 PDFs
under `docs/` were skipped (no API key configured for semantic extraction — graphify never
reads `ANTHROPIC_API_KEY`; it would need `GEMINI_API_KEY`/`GOOGLE_API_KEY` or Claude
subagent dispatch via `/graphify .`). Re-run the full `/graphify .` skill flow if you want
those folded into the graph too.

`graphify-out/` is gitignored — it's deterministic and free to regenerate locally
(`graphify extract . --code-only`, no network call). `graphify hook install` (auto-rebuild
on every commit/checkout) is NOT installed — it was blocked by the permission classifier
since it writes an auto-executing git hook; run it yourself if you want that instead of the
update-after-editing rule above.

## Structure

Unity project lives at the **repo root** (`Assets/`, `Packages/`, `ProjectSettings/`), not
under a `unity/` subdirectory.

- `Assets/_Project/Scripts/<Module>/Aayam.<Module>.asmdef` — one asmdef per module:
  `AR` (placement/interaction/session), `UI`, `Core`, `Models`, `Services`, `Utilities`.
- `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/` — NUnit tests, own asmdefs
  (`Aayam.Tests.EditMode`, `Aayam.Tests.PlayMode`).
- `Assets/Editor/` — editor-only tooling (`URPConfigValidator.cs`, `ProjectStructureCreator.cs`).
- Unity **6000.6.3f1** (see `ProjectSettings/ProjectVersion.txt`); AR Foundation 6.6.2 + ARKit
  (iOS-only right now — no ARCore package in `Packages/manifest.json`).
- `docs/phases/phase-N-*.md` track the roadmap phase by phase; `git log` shows which phase
  is actually done (currently Phase 1, AR Foundation) — trust that over the roadmap doc.

## Known gotcha

**CI workflows point at a `unity/` subdirectory that doesn't exist.** Measured 2026-10-10:
`.github/workflows/test.yml` and `unity-build.yml` both set `projectPath: unity` /
`UNITY_PROJECT_PATH: unity` and cache `unity/Library`, but `Assets/`, `Packages/`, and
`ProjectSettings/` all live at repo root. This is left over from the original planned
layout in the root `README.md` (`unity/ # Unity AR project (future)`); the Phase 1
consolidation commit moved the project to the root without updating CI. Until one of the
two is fixed, `game-ci/unity-test-runner` in CI will fail to find the project — check this
before debugging a red CI run as anything else.

## Build & test

No local headless Unity CLI command has been run/verified in-session yet — CI uses
`game-ci/unity-test-runner@v4` with `projectPath: unity` (see gotcha above; fix the path
before relying on CI). Unity Editor 6000.6.3f1 is installed locally
(`/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app`), matching `ProjectVersion.txt`, so
EditMode/PlayMode tests can be run locally via Unity's batchmode test runner
(`-runTests -testPlatform EditMode|PlayMode -testResults <path>`) against the repo root —
verify the exact invocation before trusting its output. `.github/workflows/unity-build.yml`'s
C# lint step is a placeholder ("Add dotnet-format or other linting when solution files are
available") — `Aayam.slnx` now exists, so that placeholder is stale too.
