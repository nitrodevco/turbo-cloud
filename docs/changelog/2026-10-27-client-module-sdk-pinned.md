---
title: "The server's copy of the client module SDK is pinned"
date: 2026-10-27
pr: turbo-cloud#462
breaks: no
who: "no plugin; turbo-cloud contributors updating the copy (`scripts/client-module-sdk.mjs`)."
---

**What**: the SDK runtime the server builds modules with (`Turbo.Plugins/ClientBuild/sdk/`) is
checked against the nitro-next commit it came from (`sdk-pins.json`).

**Action**: none.
