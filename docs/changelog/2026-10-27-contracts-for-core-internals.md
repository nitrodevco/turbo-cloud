---
title: "Contracts for what plugins built on core's internals"
date: 2026-10-27
pr: turbo-cloud#466
breaks: yes
who: "every plugin that used `Turbo.Rooms` or `Turbo.Furniture` (no longer compilable since turbo-cloud#463); wired effects that showed a message; tests that build `RoomUser`/`RoomFurni`."
---

**What**: `IRoomTurn` (a room in its turn: `RoomUsers`, `Map`, `SayAsync` with `RoomSpeech`,
`SetEffectAsync`, `StopWalkingAsync`, `SetRotationAsync`, bot figure and following, temporary
furni), and `IFurniContext` is one. `FurniLogic` gains `OnDetachedAsync`, `UsagePolicy`,
`Walkable`; `IFurniContext` gains `WalkToInOrderAsync`, `TileInFront`, `TilesBeside`,
`StandsBeside`, `Schedule`/`CancelSchedule`. `RoomUser` gains `Rotation`, `IsWalking`, `IsIdle`,
`EffectId`; `RoomFurni` gains `Rotation`, `Definition`. `ICommandRoom.SayAsync` and
`FloorItemsAt`. `IRoomFurniWriter` (`Turbo.Database.Furniture`). `IPluginFiles` (the plugin's own
folder, read only). `WiredVariableId.OfBox` (moved from `WiredVariableIdBuilder.CreateFromBoxId`,
the same ids). `WiredBox.UsesTextPlaceholders`. `IWiredEffectContext.ShowMessageAsync` is now
`SayAsync(user, text, RoomSpeech, ct)`.

**Breaks plugins**: `ShowMessageAsync` is gone, and `RoomUser`/`RoomFurni` take more
constructor arguments.

**Action**: `docs/upgrading.md`, steps 7 and 8.
