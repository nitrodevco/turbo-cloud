# Wired boxes and furni logic in a plugin

A plugin can add its own wired boxes (effects, triggers, conditions, selectors) and its own furni
logic, written against `Turbo.Contracts` alone: no `Turbo.Rooms`, no grain, no room module. The
server finds them as the plugin loads, gives the furniture whose logic is the box's key to them,
and gives it back to its old logic when the plugin unloads.

What the server promises:

- **Core's rules, unchanged.** A plugin box is hosted by core's own box of its kind, so a save is
  checked the same way (int params by their rules, text cut to its limit and stripped, picked furni
  in the room, de-duplicated and capped), the box flashes as core's do, an effect waits its delay
  and runs in the stack's order, a condition gets the editor's "not" and invert switches, a
  selector its filter and invert, and every run counts against the room's wired limits (visits per
  window, execution cost, recursion). Nothing a plugin box does can step around them.
- **An official editor.** The client has no editor of a plugin's own: each box names the official
  editor it opens (`Editor`), and declares the inputs that editor sends. A save with other inputs
  is refused, as for core's boxes. The editor's title and description are the furniture's own
  (its furnidata name and description).
- **Listed.** Each box is a `wired-box` contribution of the plugin (each furni logic a
  `furni-logic`), with an `override` beside it when the key was core's or another plugin's; the
  panel's Contributions tab shows them.

## Two boxes

The examples in `docs/examples/wired` are compiled, placed, saved and fired by
`PluginWiredKitTests`, against nothing but what a plugin project references.

### "Whisper to everyone in the room" (an effect)

```csharp
using Turbo.Contracts.Wired;
using Turbo.Primitives.Rooms.Enums.Wired;

[WiredBox("wf_act_whisper_room")]
public sealed class WhisperRoomEffect : WiredEffect
{
    // Show Message's editor: a text, and three ints (who sees it, the bubble, its width).
    public override WiredActionType Editor => WiredActionType.CHAT;

    public override IReadOnlyList<WiredParam> Params =>
        [WiredParam.Range(0, 1, 0), WiredParam.Range(0, 1000, 0), WiredParam.Range(-1, 2, -1)];

    public override int? TextMaxLength => 400;

    public override async Task<bool> ExecuteAsync(IWiredEffectContext ctx, CancellationToken ct)
    {
        if (ctx.Text.Length == 0)
            return false;

        var text = await ctx.FormatTextAsync(ctx.Text, ct);   // the stack's placeholders

        foreach (var user in ctx.RoomUsers)
            await ctx.ShowMessageAsync(user, text, onlyForThem: true, ct);

        return ctx.RoomUsers.Count > 0;
    }
}
```

### "When a user earns credits" (a trigger, fired by the plugin)

```csharp
public sealed record CreditsEarned(int Amount) : IWiredEvent;

[WiredBox("wf_trg_credits_earned")]
public sealed class CreditsEarnedTrigger : WiredTrigger<CreditsEarned>
{
    // Game Starts' editor has no inputs.
    public override WiredTriggerType Editor => WiredTriggerType.GAME_STARTS;

    public override bool Matches(CreditsEarned evt, IWiredBoxConfig box) => evt.Amount > 0;
}
```

Wherever the plugin pays credits, it fires the event for the player:

```csharp
if (await hotel.Wallet.CreditAsync(player, "credits", 50, reference, "jobs.pay", ct) == WalletOutcome.Done)
    await hotel.Wired.FireForPlayerAsync(player, new CreditsEarned(50), ct);
```

The event goes to the room the player is in, with them as the triggering user, and every
`CreditsEarnedTrigger` there runs its stack at the room's next wired tick. `FireAsync(room, evt,
triggerer)` fires in a room of the plugin's choosing. Both return false, and leave the room
unloaded, when no one is in it. A plugin's tests assert on `FakeHotel.Wired`
(`hotel.Wired.AssertFired<CreditsEarned>(room)`).

An event is a record of plain values: it travels to the room as JSON and is read back as the
trigger's type, so a plugin's event types never meet another plugin's, and a reload of the plugin
is picked up by the next event.

## What each kind declares

| Member | Kinds | Means |
| --- | --- | --- |
| `[WiredBox("wf_...")]` | all | the logic key of the furniture definitions that are this box |
| `Editor` | all | the official editor the client opens (`WiredActionType`, `WiredTriggerType`, `WiredConditionType`, `WiredSelectorType`) |
| `Params` | all | the int params, in the editor's order: `WiredParam.Range(min, max, default)`, `.Switch(default)`, `.OneOf(default, values...)` |
| `TextMaxLength` | all | the longest text a save keeps (never past the hotel's `Turbo:Wired:StringParamMaxLength`) |
| `ActsOnUsers`, `ActsOnFurni` | all | offer the user / furni sources (and furni picking) in the editor; the context's `Users` / `Furni` hold what they select |
| `FurniLimit` | all | fewer picks than the hotel's `Turbo:Wired:SelectedItemsLimit` |
| `ExecuteAsync(ctx, ct)` | effect | does it; returns whether it did anything |
| `Matches(evt, box)` | trigger | whether this event starts the stack (default: every one) |
| `Evaluate(ctx)` | condition | the positive rule only; synchronous |
| `Select(ctx)`, `SelectsUsers` | selector | `WiredSelection.OfFurni(ids)` / `.OfUsers(indexes)`; anything not in the room is dropped |

The context (`IWiredRunContext`) has the box as saved (`Text`, `Param(i)`, `PickedFurni`), the
room, the selected `Users` and `Furni`, and everyone and everything in the room (`RoomUsers`,
`RoomFurni`, read when asked). An effect's context adds `FormatTextAsync` and `ShowMessageAsync`.
It runs in the room's turn: never await the hotel API on this same room from a box (the room would
wait on itself); a player's wallet or inventory is another grain and is fine.

Add-ons are not in the kit: they change how a stack runs (`WiredPolicy`), which stays core's.

## Furni logic

```csharp
using Turbo.Contracts.Furni;
using Turbo.Contracts.Rooms;

