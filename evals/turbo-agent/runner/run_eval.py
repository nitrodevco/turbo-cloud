#!/usr/bin/env python3
"""Turbo Cloud agent benchmark runner.

For each (case, rep):
  1. Rebuild the repository as it was before the historical fix (`git archive` of the base
     commit into a fresh directory, one squashed commit, no history, no remote), overlay the
     instruction files of the variant under test, and run the repository's bootstrap.
  2. Run Claude Code headless on the case's issue text in that workspace, isolated from this
     session (own config dir, no web tools, no MCP), recording the full transcript.
  3. Grade the end state: hidden regression tests (kept outside the workspace), the real
     TurboCloudQualityGate, the repository's existing tests, static architecture checks, a
     contamination scan of the transcript, and an LLM judge for judgement-only dimensions.
  4. Write one results.jsonl row per (case, rep) as it completes; failures of the harness
     itself go to errors.jsonl so resume re-runs them.

Usage:
  python3 run_eval.py --variant baseline --instructions-ref 2bdee92 --reps 1 --concurrency 2
"""

from __future__ import annotations

import argparse
import concurrent.futures as cf
import hashlib
import json
import math
import os
import re
import shutil
import signal
import subprocess
import sys
import threading
import time
import traceback
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import arch_checks  # noqa: E402
import hidden  # noqa: E402
import judge  # noqa: E402
from agent_env import nested_env  # noqa: E402

EVAL_ROOT = Path(__file__).resolve().parents[1]
REPO = EVAL_ROOT.parents[1]
CASES_DIR = EVAL_ROOT / "cases"
DEFAULT_FLOW = REPO / ".claude" / "hillclimb" / "turbo-agent"
WORK = Path(os.environ.get("TURBO_EVAL_WORK", "/tmp/turbo-eval/runs"))

# The instruction surface a hillclimb may change. Everything else in the workspace is the
# historical tree of the case's base commit.
INSTRUCTION_PATHS = [
    "CLAUDE.md",
    "AGENTS.md",
    "CONTEXT.md",
    "CODEX.md",
    ".github/copilot-instructions.md",
    "docs/patterns",
    ".claude",
]

PROMPT_TEMPLATE = """\
You are working in a checkout of the Turbo Cloud repository. Resolve the issue below.

Work autonomously: nobody will answer questions during this task. When you are done, the
change should be in the working tree of this repository.

<issue>
{issue}
</issue>
"""

AGENT_TOOLS = "Bash Read Edit Write Glob Grep NotebookEdit TodoWrite Task Agent"
AGENT_DISALLOWED = "WebFetch WebSearch"

_print_lock = threading.Lock()


def log(msg: str) -> None:
    with _print_lock:
        print(time.strftime("%H:%M:%S"), msg, flush=True)


def sh(cmd, cwd=None, timeout=None, env=None, check=False, input=None) -> subprocess.CompletedProcess:
    cp = subprocess.run(cmd, cwd=cwd, timeout=timeout, env=env, capture_output=True, text=True,
                        shell=isinstance(cmd, str), input=input)
    if check and cp.returncode != 0:
        raise RuntimeError(f"command failed ({cp.returncode}): {cmd}\n{cp.stdout[-3000:]}\n{cp.stderr[-3000:]}")
    return cp


# ---------------------------------------------------------------------------------------------
# Workspace
# ---------------------------------------------------------------------------------------------

def load_cases(selected: list[str] | None) -> list[dict]:
    cases = []
    for d in sorted(CASES_DIR.iterdir()):
        if not (d / "case.json").exists():
            continue
        c = hidden.load_case(d.name)
        c["task"] = (d / "task.md").read_text()
        if selected and c["id"] not in selected:
            continue
        cases.append(c)
    return cases


def export_tree(ref: str, dest: Path, paths: list[str] | None = None) -> None:
    dest.mkdir(parents=True, exist_ok=True)
    args = ["git", "-C", str(REPO), "archive", ref]
    if paths:
        existing = [p for p in paths
                    if sh(["git", "-C", str(REPO), "cat-file", "-e", f"{ref}:{p}"]).returncode == 0]
        if not existing:
            return
        args += ["--"] + existing
    archive = subprocess.run(args, capture_output=True, check=True).stdout
    subprocess.run(["tar", "-x", "-C", str(dest)], input=archive, check=True)


