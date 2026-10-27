---
title: "Holds and watches of rooms, and envelope packets"
date: 2026-10-27
pr: turbo-cloud#464
breaks: no
who: "plugins showing one room in another."
---

**What**: `IHotel.Rooms.HoldAsync`/`ReleaseAsync` keep a room loaded and awake with no one in it
(a lease, renewed by calling again); `WatchAsync`/`UnwatchAsync` send a player a room's packets
inside the plugin's envelope message; `IFurniContext.WatchRoomAsync` and `ReadExtraDataJson`.
Everything a plugin holds or watches ends when it unloads.

**Action**: none; `docs/plugin-room-watch.md`.
