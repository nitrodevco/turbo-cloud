"""Select the documentation-only CI path; uncertainty always keeps the full build."""

import os
from pathlib import Path
import subprocess


ROOT_DOCUMENTS = {
    "README.md", "CONTRIBUTING.md", "CHANGELOG.md", "AGENTS.md", "CONTEXT.md",
    ".github/copilot-instructions.md",
}

# Keep these aligned with TurboCloudAiGovernanceCheck in Directory.Build.targets.
REQUIRED_FILES = (
    "AGENTS.md", "CONTEXT.md", ".github/copilot-instructions.md",
    "docs/patterns/ServicePattern.cs", "docs/patterns/HandlerPattern.cs",
    "docs/patterns/UnitTestPattern.cs",
)


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
        allowed = path in ROOT_DOCUMENTS or (path.startswith("docs/") and path.endswith(".md"))
        if status not in ("A", "M") or not allowed:
            return False
        paths.append(path)

    for path in (*REQUIRED_FILES, *paths):
        if not Path(path).is_file() or not Path(path).read_text(encoding="utf-8").strip():
            raise SystemExit(f"Required or changed documentation file is missing or empty: {path}")

    return True


if __name__ == "__main__":
    docs_only = documentation_only()
    print("Documentation checks passed; no build needed." if docs_only else "Full checks required.")
    if output := os.environ.get("GITHUB_OUTPUT"):
        with open(output, "a", encoding="utf-8") as stream:
            stream.write(f"docs_only={str(docs_only).lower()}\n")