def overlay_instructions(ws: Path, variant: dict) -> None:
    for p in INSTRUCTION_PATHS:
        target = ws / p
        if target.is_dir():
            shutil.rmtree(target)
        elif target.exists():
            target.unlink()
    if variant.get("instructions_dir"):
        src = Path(variant["instructions_dir"])
        for p in INSTRUCTION_PATHS:
            s = src / p
            if s.is_dir():
                shutil.copytree(s, ws / p)
            elif s.exists():
                (ws / p).parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(s, ws / p)
    else:
        export_tree(variant["instructions_ref"], ws, INSTRUCTION_PATHS)


def prepare_workspace(case: dict, variant: dict, run_dir: Path) -> tuple[Path, str]:
    ws = run_dir / "ws"
    if run_dir.exists():
        shutil.rmtree(run_dir)
    run_dir.mkdir(parents=True)
    src = case["source"]
    export_tree(src["base"], ws)
    for ref, files in (src.get("base_files_from") or {}).items():
        for f in files:
            content = subprocess.run(["git", "-C", str(REPO), "show", f"{ref}:{f}"],
                                     capture_output=True, check=True).stdout
            (ws / f).write_bytes(content)
    overlay_instructions(ws, variant)
    sh(["git", "init", "-q", "-b", "main"], cwd=ws, check=True)
    sh(["git", "config", "user.email", "eval@turbo.local"], cwd=ws, check=True)
    sh(["git", "config", "user.name", "Turbo Eval"], cwd=ws, check=True)
    sh(["git", "add", "-A"], cwd=ws, check=True)
    sh(["git", "commit", "-q", "-m", "Turbo Cloud (base)"], cwd=ws, check=True)
    base_sha = sh(["git", "rev-parse", "HEAD"], cwd=ws, check=True).stdout.strip()
    # The repository's own setup: SDK check, git hooks, local settings, smoke build.
    boot = sh(["sh", "scripts/bootstrap.sh"], cwd=ws, timeout=1200, env=nested_env("/tmp"))
    (run_dir / "bootstrap.log").write_text(boot.stdout + boot.stderr)
    if boot.returncode != 0:
        raise RuntimeError(f"bootstrap failed for base of {case['id']}: {boot.stdout[-2000:]}")
    return ws, base_sha


def instruction_leaks(ws: Path, case: dict) -> list[str]:
    hits = []
    pats = [re.compile(p, re.I) for p in case.get("leak_patterns", [])]
    for p in INSTRUCTION_PATHS:
        root = ws / p
        files = [root] if root.is_file() else list(root.rglob("*")) if root.is_dir() else []
        for f in files:
            if not f.is_file():
                continue
            text = f.read_text(errors="ignore")
            for pat in pats:
                if pat.search(text):
                    hits.append(f"{f.relative_to(ws)}: /{pat.pattern}/")
    return hits


# ---------------------------------------------------------------------------------------------
# Agent
# ---------------------------------------------------------------------------------------------

def scripted_agent(ws: Path, run_dir: Path, case: dict, mode: str) -> dict:
    """Audit stand-ins for the agent: 'oracle' applies the historical fix, 'null' changes nothing."""
    (run_dir / "transcript.jsonl").write_text("")
    if mode == "oracle":
        src = case["source"]
        for c in src["fix_commits"]:
            args = ["git", "-C", str(REPO), "diff", "--binary", f"{c}^", c]
            if src.get("fix_paths"):
                args += ["--"] + src["fix_paths"]
            patch = subprocess.run(args, capture_output=True, check=True).stdout
            ap = subprocess.run(["git", "apply", "--whitespace=nowarn", "-"], cwd=ws, input=patch, capture_output=True)
            if ap.returncode != 0:
                raise RuntimeError(f"oracle patch did not apply: {ap.stderr.decode()[-2000:]}")
    return {"status": "ok", "exit_code": 0, "elapsed_s": 0.0, "api_duration_s": 0.0, "num_turns": 0,
            "cost_usd": 0.0, "is_error": False, "subtype": mode, "result_text": mode,
            "usage": {"input_tokens": 0, "output_tokens": 0, "cache_read_input_tokens": 0,
                      "cache_creation_input_tokens": 0},
            "model_usage": {}, "served_models": [mode], "events": []}


