"""Static architecture checks over an agent's diff.

Each rule is traced to a line of the repository's contract (CLAUDE.md, CONTEXT.md, AGENTS.md)
as it stood when the benchmark was built. The rules are part of the *eval*, deliberately
frozen: the instruction files are what a hillclimb changes, so the grader must not read them.

Rules look only at lines the agent added (and at files it created or touched), so pre-existing
code never counts against it. Severity:
  critical - a stated non-negotiable; caps the case score and blocks "solved"
  warning  - a stated convention; costs architecture points
"""

from __future__ import annotations

import re
from dataclasses import dataclass, field

GATE_FILES = {
    "Directory.Build.props",
    "Directory.Build.targets",
    ".editorconfig",
    ".csharpierrc.json",
    ".csharpierignore",
    "global.json",
    ".config/dotnet-tools.json",
}


@dataclass
class FileDiff:
    path: str
    status: str  # A, M, D, R
    added: list[str] = field(default_factory=list)
    removed: list[str] = field(default_factory=list)


@dataclass
class Finding:
    rule: str
    severity: str
    path: str
    detail: str
    source: str

    def as_dict(self) -> dict:
        return self.__dict__.copy()


def parse_unified_diff(diff: str) -> list[FileDiff]:
    files: list[FileDiff] = []
    cur: FileDiff | None = None
    for line in diff.splitlines():
        if line.startswith("diff --git "):
            m = re.match(r"diff --git a/(.+?) b/(.+)$", line)
            cur = FileDiff(path=m.group(2) if m else line, status="M")
            files.append(cur)
        elif cur is None:
            continue
        elif line.startswith("new file mode"):
            cur.status = "A"
        elif line.startswith("deleted file mode"):
            cur.status = "D"
        elif line.startswith("rename to "):
            cur.status = "R"
            cur.path = line[len("rename to "):]
        elif line.startswith("+++") or line.startswith("---"):
            continue
        elif line.startswith("+"):
            cur.added.append(line[1:])
        elif line.startswith("-"):
            cur.removed.append(line[1:])
    return files


def _is_cs(p: str) -> bool:
    return p.endswith(".cs")


def _in_grain(p: str) -> bool:
    return "/Grains/" in p and _is_cs(p)


def _strip_comment(l: str) -> str:
    return l.split("//", 1)[0]


