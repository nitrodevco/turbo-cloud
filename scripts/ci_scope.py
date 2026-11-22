"""Select the documentation-only CI path; uncertainty always keeps the full build."""

import os
from pathlib import Path
import re
import subprocess


ROOT_DOCUMENTS = {
    "README.md", "CONTRIBUTING.md", "CHANGELOG.md", "AGENTS.md", "CONTEXT.md", "CLAUDE.md",
    ".github/copilot-instructions.md",
}

# Folders whose markdown is documentation: the docs site, and Claude Code's path-scoped reminders.
DOCUMENT_FOLDERS = ("docs/", ".claude/rules/")

# Keep these aligned with TurboCloudAiGovernanceCheck in Directory.Build.targets.
REQUIRED_FILES = (
    "AGENTS.md", "CONTEXT.md", ".github/copilot-instructions.md",
    "docs/patterns/ServicePattern.cs", "docs/patterns/HandlerPattern.cs",
    "docs/patterns/UnitTestPattern.cs",
)

# Codex reads AGENTS.md only up to 32 KiB. Counted as the governance check counts it: a character
# outside ASCII as three bytes, its most in UTF-8.
AGENTS_MAX_BYTES = 32768


def agents_bytes(text):
    return len(re.sub(r"[^\x00-\x7F]", "___", text))


def documentation_only():
    if os.environ.get("GITHUB_EVENT_NAME") != "pull_request":
        return False

    try:
        # checkout's PR merge commit includes the proposed changes against its first parent.
        # Do not collapse renames: a removed code file must force the full checks.
        fields = subprocess.check_output([
            "git", "diff", "--name-status", "--no-renames", "-z", "HEAD^1", "HEAD",
        ]).decode("utf-8").rstrip("\0").split("\0")
    except (subprocess.CalledProcessError, UnicodeDecodeError):
        print("Could not classify changes; running full checks.")
        return False

    if not fields or len(fields) % 2:
        return False

    paths = []
    for status, path in zip(fields[::2], fields[1::2]):
        allowed = path in ROOT_DOCUMENTS or (path.startswith(DOCUMENT_FOLDERS) and path.endswith(".md"))
        if status not in ("A", "M") or not allowed:
            return False
        paths.append(path)

    for path in (*REQUIRED_FILES, *paths):
        if not Path(path).is_file() or not Path(path).read_text(encoding="utf-8").strip():
            raise SystemExit(f"Required or changed documentation file is missing or empty: {path}")

    size = agents_bytes(Path("AGENTS.md").read_text(encoding="utf-8"))
    if size > AGENTS_MAX_BYTES:
        raise SystemExit(
            f"AGENTS.md is about {size} bytes; Codex reads only the first {AGENTS_MAX_BYTES}. "
            "Move rules for one area to their page in docs/rules/ (AGENTS.md, Rules by area)."
        )

    return True


if __name__ == "__main__":
    docs_only = documentation_only()
    print("Documentation checks passed; no build needed." if docs_only else "Full checks required.")
    if output := os.environ.get("GITHUB_OUTPUT"):
        with open(output, "a", encoding="utf-8") as stream:
            stream.write(f"docs_only={str(docs_only).lower()}\n")