def run_agent(ws: Path, run_dir: Path, case: dict, args) -> dict:
    if args.agent != "claude":
        return scripted_agent(ws, run_dir, case, args.agent)
    prompt = PROMPT_TEMPLATE.format(issue=case["task"].strip())
    cfg = run_dir / "claude-config"
    cfg.mkdir(exist_ok=True)
    transcript = run_dir / "transcript.jsonl"
    cmd = [
        "claude", "-p", prompt,
        "--model", args.model,
        "--effort", args.effort,
        "--output-format", "stream-json", "--verbose",
        "--setting-sources", "project",
        "--strict-mcp-config",
        "--permission-mode", "acceptEdits",
        "--allowedTools", AGENT_TOOLS,
        "--disallowedTools", AGENT_DISALLOWED,
        "--no-session-persistence",
        "--max-budget-usd", str(args.max_budget_usd),
    ]
    started = time.time()
    status = "ok"
    with open(transcript, "w") as out, open(run_dir / "agent.stderr", "w") as err:
        proc = subprocess.Popen(cmd, cwd=ws, stdout=out, stderr=err, env=nested_env(str(cfg)),
                                start_new_session=True)
        try:
            proc.wait(timeout=args.timeout_min * 60)
        except subprocess.TimeoutExpired:
            status = "timeout"
            os.killpg(proc.pid, signal.SIGTERM)
            try:
                proc.wait(timeout=30)
            except subprocess.TimeoutExpired:
                os.killpg(proc.pid, signal.SIGKILL)
    elapsed = time.time() - started
    final = None
    events = []
    for line in transcript.read_text().splitlines():
        try:
            ev = json.loads(line)
        except json.JSONDecodeError:
            continue
        events.append(ev)
        if ev.get("type") == "result":
            final = ev
    if final is None and status == "ok":
        status = "no_result"
    usage = (final or {}).get("usage", {}) or {}
    model_usage = (final or {}).get("modelUsage", {}) or {}
    return {
        "status": status,
        "exit_code": proc.returncode,
        "elapsed_s": round(elapsed, 1),
        "api_duration_s": round(((final or {}).get("duration_api_ms") or 0) / 1000, 1),
        "num_turns": (final or {}).get("num_turns"),
        "cost_usd": (final or {}).get("total_cost_usd"),
        "is_error": (final or {}).get("is_error"),
        "subtype": (final or {}).get("subtype"),
        "result_text": ((final or {}).get("result") or "")[:4000],
        "usage": {
            "input_tokens": sum(m.get("inputTokens", 0) for m in model_usage.values()) or usage.get("input_tokens", 0),
            "output_tokens": sum(m.get("outputTokens", 0) for m in model_usage.values()) or usage.get("output_tokens", 0),
            "cache_read_input_tokens": sum(m.get("cacheReadInputTokens", 0) for m in model_usage.values()),
            "cache_creation_input_tokens": sum(m.get("cacheCreationInputTokens", 0) for m in model_usage.values()),
        },
        "model_usage": model_usage,
        "served_models": list(model_usage.keys()),
        "events": events,
    }