def check(diff: str) -> list[Finding]:
    out: list[Finding] = []
    for f in parse_unified_diff(diff):
        p = f.path
        added = [_strip_comment(l) for l in f.added]
        blob = "\n".join(added)

        # --- quality gate / tooling tampering -------------------------------------------
        if p in GATE_FILES or p.startswith(".github/workflows/"):
            out.append(Finding("gate-config-modified", "critical", p,
                               "changed build/format/analyzer/CI configuration",
                               "AGENTS.md Priority order 1: build and quality checks in repo files"))
        if p == "Directory.Packages.props" and f.added:
            out.append(Finding("dependency-added", "warning", p,
                               "package versions changed",
                               "AGENTS.md Required standards: avoid introducing new dependencies"))
        if not _is_cs(p) and not p.endswith(".csproj"):
            continue
        if "/Migrations/" not in p and re.search(r"#pragma\s+warning\s+disable|\[SuppressMessage\(|<NoWarn>", blob):
            out.append(Finding("analyzer-suppressed", "warning", p,
                               "suppresses a compiler/analyzer diagnostic",
                               "AGENTS.md Definition of done: no new warnings introduced"))
        if p.endswith(".csproj"):
            continue

        # --- packet handlers ------------------------------------------------------------
        if p.startswith("Turbo.PacketHandlers/"):
            if re.search(r"\b(TurboDbContext|IDbContextFactory|DbContext|DbSet<|ExecuteSql\w*|FromSql\w*|CreateDbContext\w*)\b|_dbCtx", blob):
                out.append(Finding("handler-db-access", "critical", p,
                                   "database context/query in a packet handler",
                                   "CLAUDE.md: do not query database contexts/repositories from packet handlers"))
            if re.search(r"\bISessionGateway\b|\.GetSession\(|\bSendComposersAsync\(", blob):
                out.append(Finding("handler-raw-session-send", "critical", p,
                                   "handler reaches a raw session/socket",
                                   "CLAUDE.md: do not send composers directly to sockets/sessions from handlers"))
            if re.search(r"^\s*(try\s*$|try\s*\{)", blob, re.M):
                out.append(Finding("handler-try-catch", "warning", p,
                                   "handler wraps its body in try/catch",
                                   "AGENTS.md: a handler does not wrap its body in try/catch"))

        # --- outbound routing --------------------------------------------------------------
        if re.search(r"GetPlayerPresenceGrain\([^;]*?\)\s*\.SendComposerAsync\(\s*new\b", blob, re.S):
            out.append(Finding("presence-send-spelled-out", "critical", p,
                               "GetPlayerPresenceGrain(id).SendComposerAsync(new ...) instead of SendComposerToPlayerAsync",
                               "CLAUDE.md: do not spell out GetPlayerPresenceGrain(id).SendComposerAsync(...)"))
        for m in re.finditer(r"\b(?:private|internal|public|protected)[^;{=]*\bTask\b[^;{=]*\s(Send\w*To(?:Player|Players)\w*Async)\s*\(", blob):
            if not p.endswith("GrainFactoryExtensions.cs"):
                out.append(Finding("local-send-helper", "critical", p,
                                   f"declares local send helper {m.group(1)}",
                                   "CLAUDE.md: do not add a local send helper"))

        # --- exceptions and logging -------------------------------------------------------
        if re.search(r"catch\s*(\(\s*Exception\s*(\w+)?\s*\))?\s*\{\s*\}", blob) or re.search(r"catch\s*\{", blob):
            out.append(Finding("silent-catch", "critical", p,
                               "bare or empty catch block",
                               "CONTEXT.md/AGENTS.md: no bare catch { }; catch (Exception ex) and log"))
        if re.search(r"\.Log(?:Trace|Debug|Information|Warning|Error|Critical)\(\s*(?:\w+\s*,\s*)?\$\"", blob) or \
                re.search(r"LogAndForget\([^;]*?,\s*\$\"", blob, re.S):
            out.append(Finding("interpolated-log-template", "warning", p,
                               "string-interpolated log template",
                               "AGENTS.md: log with structured templates, never string interpolation"))
        if re.search(r"\.Ignore\(\)", blob):
            out.append(Finding("task-ignore", "warning", p, ".Ignore() on a task",
                               "CONTEXT.md: replace .Ignore() with LogAndForget"))
        if "Console.Write" in blob and not p.startswith(("Turbo.Main/", "scripts/")):
            out.append(Finding("console-write", "warning", p, "Console.Write in place of the logger",
                               "AGENTS.md: Console.WriteLine in place of the logger is forbidden"))

        # --- grains -----------------------------------------------------------------------
        if _in_grain(p) or p.startswith("Turbo.Rooms/"):
            if re.search(r"\bTask\.Delay\(|\bThread\.Sleep\(", blob):
                out.append(Finding("delay-in-grain", "warning", p, "Task.Delay/Thread.Sleep in grain code",
                                   "AGENTS.md: never Task.Delay inside a grain turn; use RoomTimerSystem"))
            if re.search(r"\block\s*\(|\bSemaphoreSlim\b|\bMonitor\.Enter\b", blob):
                out.append(Finding("manual-lock-in-grain", "warning", p, "manual locking in grain code",
                                   "CONTEXT.md: use grain single-threading; no manual locks"))
        if _in_grain(p):
            if re.search(r"\.ConfigureAwait\(", blob):
                out.append(Finding("configureawait-in-grain", "warning", p, "ConfigureAwait in grain code",
                                   "repo history: ORLEANS0014, grain code does not use ConfigureAwait"))
            if re.search(r"\.Take\(\s*\d+\s*\)", blob):
                out.append(Finding("hardcoded-limit", "warning", p, "hardcoded Take(N) in a grain",
                                   "CONTEXT.md: do not hardcode limits in grains; use IOptions<TConfig>"))
            if re.search(r"GetPrimaryKeyLong\(\)", blob):
                out.append(Finding("grain-key-reread", "warning", p, "reads the grain key via GetPrimaryKeyLong",
                                   "AGENTS.md: the grain key is read once in the constructor"))
        if re.search(r"\[KeepAlive\]", blob):
            out.append(Finding("keepalive-added", "warning", p, "[KeepAlive] added",
                               "CONTEXT.md: [KeepAlive] only for justified infrastructure grains"))

        # --- protocol placement -------------------------------------------------------------
        name = p.rsplit("/", 1)[-1]
        if f.status == "A" and re.search(r"(MessageParser|ComposerSerializer)\.cs$", name) and \
                not re.match(r"Turbo\.Revisions/Revision\d+/", p):
            out.append(Finding("revision-code-misplaced", "critical", p,
                               "parser/serializer outside Turbo.Revisions/Revision<id>/",
                               "CLAUDE.md: for Revision<id> parser/serializer work, edit Turbo.Revisions/Revision<id>/**"))
        if (p.startswith("Turbo.Primitives/Messages/Outgoing/") or "/Snapshots/" in p) and \
                re.search(r"\brecord\b", blob) and f.status == "A" and "GenerateSerializer" not in blob:
            out.append(Finding("missing-generate-serializer", "warning", p,
                               "new composer/snapshot record without [GenerateSerializer]",
                               "AGENTS.md: attribute every type that crosses a grain call"))
        if f.status == "A" and p.startswith("Turbo.Primitives/") and "/Snapshots/" not in p:
            types = re.findall(r"^\s*public\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+|readonly\s+)*(?:class|record|struct|enum|interface)\s+(\w+)", blob, re.M)
            if len(set(types)) > 1:
                out.append(Finding("multiple-public-types", "warning", p,
                                   f"{len(set(types))} public types in one file",
                                   "AGENTS.md: one public type per file"))
    return out


def summarize(findings: list[Finding]) -> dict:
    crit = [f for f in findings if f.severity == "critical"]
    warn = [f for f in findings if f.severity == "warning"]
    return {
        "critical": len(crit),
        "warnings": len(warn),
        "findings": [f.as_dict() for f in findings],
    }


if __name__ == "__main__":
    import json
    import sys

    print(json.dumps(summarize(check(sys.stdin.read())), indent=2))
