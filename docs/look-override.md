# Temporary look (look override)

A plugin can show a player with a different look than the one saved for them, for a uniform, a
costume or an event outfit, without rewriting the saved figure.

```csharp
var player = grainFactory.GetPlayerGrain(playerId);

// Whole look replaced.
await player.SetLookOverrideAsync("hd-180-1.ch-3030-110.lg-3006-110", null, LookOverrideMode.Replace, ct);

// Layered: only the sets named (ch, lg) are replaced; the player's own face, hair and shoes show.
await player.SetLookOverrideAsync("ch-3030-110.lg-3006-110", null, LookOverrideMode.MergeParts, ct);

await player.ClearLookOverrideAsync(ct); // false when none was set
```

`IPlayerGrain.GetLookOverrideAsync` reads it back. `PlayerLook.MergeParts` / `PlayerLook.Resolve`
(`Turbo.Primitives/Players/PlayerLook.cs`) are the pure functions that decide the result.

## Behaviour

- **Where it shows.** `GetSummaryAsync` carries the look the player is shown with, so room entry,
  live updates to the room (`UserChange`), the friend list and the player's own client
  (`FigureUpdateEventMessageComposer`) all use it. Surfaces that read the saved row directly
  (messenger history, guild member lists, leaderboards) show the saved figure.
- **Not saved.** The override lives in the player grain's memory only. Every database write of
  the profile writes the saved figure, never the shown one.
- **Merge.** `MergeParts` replaces saved parts by set type (the two letters before the first
  dash) in place, keeps the other saved parts, and appends override sets the saved figure lacks.
  The merge is computed when the look is shown, so a later change of the saved figure is still
  layered under the override. The override's `gender` (nullable) replaces the saved gender when
  given; null keeps it.
- **Validation.** The figure must pass `FigureString.IsWellFormed`, the mode must be defined, and
  the player must have an active session. Otherwise the call returns `false`, logs a warning and
  changes nothing.
- **Lifetime.** It survives room changes and ends on `ClearLookOverrideAsync` or when the player
  disconnects (the room and friends are told the saved look is back). While one is set the grain
  is held from collection for up to `Turbo:Players:LookOverrideKeepAliveMinutes` (default 1440).
- **The player changes their own figure meanwhile** (figure editor, wardrobe, clothing booth): the
  saved figure changes and is what returns when the override is cleared, but the override stays
  shown and the player's client is re-sent the look they are shown, not the one just saved. This
  is not blockable; a plugin that wants to refuse figure changes should do so on its own terms.
- **Event.** `PlayerLookOverrideChangedEvent` (`Previous`, `Current`, and the resulting `Figure` /
  `Gender`) is published on the global `EventSystem` on every real change, including the clear on
  disconnect. It is not awaited, so a handler may call back into the player grain.