def to_trace(events: list[dict], prompt: str) -> list[dict]:
    turns: list[dict] = [{"role": "user", "content": prompt}]
    for ev in events:
        t = ev.get("type")
        if t == "system" and ev.get("subtype") == "init":
            turns.insert(0, {"role": "system", "content": json.dumps(
                {k: ev.get(k) for k in ("model", "cwd", "tools", "permissionMode", "claude_code_version")}, indent=2)})
        elif t == "assistant":
            thinking = None
            for block in (ev.get("message") or {}).get("content", []):
                bt = block.get("type")
                if bt == "thinking":
                    thinking = block.get("thinking") or None
                elif bt == "text" and block.get("text", "").strip():
                    turns.append({"role": "assistant", "content": block["text"], **({"thinking": thinking} if thinking else {})})
                    thinking = None
                elif bt == "tool_use":
                    turns.append({"role": "tool_call", "name": block.get("name", ""),
                                  "content": json.dumps(block.get("input", {}), indent=2)[:20000],
                                  **({"thinking": thinking} if thinking else {})})
                    thinking = None
        elif t == "user":
            content = (ev.get("message") or {}).get("content", [])
            if isinstance(content, list):
                for block in content:
                    if block.get("type") == "tool_result":
                        c = block.get("content")
                        if isinstance(c, list):
                            c = "\n".join(x.get("text", "") for x in c if isinstance(x, dict))
                        turns.append({"role": "tool_result", "content": str(c)[:20000]})
        elif t == "result":
            turns.append({"role": "assistant", "content": f"[final] {ev.get('result', '')}"})
    return turns


CONTAMINATION = [
    (re.compile(r"/home/user/turbo-cloud|evals/turbo-agent|hillclimb/turbo-agent|/tmp/turbo-eval/(?!runs/)"), "read the eval or the host repository"),
    (re.compile(r"github\.com/nitrodevco|api\.github\.com|nitrodevco/turbo-cloud"), "fetched the upstream repository"),
    (re.compile(r"\bgit\s+(fetch|pull|clone|remote\s+add)\b"), "fetched history"),
]


def contamination(events: list[dict], ws: Path) -> list[str]:
    hits = []
    for ev in events:
        if ev.get("type") != "assistant":
            continue
        for block in (ev.get("message") or {}).get("content", []):
            if block.get("type") != "tool_use":
                continue
            s = json.dumps(block.get("input", {}))
            s_ws = s.replace(str(ws), "<ws>")
            for pat, why in CONTAMINATION:
                if pat.search(s_ws):
                    hits.append(f"{why}: {block.get('name')} {s_ws[:200]}")
    return hits


# ---------------------------------------------------------------------------------------------
# Grading
# ---------------------------------------------------------------------------------------------

def capture_diff(ws: Path, base_sha: str) -> str:
    sh(["git", "add", "-A"], cwd=ws)
    return sh(["git", "diff", "--cached", "--no-color", "--binary", base_sha, "--",
               ".", ":(exclude)appsettings.Development.json"], cwd=ws).stdout


def quality_gate(ws: Path, art: Path) -> dict:
    env = nested_env("/tmp")
    cp = sh(["dotnet", "build", "Turbo.Main/Turbo.Main.csproj", "-t:TurboCloudQualityGate", "-nologo"],
            cwd=ws, timeout=1800, env=env)
    (art / "quality_gate.log").write_text(cp.stdout + cp.stderr)
    res = {"passed": cp.returncode == 0, "failed_steps": []}
    if cp.returncode == 0:
        return res
    # Attribute the failure to the gate's own steps (the commands in Directory.Build.targets).
    steps = {
        "csharpier": ["dotnet", "csharpier", "check", "."],
        "build": ["dotnet", "build", "Turbo.Main/Turbo.Main.csproj", "--no-restore", "-nologo"],
        "format-style": ["dotnet", "format", "Turbo.Main/Turbo.Main.csproj", "style", "--verify-no-changes", "--no-restore"],
        "format-analyzers": ["dotnet", "format", "Turbo.Main/Turbo.Main.csproj", "analyzers", "--verify-no-changes", "--no-restore"],
    }
    for name, cmd in steps.items():
        s = sh(cmd, cwd=ws, timeout=1200, env=env)
        (art / f"gate_{name}.log").write_text(s.stdout + s.stderr)
        if s.returncode != 0:
            res["failed_steps"].append(name)
    errs = re.findall(r"error [A-Z]+\d+: [^\[]+", cp.stdout)
    res["errors"] = sorted(set(e.strip() for e in errs))[:20]
    return res


