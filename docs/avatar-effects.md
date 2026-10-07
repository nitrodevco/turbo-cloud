# Avatar effects

A player owns avatar effects: copies of an effect wait in their inventory, they **activate** one
to start its timer, **wear** an effect they have running, and the running copy **expires** on its
own. This page is how the server keeps that, what a hotel sets up, and what is deliberately not
here. The client's side of it is the inventory effects tab, the Me menu's effects widget and the
avatar editor's effects tab.

## What it does

- `PlayerEffectGrain` (`Turbo.Inventory/Grains/Effects`) holds a player's effects, keyed by player
  id. It is **write-through**: a row is written first and memory follows, so nothing is flushed on
  deactivation.
- A running copy ends at an absolute time, `player_effects.expires_at`. It keeps counting while
  the player is offline and across restarts. One timer, set to the earliest expiry, tells the
  player when a copy ends; it is not what makes the effect expire. Every call looks for ended
  copies first, a list shows an ended copy as gone, and a grain that loads after a long idle spell
  drops whatever ran out meanwhile (quietly, since nobody was shown it running).
- What the **avatar wears is the room's**, not the grain's. Wearing is told to the presence, which
  tells the room, together with the ids of every effect the player owns. The room puts the effect
  on only where the avatar is bare or wears one of those. Riding (77), a game team and a freeze put
  effects on avatars too, and none of them remembers what was worn, so an inventory effect never
  overwrites one. Nothing about what is worn is kept in the grain, so it cannot go stale when the
  grain deactivates.
- When one effect expires, only **that** id is taken off, never every effect the player owns.

## Data

`player_effects` (migration `AddPlayerEffects`), one row per player and effect id:

| Column | Meaning |
|---|---|
| `player_id` | FK `players`, cascade delete |
| `effect_id` | the client's effect type |
| `sub_type` | 0 effect, 1 costume (see `CostumeEffectIds`) |
| `inactive_count` | copies waiting to be activated; the running copy is not counted |
| `is_permanent` | the effect for good; such a row holds no copies and no timer |
| `expires_at` | UTC; when the running copy ends, null when none runs |

Unique on `(player_id, effect_id)`. A row exists only while the player has a waiting copy, a running
one or the effect for good; the last copy of a timed effect ending deletes it.

## Rules

- **Reserved ids.** The room cannot tell where a worn effect came from, so an owned effect that
  shares an id with one the hotel applies (a rider's 77, a game team's) would be taken off the
  avatar by the player's own unwear, or by its expiry. The client also reads some ids to decide
  what an avatar is doing (29, 30 and 185 are swimming, 77 is riding), so an owned one would change
  a player's menu. Such an id is refused as invalid; that is also why the hotel's freeze ids belong
  in `ReservedEffectIds`. These effects never appear in a player's list, so the client's `fx_<id>`
  names are only needed for the effects the hotel sells.
- **Give**: copies are added to the row. A permanent grant replaces the copies and any running
  one; a timed grant on a permanent effect is refused (`AlreadyPermanent`). Caps: `MaxCopiesPerType`
  and `MaxDistinctEffects`; an id outside `1..MaxEffectId` is invalid.
- **Activate** uses one waiting copy, starts the timer and wears the effect. The client takes
  activating to mean wearing and sends select straight after, so either order converges. An effect
  already running, or permanent, is only worn: the client activates again on every room it enters.
- **Select** wears an effect that is running or permanent; one that only has waiting copies is
  refused. `0` or less (the official client sends `-1`, Nitro `0`) takes the worn effect off.
- Two effects may run at once (the client allows it); one is worn.
- A stack of N: one runs, N-1 wait. When it ends the others stay waiting.

## The avatar editor's effects tab ("Change your looks")

The editor's tab reads the same list and sends the same two messages, so nothing separate is
needed here. Saving the look sends **activate then select** for an effect that is not running (which
uses up one waiting copy), or **select -1** when none is chosen (which takes the worn effect off,
as it does in Habbo). The tab refreshes from `Added`, `Activated`, `Expired` and `Selected` and from
the room broadcast for the player's own avatar, all of which are sent. It appears only where the
client config `effects.in.avatar.editor` is true. Nitro's tab is not built yet (see below).

## Setting it up

Hotel settings, section `Turbo:Effects` (`EffectConfig`):