public sealed record ButtonPressed : IWiredEvent;

[FurniLogic("jobs_button")]
public sealed class ButtonLogic : FurniLogic
{
    public override async Task<bool> OnUseAsync(IFurniContext ctx, RoomUser? user, int param, CancellationToken ct)
    {
        await ctx.SetStateAsync(1, ct);
        await ctx.FireWiredAsync(new ButtonPressed(), user, ct);   // this room's triggers hear it

        return true;   // handled; false lets the furni cycle its states as by default
    }
}
```

Hooks: `OnAttachedAsync` (placed, room loaded, plugin reloaded), `OnUseAsync`, `OnWalkOnAsync`,
`OnWalkOffAsync`, `OnMessageAsync` (below). Each runs after core's own (a walk-on still fires core's
walk-on triggers); everything else (walking, stacking, sitting, rolling) is the furniture
definition's. The context (`IFurniContext`) has the room, the furni itself, the room's users,
`SetStateAsync` and `FireWiredAsync` (from inside the room; `IHotel.Wired` is for outside it), and:

| Member | What it does |
| --- | --- |
| `ReadExtraData<T>(section)` | one section of the furni's extra data as JSON (its own, else its definition's); null when none |
| `PairedItemId` | the other half of a linked pair (its `room_linker` section); 0 when none |
| `GetRoomOfItemAsync(itemId)` | the room an item stands in; null in an inventory |
| `ObjectExtra`, `SetObjectExtraAsync(n)` | the number the client gets with the furni; setting it shows the furni again. Kept while the room is loaded: set it in `OnAttachedAsync` if it must last |
| `WalkToAsync(user, tiles, onArrived)` | walks the user to the nearest tile it can reach and runs `onArrived` once it stands there, in the room's turn (core's walk-to-use machinery: at once when it stands still on the nearest; forgotten if the user walks elsewhere or takes too long). False when no tile is reachable |
| `StopWalkingAsync(user)`, `SetRotationAsync(user, rotation)` | stop the user where it stands; turn it, body and head |
| `ShowStepAsync(user, tile)` | shows one step to a tile (the `mv` status, facing it) without moving the user; the tile may be off the map. Shown once |
| `ForwardToRoomAsync(user, room, extension)` | sends a player to another room with your own data (`RoomEntryExtension(key, payload)`) for that room's arrival policies (`IRoomArrivalPolicy`, `docs/plugin-policies.md`). A plain entry: bans, the door (doorbell, password), a full room and the entry policies all decide |

`RoomUser` stays a read-only view: every action is on the context, and each answers false when the
user has left the room.

### Linked pairs

`[FurniLogic("portal", LinkedPair = true)]` makes the furni a linked pair while the plugin is
loaded, exactly as a teleporter is: one bought from the catalogue (or granted by an offer) is two,
each half's `room_linker` section naming the other (`ctx.PairedItemId`), and it cannot be sent as a
gift. "WIRED Effect: Teleport to Room" also reads the pair, as it does a room linker's.

### Messages from your own code

A packet handler, command or task reaches its furni logic through the room:
`IHotel.Rooms.InvokeFurniLogicAsync(room, itemId, message)` (or `IRoomGrain.InvokeFurniLogicAsync`).
The message is `FurniLogicMessage(key, payload, sender)`: `key` is your logic's `[FurniLogic]` key,
`payload` a string you serialise (`FurniLogicMessage.Of(key, body, sender)` writes JSON,
`message.Read<T>()` reads it back), `sender` the player it is for. Strings only: your own types
never cross grains, since the plugin can be reloaded. The logic answers in the room's turn:

```csharp
public override async Task<FurniLogicReply> OnMessageAsync(
    IFurniContext ctx, FurniLogicMessage message, RoomUser? sender, CancellationToken ct)
{
    if (message.Read<SetTarget>() is not { } target)
        return FurniLogicReply.NotHandled;

    await ctx.SetObjectExtraAsync(target.RoomId, ct);

    return FurniLogicReply.Handled();   // or Handled(body) with an answer for the caller
}
```

The caller never gets an exception: the reply's `Status` is `Handled` (with its `Payload`),
`NotHandled`, `UnknownItem` (no such floor item in the room), `WrongLogic` (the item runs another
logic than `key`) or `Failed` (the logic threw; the room logs it).

## Scaffolding

```
npm run make wired effect whisper_room      # server/Wired/WhisperRoomEffect.cs, wf_act_whisper_room
npm run make wired trigger credits_earned   # the CreditsEarned event and CreditsEarnedTrigger
npm run make wired condition on_furni       # wf_cnd_on_furni
npm run make wired selector lit_furni       # wf_slc_lit_furni
npm run make logic skill_stove              # server/Logic/SkillStoveLogic.cs (FurniLogic)
```

Someone can place the box once a furniture definition has its key as its logic: staff pick it on
the admin panel's furniture page, whose logic list holds every key the server has registered,
plugins' included (`docs/gamedata.md`). Its furnidata gives it its sprite, name and description.