def existing_tests(ws: Path, art: Path) -> dict:
    env = nested_env("/tmp")
    out = {"suites": {}, "passed": True}
    if (ws / "scripts/tests").is_dir() and list((ws / "scripts/tests").glob("test_*.py")):
        cp = sh([sys.executable, "-m", "unittest", "discover", "-s", "scripts/tests"], cwd=ws, timeout=600, env=env)
        out["suites"]["scripts/tests (python)"] = cp.returncode == 0
        (art / "existing_python.log").write_text(cp.stdout + cp.stderr)
    for proj in sorted(ws.glob("scripts/tests/*/*.csproj")):
        cp = sh(["dotnet", "run", "--project", str(proj.relative_to(ws))], cwd=ws, timeout=900, env=env)
        out["suites"][str(proj.relative_to(ws))] = cp.returncode == 0
        (art / f"existing_{proj.stem}.log").write_text(cp.stdout + cp.stderr)
    out["passed"] = all(out["suites"].values()) if out["suites"] else True
    return out


def reference_patch(case: dict) -> str:
    src = case["source"]
    parts = []
    for c in src["fix_commits"]:
        args = ["git", "-C", str(REPO), "show", "--format=%s%n%n%b", c]
        if src.get("fix_paths"):
            args += ["--"] + src["fix_paths"]
        parts.append(sh(args).stdout)
    return "\n".join(parts)


def diff_stats(diff: str) -> dict:
    files = arch_checks.parse_unified_diff(diff)
    return {
        "files_changed": len(files),
        "lines_added": sum(len(f.added) for f in files),
        "lines_removed": sum(len(f.removed) for f in files),
        "paths": [f"{f.status} {f.path}" for f in files],
        "instruction_files_edited": [f.path for f in files
                                     if any(f.path == p or f.path.startswith(p + "/") for p in INSTRUCTION_PATHS)],
        "tests_added": [f.path for f in files if f.status == "A" and (
            "scripts/tests/" in f.path or re.search(r"Tests?\.cs$", f.path))],
    }


def compute_score(g: dict) -> dict:
    """0-100. Behaviour dominates; a patch that fails behaviour or architecture cannot score high."""
    ht = g["hidden"]
    frac = (ht["passed"] / ht["total"]) if ht.get("total") else 0.0
    behaviour_ok = ht.get("status") == "passed"
    gate_ok = g["quality_gate"]["passed"]
    tests_ok = g["existing_tests"]["passed"]
    crit = g["arch"]["critical"]
    warn = g["arch"]["warnings"]
    contaminated = bool(g["contamination"]) or bool(g["instruction_leaks"])
    j = (g.get("judge") or {}).get("verdict")
    judge_frac = ((j["mean"] - 1) / 4) if j else 0.0

    parts = {
        "behaviour": 45 * frac,
        "quality_gate": 15 if gate_ok else 0,
        "existing_tests": 5 if tests_ok else 0,
        "architecture": max(0.0, 15 - 7.5 * crit - 2.5 * warn),
        "judge": 20 * judge_frac,
    }
    score = sum(parts.values())
    caps = []
    if not behaviour_ok:
        score = min(score, 40)
        caps.append("hidden tests failing: <=40")
    if not gate_ok:
        score = min(score, 50)
        caps.append("quality gate failing: <=50")
    if crit:
        score = min(score, 50)
        caps.append("critical architecture violation: <=50")
    if g["diff_stats"]["files_changed"] == 0:
        score = 0
        caps.append("no-op: 0")
    if contaminated:
        score = 0
        caps.append("contaminated: 0")
    solved = behaviour_ok and gate_ok and tests_ok and crit == 0 and not contaminated
    return {"score": round(score, 1), "solved": solved, "parts": {k: round(v, 1) for k, v in parts.items()},
            "caps": caps, "hidden_pass_rate": round(frac, 3)}


