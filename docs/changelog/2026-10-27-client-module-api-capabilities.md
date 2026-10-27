---
title: "Client module API: capabilities, core packet parsing, unused pointer events"
date: 2026-10-27
pr: nitro-next#252
breaks: no
who: "client modules."
---

**What**: `api.capabilities.declare(name, version?)` asks the server for an extension of the
plugin's while the module runs (`version`, `subscribe` read the answer);
`api.packets.parseCore(header, body)` reads a core packet with the client's own parser;
`api.room.onUnhandledPointer(listener)` hears pointer events over the room that the room did not
use (return `true` to keep the room from acting on it).

**Action**: none; nitro-next `docs/client-modules.md`.
