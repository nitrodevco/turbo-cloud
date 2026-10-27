# Wired boxes and furni logic in a plugin

- [Introduction](#introduction)
- [Two boxes](#two-boxes)
    - ["Whisper to everyone in the room" (an effect)](#whisper-to-everyone-in-the-room-an-effect)
    - ["When a user earns credits" (a trigger, fired by the plugin)](#when-a-user-earns-credits-a-trigger-fired-by-the-plugin)
- [What each kind declares](#what-each-kind-declares)
- [Furni logic](#furni-logic)
    - [Linked pairs](#linked-pairs)
    - [Messages from your own code](#messages-from-your-own-code)
- [Scaffolding](#scaffolding)

## Introduction

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
using Turbo.Contracts.Rooms;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Rooms.Enums.Wired;

[WiredBox("wf_act_whisper_room")]
public sealed class WhisperRoomEffect : WiredEffect
{
    // Show Message's editor: a text, and three ints (who sees it, the bubble, its width).
    public override WiredActionType Editor => WiredActionType.CHAT;

    public override IReadOnlyList<WiredParam> Params =>
        [
            WiredParam.Range(0, 1, 0),
            WiredParam.OneOf(202, 34, 200, 201, 202, 210 /* ...the editor's styles */),
            WiredParam.Range(-1, 2, -1),
        ];

    public override int? TextMaxLength => 400;

    public override bool UsesTextPlaceholders => true;   // the editor lists the stack's placeholders

    public override async Task<bool> ExecuteAsync(IWiredEffectContext ctx, CancellationToken ct)
    {
        if (ctx.Text.Length == 0)
            return false;

        var text = await ctx.FormatTextAsync(ctx.Text, ct);   // the stack's placeholders
        var speech = new RoomSpeech
        {
            OnlyForThem = true,
            StyleId = ctx.Param(1),
            BubbleWidth = ctx.Param(2) < 0 ? null : ctx.Param(2),
        };
        var whispered = false;

        foreach (var user in ctx.RoomUsers)
            whispered |= await ctx.SayAsync(user, text, speech, ct);

        return whispered;
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
`RoomFurni`, read when asked). An effect's context adds `FormatTextAsync` and `SayAsync(user, text,
speech)` (a bubble over the user: the whole room's, or a whisper only that player sees, in the
`RoomSpeech` style and width). A box whose text takes placeholders says `UsesTextPlaceholders`, and
its editor lists them as Show Message's does.
It runs in the room's turn: never await the hotel API on this same room from a box (the room would
wait on itself); a player's wallet or inventory is another grain and is fine.

Add-ons are not in the kit: they change how a stack runs (`WiredPolicy`), which stays core's.

## Furni logic

> [!NOTE]
> Furni logic has its own guide, with every hook and the room turn explained:
> [Furni Logic & Room Turns](furni-logic.md). This section is its short form.

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

Hooks: `OnAttachedAsync` (placed, room loaded, plugin reloaded), `OnDetachedAsync` (picked up,
room unloading), `OnUseAsync`, `OnWalkOnAsync`, `OnWalkOffAsync`, `OnMessageAsync` (below). Each
runs after core's own (a walk-on still fires core's walk-on triggers). Two properties override the
definition: `UsagePolicy` (who may use it: `Everybody` for a machine every visitor plays) and
`Walkable` (true for a tile the logic acts on as it is stepped on); everything else (stacking,
sitting, rolling) is the furniture definition's. The context (`IFurniContext`) is the room in its
turn (`IRoomTurn`, below) and the furni itself (`Self`: its position, rotation, state and
definition, custom params included), with `SetStateAsync` and `FireWiredAsync` (from inside the
room; `IHotel.Wired` is for outside it), and:

| Member | What it does |
| --- | --- |
| `ReadExtraData<T>(section)` | one section of the furni's extra data as JSON (its own, else its definition's); null when none |
| `ReadExtraDataJson(section)` | the same section as its JSON text, whatever its shape (a number, a list, an object); null when none |
| `PairedItemId` | the other half of a linked pair (its `room_linker` section); 0 when none |
| `GetRoomOfItemAsync(itemId)` | the room an item stands in; null in an inventory |
| `ObjectExtra`, `SetObjectExtraAsync(n)` | the number the client gets with the furni; setting it shows the furni again. Kept while the room is loaded: set it in `OnAttachedAsync` if it must last |
| `WalkToAsync(user, tiles, onArrived)` | walks the user to the nearest tile it can reach and runs `onArrived` once it stands there, in the room's turn (core's walk-to-use machinery: at once when it stands still on the nearest; forgotten if the user walks elsewhere or takes too long). False when no tile is reachable |
| `WalkToInOrderAsync(user, tiles, onArrived)` | the same with the tiles tried in the order given, best first (in front of a machine, else beside it), not nearest first |
| `TileInFront`, `TilesBeside(user)`, `StandsBeside(user)` | the tile the furni faces; the tiles touching it the user may stand on, nearest to the user first; whether the user stands still on one |
| `Schedule(delay, run)`, `CancelSchedule()` | runs `run` once, `delay` later by the room's clock, in the room's turn (a spin's result, a rest's end, a game's next second); one per furni, a later call replaces it; never after the furni is picked up or its room unloads |
| `ShowStepAsync(user, tile)` | shows one step to a tile (the `mv` status, facing it) without moving the user; the tile may be off the map. Shown once |
| `ForwardToRoomAsync(user, room, extension)` | sends a player to another room with your own data (`RoomEntryExtension(key, payload)`) for that room's arrival policies (`IRoomArrivalPolicy`, `docs/plugin-policies.md`). A plain entry: bans, the door (doorbell, password), a full room and the entry policies all decide. A linked pair's half sending them to the room its other half stands in passes the door, as a teleporter does (see Linked pairs) |
| `WatchRoomAsync(user, room, envelope, lease)` | lets a player watch another room from here: its packets inside your envelope message, for your plugin (`docs/plugin-room-watch.md`). The door rule is the forward's: a linked pair's half watching the room its other half stands in passes it. Asked, not awaited |

What any code in the room's turn can do (`IRoomTurn`, which the furni context is):

| Member | What it does |
| --- | --- |
| `RoomUsers`, `Map` | everyone in the room (with where they stand and face, walking, idle, their effect); the floor: `IsFloor`, `IsFree` (no furni, nobody), `CanStand(user, tile)`, `FurniAt(tile)` lowest first |
| `SayAsync(user, text, speech)` | a bubble over a player or bot, as `RoomSpeech` says: shouted, a whisper only that player sees, its style and width. Filtered, not chat (no flood check, commands or chat log) |
| `SetEffectAsync(user, effect)` | puts an effect on the user; 0 takes it off |
| `StopWalkingAsync(user)`, `SetRotationAsync(user, rotation)` | stop the user where it stands; turn it, body and head |
| `SetBotFigureAsync(bot, figure)`, `SetBotFollowingAsync(bot, target)` | dress a bot; have it follow a user (null stops it) |
| `PlaceTemporaryFurniAsync(definition, tile, rotation)`, `RemoveTemporaryFurniAsync(id)` | a furni that is never saved and goes when removed or the room unloads, owned by the furni's owner; the definition may name another logic (`definition with { LogicName = ... }`) |

`RoomUser` stays a read-only view: every action is on the context, and each answers false when the
user has left the room.

### Linked pairs

`[FurniLogic("portal", LinkedPair = true)]` makes the furni a linked pair while the plugin is
loaded, exactly as a teleporter is: one bought from the catalogue (or granted by an offer) is two,
each half's `room_linker` section naming the other (`ctx.PairedItemId`), and it cannot be sent as a
gift. "WIRED Effect: Teleport to Room" also reads the pair, as it does a room linker's.

A player one half sends (`ctx.ForwardToRoomAsync`) to the room its other half stands in passes that
room's door (doorbell, password) as a teleporter's does: both halves were placed by someone with
rights in both rooms. Core checks the pair and the room itself; your logic cannot ask for it. Bans,
a full room and the entry policies still decide, and unlike a teleporter's arrival the room's
arrival policies still place the player. A forward anywhere else is a plain entry.

`docs/examples/room-window` uses all of it: a window pair that shows the room behind it and lets a
player walk through it onto the tile they click there.

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
