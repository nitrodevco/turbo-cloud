---
title: "Client modules draw another room beside the room on screen"
date: 2026-10-27
pr: nitro-next#253
breaks: no
who: "client modules."
---

**What**: `api.room.createSecondary(roomId, options)` draws a room fed with core packets
(`apply`) beside the active one; `api.room.keepWhenLeft(options)` keeps the room being left as
one; entering a drawn room adopts it. `onUnhandledPointer` events carry `secondaryTile`.

**Action**: none; nitro-next `docs/client-modules.md`, "Secondary rooms".