def grade(case: dict, ws: Path, base_sha: str, run_dir: Path, agent: dict, args) -> dict:
    art = run_dir / "artifacts"
    art.mkdir(exist_ok=True)
    diff = capture_diff(ws, base_sha)
    (art / "agent.patch").write_text(diff)
    g: dict = {"diff_stats": diff_stats(diff)}
    g["instruction_leaks"] = instruction_leaks(ws, case)
    g["contamination"] = contamination(agent["events"], ws)
    g["arch"] = arch_checks.summarize(arch_checks.check(diff))
    log(f"  [{case['id']}] quality gate...")
    g["quality_gate"] = quality_gate(ws, art)
    g["existing_tests"] = existing_tests(ws, art)
    log(f"  [{case['id']}] hidden tests...")
    g["hidden"] = hidden.run(case, ws, run_dir / "hidden")
    g["hidden"].pop("log_tail", None) if g["hidden"].get("status") == "passed" else None
    if g["diff_stats"]["files_changed"] and not args.no_judge:
        objective = {
            "hidden_tests": f"{g['hidden'].get('passed', 0)}/{g['hidden'].get('total', 0)} passed ({g['hidden'].get('status')})",
            "quality_gate": "passed" if g["quality_gate"]["passed"] else f"failed: {g['quality_gate'].get('failed_steps')}",
            "static_findings": [f"{f['severity']} {f['rule']} {f['path']}: {f['detail']}" for f in g["arch"]["findings"]],
            "files_changed": g["diff_stats"]["paths"],
        }
        try:
            g["judge"] = judge.run_judge(args.judge_model, case["task"], reference_patch(case), diff, objective,
                                         case.get("judge_only_requirements", []))
        except Exception as e:  # judge failure must not erase the objective grade
            g["judge"] = {"error": str(e)[:2000]}
    g["score"] = compute_score(g)
    return g


# ---------------------------------------------------------------------------------------------
# Orchestration
# ---------------------------------------------------------------------------------------------

def harness_sha() -> str:
    h = hashlib.sha256()
    for root in ("runner", "harness", "cases"):
        for f in sorted((EVAL_ROOT / root).rglob("*")):
            if f.is_file() and "__pycache__" not in f.parts:
                h.update(str(f.relative_to(EVAL_ROOT)).encode())
                h.update(f.read_bytes())
    return h.hexdigest()


def write_state(flow: Path, args, approve: bool) -> None:
    state_path = flow / "_state.json"
    state = json.loads(state_path.read_text()) if state_path.exists() else {}
    sha = harness_sha()
    if approve:
        state["harness_sha"] = sha
    elif state.get("harness_sha") != sha:
        print(f"Harness changed since it was approved (or never approved).\n"
              f"Review the diff under evals/turbo-agent and re-run with --approve-harness.", file=sys.stderr)
        sys.exit(2)
    state.setdefault("flow", "turbo-agent")
    state["metrics"] = [
        {"id": "solved", "label": "Solved", "kind": "binary"},
        {"id": "score", "label": "Score (0-100)", "kind": "continuous"},
        {"id": "hidden_pass_rate", "label": "Hidden tests", "kind": "continuous"},
        {"id": "quality_gate", "label": "Quality gate", "kind": "binary"},
        {"id": "arch_clean", "label": "No crit. arch", "kind": "binary"},
        {"id": "judge_mean", "label": "Judge (1-5)", "kind": "continuous"},
    ]
    state["perf_fields"] = ["latency_s", "usage", "cost_usd", "tool_calls"]
    state["prices"] = {"claude-opus-5-5": {"in": 4.0, "out": 20.0}, "claude-sonnet-5-5": {"in": 2.0, "out": 10.0}}
    state_path.write_text(json.dumps(state, indent=2) + "\n")


def done_keys(results: Path) -> set[tuple[str, int]]:
    keys = set()
    if results.exists():
        for line in results.read_text().splitlines():
            try:
                r = json.loads(line)
                keys.add((r["prompt_id"], r["meta"]["rep"]))
            except Exception:
                pass
    return keys


