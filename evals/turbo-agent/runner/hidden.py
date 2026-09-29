"""Assemble and run a case's hidden regression tests against a workspace.

The hidden test project lives *beside* the workspace (never inside it), so the agent's
workspace is not modified by grading and the workspace's Directory.Build.* / central package
management do not apply to the test project.
"""

from __future__ import annotations

import json
import shutil
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path

EVAL_ROOT = Path(__file__).resolve().parents[1]
HARNESS = EVAL_ROOT / "harness"
CASES = EVAL_ROOT / "cases"


def load_case(case_id: str) -> dict:
    case = json.loads((CASES / case_id / "case.json").read_text())
    case["_dir"] = str(CASES / case_id)
    return case


def assemble(case: dict, ws: Path, out: Path) -> Path:
    """Write the hidden test project for `case` into `out`, referencing workspace `ws`."""
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)
    template = (HARNESS / "HiddenTests.csproj.template").read_text()
    (out / "HiddenTests.csproj").write_text(template)
    (out / "Directory.Build.props").write_text(
        f"<Project><PropertyGroup><WS>{ws}</WS></PropertyGroup></Project>\n"
    )
    # Stop MSBuild from walking up into a parent's Directory.Build.targets / packages props.
    (out / "Directory.Build.targets").write_text("<Project />\n")
    (out / "Directory.Packages.props").write_text("<Project />\n")
    for name in case.get("hidden", {}).get("common", []):
        shutil.copy(HARNESS / "common" / name, out / name)
    hidden_dir = Path(case["_dir"]) / "hidden"
    for f in sorted(hidden_dir.glob("*.cs")):
        shutil.copy(f, out / f.name)
    return out


def run(case: dict, ws: Path, out: Path, timeout_s: int = 900) -> dict:
    """Build and run the hidden tests. Returns a dict with counts and a status.

    status: "passed" | "failed" | "compile_error" | "timeout" | "error"
    """
    proj = assemble(case, ws, out)
    trx_dir = proj / "trx"
    cmd = [
        "dotnet",
        "test",
        str(proj / "HiddenTests.csproj"),
        "--logger",
        "trx;LogFileName=hidden.trx",
        "--results-directory",
        str(trx_dir),
        "-nologo",
    ]
    try:
        import os
        env = dict(os.environ, EVAL_WS=str(ws))
        cp = subprocess.run(
            cmd, cwd=proj, capture_output=True, text=True, timeout=timeout_s, env=env
        )
    except subprocess.TimeoutExpired as e:
        return {"status": "timeout", "passed": 0, "failed": 0, "total": 0,
                "log_tail": str(e)[-4000:]}
    log = cp.stdout + "\n" + cp.stderr
    (proj / "dotnet-test.log").write_text(log)
    trx = trx_dir / "hidden.trx"
    if not trx.exists():
        status = "compile_error" if "error CS" in log or "Build FAILED" in log else "error"
        return {"status": status, "passed": 0, "failed": 0, "total": 0,
                "log_tail": _errors(log)}
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    root = ET.parse(trx).getroot()
    results = []
    for r in root.iterfind(".//t:UnitTestResult", ns):
        msg = r.find(".//t:Message", ns)
        results.append({
            "test": r.get("testName"),
            "outcome": r.get("outcome"),
            "message": (msg.text or "")[:600] if msg is not None else "",
        })
    passed = sum(1 for r in results if r["outcome"] == "Passed")
    failed = sum(1 for r in results if r["outcome"] != "Passed")
    return {
        "status": "passed" if failed == 0 and passed > 0 else "failed",
        "passed": passed,
        "failed": failed,
        "total": len(results),
        "results": results,
        "log_tail": "" if failed == 0 else _errors(log),
    }


def _errors(log: str) -> str:
    lines = [l for l in log.splitlines() if "error" in l.lower() or "Failed " in l or "Assert" in l]
    return "\n".join(lines[-40:])[-4000:]


if __name__ == "__main__":
    import argparse

    ap = argparse.ArgumentParser(description="Run a case's hidden tests against a workspace")
    ap.add_argument("case")
    ap.add_argument("ws")
    ap.add_argument("--out", default=None)
    a = ap.parse_args()
    c = load_case(a.case)
    o = Path(a.out or f"/tmp/turbo-eval/hidden-{a.case}")
    res = run(c, Path(a.ws).resolve(), o)
    print(json.dumps({k: v for k, v in res.items() if k != "results"}, indent=2))
    for r in res.get("results", []):
        print(f"  {r['outcome']:8} {r['test']}  {r['message'][:200]}")
