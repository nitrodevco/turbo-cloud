"""Environment for nested Claude Code runs (the agent under test and the judge).

The nested CLI must not inherit this orchestrating session's identity or instruction sources
(session ids, messaging sockets, extra CLAUDE.md directories), and gets its own config
directory so user-level settings, skills, hooks and memory do not leak in. Provider/auth
variables are kept so the CLI can reach the API the same way this host does.
"""

from __future__ import annotations

import os

DENY = {
    "CLAUDECODE",
    "CLAUDE_CODE_SESSION_ID",
    "CLAUDE_CODE_REMOTE_SESSION_ID",
    "CLAUDE_CODE_ADDITIONAL_DIRECTORIES_CLAUDE_MD",
    "CLAUDE_ADDITIONAL_DIRECTORIES",
    "CLAUDE_AFTER_LAST_COMPACT",
    "CLAUDE_CODE_CHILD_SESSION",
    "CLAUDE_CODE_MESSAGING_SOCKET",
    "CLAUDE_CODE_MESSAGING_TOKEN",
    "CLAUDE_CODE_SYNC_SESSION_REFS",
    "CLAUDE_CODE_SYNC_SKILLS",
    "CLAUDE_CODE_TEE_SDK_STDOUT",
    "CLAUDE_CODE_DIAGNOSTICS_FILE",
    "CLAUDE_EFFORT",
    "CLAUDE_AUTOCOMPACT_PCT_OVERRIDE",
    "CLAUDE_AUTO_BACKGROUND_TASKS",
    "CLAUDE_CODE_BG_TASKS_REPORT_RUNNING",
    "CLAUDE_PID",
}


def nested_env(config_dir: str, extra: dict | None = None) -> dict:
    env = {k: v for k, v in os.environ.items() if k not in DENY}
    env["CLAUDE_CONFIG_DIR"] = config_dir
    # Builds inside the agent's workspace must not share MSBuild nodes across workspaces.
    env["MSBUILDDISABLENODEREUSE"] = "1"
    env["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"
    env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    env["DOTNET_NOLOGO"] = "1"
    env["PATH"] = env.get("PATH", "") + ":/root/.dotnet:/root/.dotnet/tools"
    env["DOTNET_ROOT"] = "/root/.dotnet"
    if extra:
        env.update(extra)
    return env