def run_one(case: dict, rep: int, variant: dict, vdir: Path, args, results_lock: threading.Lock) -> None:
    key = f"{case['id']}_rep{rep}"
    run_dir = WORK / variant["name"] / key
    t0 = time.time()
    try:
        log(f"[{key}] preparing workspace at base {case['source']['base']}")
        ws, base_sha = prepare_workspace(case, variant, run_dir)
        log(f"[{key}] running agent ({args.agent}: {args.model}, effort {args.effort})")
        agent = run_agent(ws, run_dir, case, args)
        if args.agent == "claude" and (agent["status"] == "no_result" or (
                agent["served_models"] and not any(args.model in m for m in agent["served_models"]))):
            raise RuntimeError(f"agent run unusable: status={agent['status']} served={agent['served_models']} "
                               f"exit={agent['exit_code']}")
        log(f"[{key}] agent finished: {agent['status']} in {agent['elapsed_s']}s, ${agent['cost_usd']}")
        g = grade(case, ws, base_sha, run_dir, agent, args)
    except Exception as e:
        with results_lock, open(vdir / "errors.jsonl", "a") as f:
            f.write(json.dumps({"prompt_id": case["id"], "rep": rep, "failure_class": "harness_error",
                                "error": str(e)[:4000], "traceback": traceback.format_exc()[-4000:],
                                "elapsed_s": round(time.time() - t0, 1)}) + "\n")
        log(f"[{key}] HARNESS ERROR: {str(e)[:300]}")
        return

    prompt = PROMPT_TEMPLATE.format(issue=case["task"].strip())
    trace = to_trace(agent["events"], prompt)
    (vdir / "traces").mkdir(exist_ok=True)
    (vdir / "traces" / f"{case['id']}_rep{rep}.json").write_text(json.dumps(trace, indent=1))
    art_dst = vdir / "artifacts" / key
    art_dst.mkdir(parents=True, exist_ok=True)
    for f in (run_dir / "artifacts").iterdir():
        shutil.copy2(f, art_dst / f.name)
    shutil.copy2(run_dir / "transcript.jsonl", art_dst / "transcript.jsonl")

    s = g["score"]
    j = (g.get("judge") or {}).get("verdict")
    row = {
        "prompt_id": case["id"],
        "prompt": case["task"],
        "tags": [case["domain"].split("/")[0], case["domain"], case["difficulty"]],
        "stop_reason": agent.get("subtype"),
        "status": "ok",
        "grade": {
            "solved": 1.0 if s["solved"] else 0.0,
            "score": s["score"],
            "hidden_pass_rate": s["hidden_pass_rate"],
            "quality_gate": 1.0 if g["quality_gate"]["passed"] else 0.0,
            "arch_clean": 1.0 if g["arch"]["critical"] == 0 else 0.0,
            "judge_mean": round(j["mean"], 2) if j else None,
        },
        "explanation": {
            "score": "; ".join(s["caps"]) or "uncapped",
            "judge_mean": (j or {}).get("rationale", (g.get("judge") or {}).get("error", "")),
        },
        "model": (agent["served_models"] or [args.model])[0],
        "usage": agent["usage"],
        "cost_usd": agent["cost_usd"],
        "latency_s": agent["elapsed_s"],
        "tool_calls": sum(1 for t in trace if t["role"] == "tool_call"),
        "judge_model": (g.get("judge") or {}).get("judge_model"),
        "judge_usage": (g.get("judge") or {}).get("judge_usage"),
        "meta": {
            "rep": rep,
            "variant": variant["name"],
            "base": case["source"]["base"],
            "agent_status": agent["status"],
            "num_turns": agent["num_turns"],
            "api_duration_s": agent["api_duration_s"],
            "judge_cost_usd": (g.get("judge") or {}).get("judge_cost_usd"),
            "score_parts": s["parts"],
            "hidden": {k: v for k, v in g["hidden"].items() if k != "results"},
            "hidden_results": g["hidden"].get("results", []),
            "quality_gate": g["quality_gate"],
            "existing_tests": g["existing_tests"],
            "arch": g["arch"],
            "judge": j,
            "diff_stats": g["diff_stats"],
            "contamination": g["contamination"],
            "instruction_leaks": g["instruction_leaks"],
            "agent_result": agent["result_text"][:2000],
            "grading_s": round(time.time() - t0 - agent["elapsed_s"], 1),
        },
    }
    with results_lock, open(vdir / "results.jsonl", "a") as f:
        f.write(json.dumps(row) + "\n")
    log(f"[{key}] solved={s['solved']} score={s['score']} hidden={g['hidden'].get('passed')}/{g['hidden'].get('total')} "
        f"gate={g['quality_gate']['passed']} arch_crit={g['arch']['critical']}")
    if not args.keep_workspaces:
        shutil.rmtree(run_dir / "ws", ignore_errors=True)


