# Graphify notes

Project reference for the Graphify knowledge graph set up in this repo. Behavioral rules
for Claude live in `CLAUDE.md` § 9 — this file is just facts, and it will go stale; re-check
before trusting a number here.

## Setup (2026-10-10)

- Installed project-scoped: `uv tool install graphifyy`, then `graphify install --platform
  claude --project`. Skill at `.claude/skills/graphify/`, nudge-only PreToolUse hooks in
  `.claude/settings.json` (non-blocking — no `--strict`).
- `graphify hook install` (auto-rebuild graph.json on every git commit/checkout) is **not**
  installed — it was blocked by the permission classifier because it writes an
  auto-executing git hook. Run it manually if you want that instead of rebuilding by hand.
- `graphify-out/` is gitignored. Regenerate anytime with `graphify extract . --code-only` —
  local tree-sitter AST only, no network call, no API key.

## Current graph (built 2026-10-10)

Built from code only (`graphify extract . --code-only`):

- 65 C# files → 1208 nodes / 2131 edges / 56 communities
- God nodes (most connected): `ARHUDController`, `PlacementReticle`, `ObjectSelector`,
  `ObjectPlacementController`, `ARSessionController`

The 27 docs + 2 PDFs under `docs/` were **not** included. Graphify's code-path extraction
(AST) needs no API key at all, but semantic extraction of docs/papers/images needs either
`GEMINI_API_KEY`/`GOOGLE_API_KEY` set, or the full `/graphify .` skill flow (which dispatches
Claude Code subagents to read them instead of calling an external API). Graphify never reads
`ANTHROPIC_API_KEY` or `OPENAI_API_KEY`. Re-run `/graphify .` if you want the docs folded in.

## Re-checking

`graphify god-nodes --top 15` and `graphify query "<question>"` read the live
`graphify-out/graph.json` — trust their output over the numbers above.
