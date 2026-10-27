---
name: turbo-wired-boxes
description: Add wired boxes (effects, triggers, conditions, selectors) to a Turbo plugin with WiredEffect, WiredTrigger<TEvent>, WiredCondition, WiredSelector and [WiredBox("wf_...")], each opening an official editor, and fire the plugin's own wired events. Use when a plugin adds wired furni or lets its features start wired stacks.
---

# Wired boxes in a plugin

## When to use this skill

A new wired effect, trigger, condition or selector; a plugin event that should start wired stacks
("when a user earns credits"). Written against `Turbo.Contracts` alone.

## What the hotel guarantees

A plugin box is hosted by core's box of its kind: saves are checked like core's (params by the
rules you declare, text cut and stripped, picked furni in the room, capped), the box flashes, an
effect waits its delay, conditions get the editor's "not"/invert, selectors their filter/invert,
and every run counts against the room's wired limits. The client has no editor of yours: each box
names the **official editor** it opens and declares exactly the inputs that editor sends (a save
with other inputs is refused). The box's title and description are its furnidata's.

## An effect

<!-- compiled as the plugin "demo" -->

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Rooms;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Demo;

/// <summary>"Whisper to everyone in the room", in Show Message's editor (a text and three ints).</summary>
[WiredBox("wf_act_demo_whisper_room")]
public sealed class WhisperRoomEffect : WiredEffect
{
    public override WiredActionType Editor => WiredActionType.CHAT;

    // The editor's own ints, in its order: who sees it, the bubble style, the bubble width.
    public override IReadOnlyList<WiredParam> Params =>
        [WiredParam.Range(0, 1, 0), WiredParam.OneOf(202, 34, 200, 201, 202, 210), WiredParam.Range(-1, 2, -1)];

    public override int? TextMaxLength => 400;

    public override bool UsesTextPlaceholders => true;

    public override async Task<bool> ExecuteAsync(IWiredEffectContext ctx, CancellationToken ct)
    {
        if (ctx.Text.Length == 0)
            return false;

        var text = await ctx.FormatTextAsync(ctx.Text, ct);
        var speech = new RoomSpeech
        {
            OnlyForThem = true,
            StyleId = ctx.Param(1),
            BubbleWidth = ctx.Param(2) < 0 ? null : ctx.Param(2),
        };
        var whispered = false;

        foreach (var user in ctx.RoomUsers)
            whispered |= await ctx.SayAsync(user, text, speech, ct);

        return whispered; // whether it did anything
    }
}
```

## A trigger fired by the plugin

```csharp
using Turbo.Contracts.Wired;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Demo;

/// <summary>The event: a record of plain values (it travels to the room as JSON).</summary>
public sealed record CreditsEarned(int Amount) : IWiredEvent;

[WiredBox("wf_trg_demo_credits_earned")]
public sealed class CreditsEarnedTrigger : WiredTrigger<CreditsEarned>
{
    // Game Starts' editor has no inputs.
    public override WiredTriggerType Editor => WiredTriggerType.GAME_STARTS;

    public override bool Matches(CreditsEarned evt, IWiredBoxConfig box) => evt.Amount > 0;
}
```

Fire it where the plugin acts: `await hotel.Wired.FireForPlayerAsync(player, new CreditsEarned(50), ct)`
(the player's room, them as the triggering user) or `hotel.Wired.FireAsync(room, evt, triggerer, ct)`;
both answer false and load nothing when the room is empty. From furni logic in the same room use
`ctx.FireWiredAsync(evt, user, ct)` instead.

## A condition and a selector

```csharp
using System.Linq;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Demo;

[WiredBox("wf_cnd_demo_on_furni")]
public sealed class OnFurniCondition : WiredCondition
{
    public override WiredConditionType Editor => WiredConditionType.TRIGGERER_IS_ON_FURNI;

    public override bool ActsOnUsers => true;

    public override bool ActsOnFurni => true;

    // The positive rule only: the room applies "not" and invert. Synchronous.
    public override bool Evaluate(IWiredRunContext ctx) =>
        ctx.Users.Any(user => ctx.Furni.Any(furni => furni.X == user.X && furni.Y == user.Y));
}

[WiredBox("wf_slc_demo_lit_furni")]
public sealed class LitFurniSelector : WiredSelector
{
    public override WiredSelectorType Editor => WiredSelectorType.SELECTED_FURNIS;

    public override WiredSelection Select(IWiredRunContext ctx) =>
        WiredSelection.OfFurni(ctx.RoomFurni.Where(furni => furni.State > 0).Select(furni => furni.Id));
}
```

## Members

| Member | Means |
| --- | --- |
| `[WiredBox("wf_act_..." / "wf_trg_..." / "wf_cnd_..." / "wf_slc_...")]` | the logic key of the furniture definitions that are this box |
| `Editor` | the official editor (`WiredActionType`, `WiredTriggerType`, `WiredConditionType`, `WiredSelectorType`): pick one whose inputs are what the box needs |
| `Params` | the editor's ints, in order: `WiredParam.Range(min, max, default)`, `.Switch(default)`, `.OneOf(default, values...)` |
| `TextMaxLength`, `UsesTextPlaceholders` | keep the editor's text; list the stack's placeholders |
| `ActsOnUsers`, `ActsOnFurni`, `FurniLimit` | offer user/furni sources and picking; read them as `ctx.Users` / `ctx.Furni` |

The context (`IWiredRunContext`): `Text`, `Param(i)`, `PickedFurni`, `Users`, `Furni`, `RoomUsers`,
`RoomFurni`; an effect's adds `FormatTextAsync` and `SayAsync(user, text, speech, ct)`.

## Rules

- A box runs in the room's turn: never await the hotel API on this same room; another grain (a
  wallet) is fine. Add-ons (`WiredPolicy`) stay core's.
- Keys are unique hotel-wide: put the plugin's key in them (`wf_act_<key>_...`).
- Scaffold: `npm run make wired effect|trigger|condition|selector <name>`. Guide: `plugin-wired.md`.
- Test: fire through `FakeHotel` (`hotel.Wired.AssertFired<CreditsEarned>(room)`), see turbo-plugin-tests.
