# Turbo Cloud agent benchmark

Measures how reliably a Claude Code agent, working under this repository's instruction files,
fixes **real historical Turbo Cloud bugs** while respecting Turbo's architecture.
It evaluates the *workflow* (model + Claude Code + CLAUDE.md/AGENTS.md/CONTEXT.md/docs/patterns),
not Turbo's production code. Nothing here is compiled into Turbo.

## What a case is

`cases/<id>/`

| file | purpose |
|---|---|
| `case.json` | base commit (the tree before the fix), the historical fix commit(s), domain, difficulty, helper files the hidden tests need, leak fingerprints, judge-only requirements |
| `task.md` | the issue the agent sees: user-visible symptoms and client protocol facts only, never the fix |
| `hidden/*.cs` | xUnit regression tests the agent never sees |

Every hidden test was checked to **fail on the base tree and pass on the historical fix**
(see "Validation" in the design notes). Tests drive Turbo through entry points that existed
before the fix (a room grain method, a module entry point, client bytes through the real
revision parser/handler/serializer) and read back observable results (item heights and
states, wire bytes, log levels, query results), so a different-but-correct implementation
passes.

`harness/` holds the test project template and shared helpers (Orleans-free grain
construction, recording fakes, packet round-trips, EF InMemory/SQLite databases).

## What is graded (in priority order)

1. **Behaviour** – hidden regression tests (all must pass).
2. **TurboCloudQualityGate** – the repo's real gate (CSharpier, build, governance, `dotnet
   format` style + analyzers), plus the repo's existing tests (`scripts/tests`, water harness).
3. **Architecture** – static rules over the agent's added lines, each traced to CLAUDE.md /
   CONTEXT.md / AGENTS.md (`runner/arch_checks.py`). Critical rules block "solved".
4. **Judgement** – an LLM judge (default `claude-sonnet-5-5`, tools disabled) scores scope,
   verification, maintainability, consistency and architectural fit 1-5 with a rubric frozen
   in `runner/judge.py`.

Headline metric: **solved** = hidden tests pass ∧ gate passes ∧ existing tests pass ∧ no
critical architecture finding ∧ no contamination. Secondary: **score 0-100** =
45·hidden-pass-rate + 15 gate + 5 existing tests + 15 architecture + 20 judge, capped at 40 if
hidden tests fail, at 50 if the gate fails or a critical rule fires, 0 for a no-op or a
contaminated run. A strong score is impossible without solving.

Per run it also records input/output/cache tokens, cost (as reported by Claude Code), elapsed
time, turns, tool calls, files/lines changed, tests added and instruction files edited.

## Isolation and leakage controls

- Workspace = `git archive <base>` + instruction overlay + one squashed commit: no future
  history, no remote. Hidden tests live outside the workspace.
- Agent: `claude -p` with its own config dir, project settings only, no MCP, no WebFetch /
  WebSearch, this session's identity variables removed.
- Contamination scan of the transcript (reading the eval, the host repo, or upstream GitHub).
- Leak scan of the variant's instruction files against each case's fingerprints: a
  hillclimbed CLAUDE.md that describes a case's fix invalidates that case's score.
- The grader never reads the instruction files under test (rules and rubric are frozen here).

## Running

```bash
cd evals/turbo-agent/runner
# audit stand-ins (no agent cost): historical fix, and no change
python3 run_eval.py --agent oracle --flow-dir /tmp/audit-oracle --approve-harness
python3 run_eval.py --agent null --no-judge --flow-dir /tmp/audit-null --approve-harness
# the real thing: current main's instruction files, Opus 5.5 at effort high
python3 run_eval.py --variant baseline --instructions-ref 2bdee92 --reps 1 --concurrency 2
# a hillclimb variant: instruction files from a directory
python3 run_eval.py --variant v1 --instructions-dir /path/to/candidate-instructions
# one case's hidden tests against any checkout
python3 hidden.py rooms-rotate-restack /path/to/checkout
```

Results go to `.claude/hillclimb/turbo-agent/<variant>/` (`results.jsonl`, `traces/`,
`errors.jsonl`, `artifacts/<case>_rep<k>/` with patch, gate logs and transcript). The runner
refuses to run when `runner/`, `harness/` or `cases/` changed since the digest recorded with
`--approve-harness`.

Requirements: .NET SDK 10.0.400, Python 3.11, Claude Code CLI, network access to NuGet.
