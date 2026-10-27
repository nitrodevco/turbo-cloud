---
title: "A replacement reaches the service it replaced through the host"
date: 2026-10-27
pr: turbo-cloud#468
breaks: no
who: "plugins that replace a host service (`docs/plugin-services.md`)."
---

**What**: a plugin that replaced a host service (`IServiceSlot<T>`) and asked the host for that
same contract (`ctx.Host<T>()`, `IHostServices`) got itself back, so a call through it recursed
until the stack overflowed and the server died. It now gets the implementation under its own (as
`slot.Under(this)` gives), worked out at each call; other plugins and the hotel still get the one in
use. A replacement whose constructor takes its own contract now fails its load with "A circular
dependency was detected" instead of overflowing.

**Breaks plugins**: A plugin that relied on `Host<T>()` returning itself crashed the server.

**Action**: none; a plugin that took its own contract by constructor takes the one it replaces
through `ctx.Host<T>()` or `Under(this)`.