def summarize(vdir: Path) -> None:
    rows = [json.loads(l) for l in (vdir / "results.jsonl").read_text().splitlines()] if (vdir / "results.jsonl").exists() else []
    if not rows:
        print("no results")
        return
    n = len(rows)
    solved = sum(r["grade"]["solved"] for r in rows)
    p = solved / n
    ci = 1.96 * math.sqrt(p * (1 - p) / n) if n > 1 else 0
    cost = sum((r.get("cost_usd") or 0) + ((r["meta"].get("judge_cost_usd")) or 0) for r in rows)
    print(f"\n{vdir.name}: solved {solved:.0f}/{n} = {p:.0%} (±{ci:.0%} 95% CI), "
          f"mean score {sum(r['grade']['score'] for r in rows)/n:.1f}, total cost ${cost:.2f}")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--variant", default="baseline", help="baseline or v<N>")
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--instructions-ref", default="2bdee92", help="git ref whose instruction files are overlaid")
    g.add_argument("--instructions-dir", help="directory holding the instruction files to overlay")
    ap.add_argument("--cases", nargs="*", help="case ids (default: all)")
    ap.add_argument("--reps", type=int, default=1)
    ap.add_argument("--concurrency", type=int, default=2)
    ap.add_argument("--model", default="claude-opus-5-5")
    ap.add_argument("--effort", default="high")
    ap.add_argument("--judge-model", default="claude-sonnet-5-5")
    ap.add_argument("--no-judge", action="store_true")
    ap.add_argument("--agent", choices=["claude", "oracle", "null"], default="claude",
                    help="oracle/null are audit stand-ins (historical fix / no change)")
    ap.add_argument("--timeout-min", type=int, default=60)
    ap.add_argument("--max-budget-usd", type=float, default=40.0)
    ap.add_argument("--flow-dir", default=str(DEFAULT_FLOW))
    ap.add_argument("--keep-workspaces", action="store_true")
    ap.add_argument("--approve-harness", action="store_true",
                    help="record the current runner/harness/cases digest as approved")
    args = ap.parse_args()

    if not re.fullmatch(r"baseline|v\d+", args.variant):
        sys.exit("--variant must be 'baseline' or v<N> (the report builder ignores anything else)")
    flow = Path(args.flow_dir)
    vdir = flow / args.variant
    vdir.mkdir(parents=True, exist_ok=True)
    write_state(flow, args, args.approve_harness)

    variant = {"name": args.variant, "instructions_ref": args.instructions_ref,
               "instructions_dir": args.instructions_dir}
    (vdir / "variant.json").write_text(json.dumps({**variant, "model": args.model, "effort": args.effort,
                                                   "judge_model": args.judge_model,
                                                   "harness_sha": harness_sha()}, indent=2) + "\n")
    cases = load_cases(args.cases)
    done = done_keys(vdir / "results.jsonl")
    todo = [(c, r) for c in cases for r in range(args.reps) if (c["id"], r) not in done]
    log(f"{len(todo)} runs to do ({len(done)} already done) with concurrency {args.concurrency}")
    lock = threading.Lock()
    with cf.ThreadPoolExecutor(max_workers=args.concurrency) as ex:
        futs = [ex.submit(run_one, c, r, variant, vdir, args, lock) for c, r in todo]
        for f in cf.as_completed(futs):
            f.result()
    summarize(vdir)


if __name__ == "__main__":
    main()
