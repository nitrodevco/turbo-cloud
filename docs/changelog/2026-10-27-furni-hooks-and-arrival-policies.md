---
title: "Furni hooks, linked pairs and room arrival policies"
date: 2026-10-27
pr: turbo-cloud#460
breaks: yes
who: "plugins with furni logic; plugins that checked how a player arrived."
---

**What**: `IFurniContext` gains `ReadExtraData<T>`, `PairedItemId`, `GetRoomOfItemAsync`,
`ObjectExtra`/`SetObjectExtraAsync`, `WalkToAsync` with an arrival callback, `StopWalkingAsync`,
`SetRotationAsync`, `ShowStepAsync`, `ForwardToRoomAsync` (with an entry extension).
`[FurniLogic(key, LinkedPair = true)]` makes a plugin's pair furni, whose forward passes the other
half's room door as a teleporter does. `FurniLogic.OnMessageAsync` answers
`IHotel.Rooms.InvokeFurniLogicAsync`. `IRoomArrivalPolicy` (`make policy room-arrival`) picks where
an arriving avatar starts. `IsArrivingByTeleportAsync` is now `PassesRoomDoorAsync`.

**Breaks plugins**: Only for a plugin that called `IsArrivingByTeleportAsync`.

**Action**: rename the call; `docs/plugin-wired.md` ("Furni logic", "Linked pairs", "Messages from
your own code") and `docs/plugin-policies.md` for the rest.