| Key | Default | |
|---|---|---|
| `DefaultDurationSeconds` | `3600` | how long one use lasts |
| `DurationOverrides` | none | seconds by effect id, for effects that differ |
| `CostumeEffectIds` | none | effects stored as sub type 1; the client opens the avatar editor's effects tab instead of the costumes catalog page for a player who owns one |
| `ReservedEffectIds` | the ids the client special-cases | ids that cannot be given: water `28 29 30 184 185` (the splash and the swim menu), game teams `33`-`36` `38` `39`, a rider `77`, snow war `95 96 98`, a snowboard `97` and a freeze `218`. Entries you list are **added** to these, never instead of them. Add your freeze ids (`Turbo:Wired:FreezeEffectIds`) and any changed team ids (`Turbo:Rooms:GameTeamEffectIds`) |
| `MaxEffectId` | `10000` | highest id a player can be given or ask for |
| `MaxCopiesPerType` | `99` | copies of one effect waiting |
| `MaxDistinctEffects` | `500` | different effects a player owns |

The length of a use is the **effect's**, not the grant's: the client shows one duration per effect
type, so two grants of one type with different lengths could not both be right.

A client names the quantity of a catalog purchase, so every sum against the caps is made in a wide
type and a count that would not fit is refused rather than wrapped. The purchase asks about its
**whole offer in one call** (`CheckGiveEffectsAsync`): copies of one effect are added up, and the
new effects it would add are counted together against `MaxDistinctEffects`, so an offer that would
give the first effect and then fail on the second is refused before anything is charged or given. A
failed message to the player (a closed session) never fails a grant that is already stored.

**Selling an effect:** a catalog product of type effect (`e`) with the **effect id as its extra
parameter** and the number of copies as its quantity. The editor checks the id; the client reads
the id where a sprite id goes (it draws `fx_icon_<id>`). A purchase that cannot be given is refused
**before** the buyer is charged: `AlreadyPermanent` is the client's own "you already own this
effect" (error 5), a cap is its full-inventory text.

Other things the hotel supplies, which are not this server's:

- an **effect map** and assets for every id it sells (an id with none is harmless but looks like
  nothing),
- the client's own texts (for example `catalog.alert.purchaseerror.description.5`, `fx_<id>` names
  and the `inventory.effects.*` keys),
- the client config `effects.reactivate.on.room.entry`, `effects.in.avatar.editor` and
  `avatareditor.effects.buy.button.catalog.page.name`, and `memenu.effects.widget.disabled` set to
  `false` (Nitro's shipped config sets it to `true`, which hides the Me menu's effects entry).

## Messages

| Direction | Message | Fields |
|---|---|---|
| client to server | `AvatarEffectActivated` (2036) | `int type` |
| client to server | `AvatarEffectSelected` (2995) | `int type` |
| server to client | `AvatarEffects` (1694) | list, entry: `type, subType, duration, inactive, secondsLeft, isPermanent` |
| server to client | `AvatarEffectAdded` (3908) | `type, subType, duration, isPermanent`, once per copy |
| server to client | `AvatarEffectActivated` (2071) | `type, duration, isPermanent` |
| server to client | `AvatarEffectExpired` (3701) | `type` |
| server to client | `AvatarEffect` (2282) | room broadcast: unit id, effect id, delay |

What the client depends on in the list: `secondsLeft` of **exactly `-1`** means not running and zero
is never sent for a running copy (it is at least one, because Nitro's test is "more than zero");
`duration` is a divisor and is never zero; a permanent effect is listed as running with a full bar.
The client never asks an effect to expire, so the server sends `Expired`. The room broadcast with
effect 0 takes the effect off the avatar; the client has no room handler for `Expired`.

## Not in this change

Found while reading the client for every use of effects, and left for their own changes:

- **Water and pool effects (28, 29, 30, 184, 185).** The client animates the splash and its swim
  menu from them, but nothing here applies them on entering or leaving water.
- **Wired:** the "give effect" action (code 52), and the freeze effect ids, which default to all
  zero (`WiredConfig.FreezeEffectIds`) while the client has a freeze effect (218) and a five-entry
  dropdown.
- **Furniture that gives effects:** the effect box, effect tiles and givers, and the snowboard (97).
  An effect box should end in `IPlayerEffectGrain.GiveEffectAsync`.
- **The room broadcast's delay** (always 0 here; the client supports one).
- **Nitro:** the avatar editor's effects tab has no data, nothing re-sends the worn effect on room
  entry (`effects.reactivate.on.room.entry`) and nothing takes it off on leaving, and the catalog
  preview of an effect product is not drawn.
- **No unseen ("new") marker** for effects; the client has none for them.
- **A refused wear is silent.** While the avatar wears something the hotel put on it (a rider, a
  game team, a freeze) the room will not put an inventory effect over it. The client cannot be
  told: the wardrobe's "worn" mark is its own (it sets it when the player clicks), the only
  message about the worn effect (`AvatarEffectSelected`) is read by the avatar editor alone, and
  Flash lives with the same limit by hiding the effects entry while riding. In a game or a freeze
  the window can therefore show an effect as worn that the avatar does not wear, until the player
  leaves the room, as in Flash.
