---
title: "A room watch starts with the room as an entry draws it"
date: 2026-10-27
pr: turbo-cloud#465
breaks: no
who: "plugins watching rooms."
---

**What**: `IHotel.Rooms.WatchAsync` first sends the watcher the room's whole view (floor, furni,
avatars, their states), then its stream, in order; a watch renewed after it lapsed starts with a
new view.

**Action**: a plugin that built the first frame itself can stop.
