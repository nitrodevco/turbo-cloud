# Benchmark cases (v1) — for review

10 real historical Turbo Cloud fixes. For each: the agent gets only the issue text
below, in a copy of the repository as it was before the fix (no history, no later code), with the
instruction files under test overlaid. Hidden tests were verified to fail on that base and pass
on the historical fix.

**Please check:** are these the kinds of changes you want the agent to get right? Is any issue
text unfair (missing a fact a developer would need) or too leading (giving the fix away)?

| case | domain | difficulty | source | hidden tests | validation (tests passing) |
|---|---|---|---|---|---|
| `db-limited-query-ordering` | database/queries | easy | PR #22 (9badc99) | 3 | 0/3 at base → 3/3 with fix |
| `players-name-lookup` | players/directory | easy | commit (e68ed4d) | 4 | 2/4 at base (both behaviour tests fail) → 4/4 with fix |
| `reception-timing` | protocol/competition | medium | PR #22 (d02bd82) | 8 | 0/8 at base → 8/8 with fix |
| `rooms-model-heights` | rooms/model | medium | PR #26 (98b62b3) | 4 | 0/4 at base → 4/4 with fix |
| `rooms-reentry-access` | rooms/entry | medium | commit (6feb38e) | 7 | 4/7 at base (all 3 re-entry tests fail) → 7/7 with fix |
| `rooms-rotate-restack` | rooms/map | medium | PR #30 (21f4d60) | 7 | 5/7 at base (both restack tests fail) → 7/7 with fix |
| `rooms-stale-furni-click` | rooms/actions | easy | PR #24 (6f3e7fc) | 3 | 1/3 at base → 3/3 with fix |
| `rooms-water-joining` | rooms/furniture-logic | hard | PR #25 (8650dcf) | 10 | 4/13 at base (only guard tests pass) → 13/13 with fix |
| `session-duplicate-login` | networking/protocol | medium | PR #24 (6f3e7fc) | 5 | 2/5 at base (all 3 behaviour tests fail) → 5/5 with fix |
| `session-late-disconnect` | networking/presence | hard | PR #24 (6f3e7fc) | 4 | 2/4 at base (both behaviour tests fail) → 4/4 with fix |

**Excluded on purpose:** fixes whose lesson is already written in today's AGENTS.md (it would
hand the agent the answer: e.g. `HiddenByBc` in navigator snapshots, the chat-bubble serializer,
`SetHeight`/`SetPositionZ`, the placement-cap/`EnsureLogic` bugs), fixes that only exist
together with the feature they fix, and changes with no observable behaviour to test (tick
cadence, ConfigureAwait/await-using clean-ups, typed-exception refactor).

## `db-limited-query-ordering` — Limited navigator and messenger queries are unordered

- **Source:** PR #22, fix 9badc99
- **Base the agent starts from:** d02bd82
- **Why it is hard:** Easy/medium: find every limited query involved; the navigator one only shows up on a relational provider.
- **Hidden tests (3):** `FriendSearch_ReturnsFirstMatchesInNameOrder`, `FriendSearch_SameNamePrefix_TiesAreStable`, `NavigatorRoomsById_DoNotRelyOnUnorderedLimit`
- **Validation:** 0/3 at base → 3/3 with fix

Issue text given to the agent:

`````markdown
# Limited database queries without an order give arbitrary results and EF Core warnings

EF Core logs "The query uses a row limiting operator ('Skip'/'Take') without an 'OrderBy'
operator" for some of our queries, and the results of those queries are not deterministic:

- The messenger's friend search (search by name prefix, capped by the configured search
  limit) returns an arbitrary subset of matching players in an arbitrary order when more
  players match than the limit allows. Players expect alphabetical results: ordered by name,
  with ties broken by player id.
- The navigator's room lookup by id is a limited query with no order either, which triggers
  the same warning on every lookup of uncached rooms (the rooms it returns must still come
  back in the order they were asked for).

Make every limited query involved here deterministic and warning-free.
`````

## `players-name-lookup` — Name lookup for a single unknown player answers an empty name

- **Source:** commit, fix e68ed4d (only Turbo.Players/Grains/PlayerDirectoryGrain.cs)
- **Base the agent starts from:** 69c0cfc
- **Why it is hard:** Easy: a special case for a single id disagrees with the batch path.
- **Hidden tests (4):** `SingleUnknownId_IsLeftOut`, `SingleUnknownId_SameAnswerAsInABatch`, `SingleKnownId_IsAnswered`, `MixedBatch_AnswersOnlyKnownPlayers_Deduplicated`
- **Validation:** 2/4 at base (both behaviour tests fail) → 4/4 with fix

Issue text given to the agent:

`````markdown
# Looking up the name of one deleted player returns an empty name instead of nothing

The player directory's batch name lookup (player ids in, id → name out) is used for room
rights lists, ban lists, and furni/pet/bot owner names. For ids that don't belong to any player
(for example a deleted account), a lookup of several ids simply leaves them out, and callers
rely on that to skip them. But when the lookup is asked for exactly **one** id, an unknown
player comes back with an empty name. So a room whose only banned player or only rights
holder was deleted shows a blank entry, while rooms with two or more such entries don't.

A lookup should give the same answer however many ids it is asked for: every existing player
with their name, unknown ids left out, duplicates answered once.
`````

## `reception-timing` — Reception timing-code and countdown requests get no reply

- **Source:** PR #22, fix d02bd82 (only Turbo.PacketHandlers/Competition, Turbo.Primitives/Competition, Turbo.Primitives/Messages/Incoming/Competition, Turbo.Primitives/Messages/Outgoing/Competition, Turbo.Revisions/Revision20260909/Parsers/Competition, Turbo.Revisions/Revision20260909/Serializers/Competition)
- **Base the agent starts from:** 25e38ff
- **Why it is hard:** Stub handlers → parser, two composers, two serializers and a UTC schedule resolver across three projects, with protocol placement rules (Turbo.Revisions).
- **Hidden tests (8):** `TimingCode_IsLatestStartedEntry_AndEchoesSchedule`, `TimingCode_OrderOfEntriesDoesNotMatter`, `TimingCode_UsesUtcNowAgainstMinutePrecisionTimes`, `TimingCode_NothingStartedYet_IsEmpty`, `TimingCode_MalformedEntriesAreSkipped`, `SecondsUntil_FutureTime_CountsDownInUtc`, `SecondsUntil_PastTime_IsZero`, `SecondsUntil_Unparseable_IsZero_AndEchoed`
- **Validation:** 0/8 at base → 8/8 with fix

Issue text given to the agent:

`````markdown
# The reception's timed content never updates (timing-code and countdown requests get no reply)

The hotel reception view asks the server two timing questions, and Turbo answers neither
(the handlers are stubs), so scheduled reception content and countdowns never show.

1. **Current timing code** (`GetCurrentTimingCode`). The client sends one string: a schedule
   of `;`-separated entries, each `yyyy-MM-dd HH:mm,<code>` (seconds may also appear:
   `yyyy-MM-dd HH:mm:ss`). The reply (`CurrentTimingCode`) must echo the exact schedule string
   the client sent (the client matches replies by it), followed by the code of the entry with
   the latest start time that is not in the future, or an empty string when none has started.
   Malformed entries are ignored.

2. **Seconds until** (`GetSecondsUntil`). The client sends one string, a time in the same
   `yyyy-MM-dd HH:mm[:ss]` format. The reply (`SecondsUntil`) must echo that exact string,
   followed by an int: the whole number of seconds from now until that time, or 0 if it has
   passed or cannot be parsed.

All times in these strings are UTC, resolved against the server's current UTC time. The wire
order is: string, then string (timing code) / string, then int (seconds until). The incoming
seconds-until message currently does not even read its string.
`````

## `rooms-model-heights` — Room heightmaps load as hundredths; rooms default to fixed walls

- **Source:** PR #26, fix 98b62b3
- **Base the agent starts from:** 4d5f6d1
- **Why it is hard:** Three independent symptoms; the agent must find the fixed-point unit mix-up (Altitude.FromInt vs FromValue), reverse the heightmap encoder for the camera, and know -1 means automatic walls.
- **Hidden tests (4):** `DigitTiles_AreWholeTileHeights`, `LetterTiles_ContinueAfterNine`, `VoidTiles_StayUnusable`, `RoomWithoutExplicitWallHeight_GetsAutomaticWalls`
- **Judge-only requirement:** The camera's initial Z (door altitude) is decoded from the packed heightmap value: mask 0x3FFF, divided by 256; negative/blocked values give 0.
- **Validation:** 0/4 at base → 4/4 with fix

Issue text given to the agent:

`````markdown
# Room heights are wrong: floors load almost flat, the camera starts at the wrong height, walls are fixed

Three related problems with room heights:

1. **Heightmap tiles.** Room models are stored as heightmap text: `x` is a void tile, digits
   `0`-`9` are tile heights 0-9, and letters continue the scale (`a` = 10, `b` = 11, ... `z` =
   35). Turbo loads these far too small: a tile marked `1` ends up at 0.01 instead of 1.0, so
   raised floors and stairs are effectively flat and avatars/furni sit at the wrong height.

2. **Initial camera height.** When a player enters a room, the camera's starting Z is taken
   from the door tile's value in the packed heightmap sent to the client. That value is the
   heightmap packet's own encoding of a tile (a fixed-point height plus flag bits), not a
   plain height, but Turbo uses the packed number as if it were one, so the camera starts at
   a nonsense altitude. It should be decoded the way it was encoded; blocked (negative)
   values should give 0.

3. **Walls.** Rooms that never set an explicit wall height should get automatic walls. The
   client's floor-heightmap packet uses `-1` for "automatic walls above the highest floor";
   `0` means a fixed wall height of zero. Turbo's hotel default currently produces fixed
   walls for every such room.
`````

## `rooms-reentry-access` — Reloading the room you are standing in ejects you when it is full or locked

- **Source:** commit, fix 6feb38e (only Turbo.Rooms/Grains/Modules/RoomEntryModule.cs)
- **Base the agent starts from:** 6feb38e + Turbo.Rooms/Grains/Modules/RoomEntryModule.cs from bbc14d0 — The true parent bbc14d0 does not compile (a stale using fixed in the same commit), so the base is 6feb38e with only RoomEntryModule.cs restored from its parent.
- **Why it is hard:** Older base (Sep 17); must identify 'already inside' from room state and keep bans in force.
- **Hidden tests (7):** `FullRoom_PlayerAlreadyInside_IsAllowed`, `LockedRoom_PlayerAlreadyInside_IsNotSentToDoorbell`, `PasswordRoom_PlayerAlreadyInside_NeedsNoPassword`, `BannedWhileInside_IsStillBanned`, `FullRoom_NewPlayer_IsFull`, `LockedRoom_NewPlayer_RingsDoorbell`, `Owner_IsAllowedIntoFullLockedRoom`
- **Validation:** 4/7 at base (all 3 re-entry tests fail) → 7/7 with fix

Issue text given to the agent:

`````markdown
# Reloading the room you are already standing in can kick you out

A player whose avatar is already in a room can re-request entry to that same room (for
example by reloading it from the navigator). Turbo runs the full entry checks again as if
they were arriving:

- in a full room they are refused as "room full", although they already hold one of its
  slots, and
- in a locked (doorbell) or password room they are sent back to the doorbell / password
  prompt, although they are already past the door.

Either way they end up ejected from a room they legitimately occupy. A player who is already
inside should be let back in without counting against capacity or the door. Bans must still
apply (a ban placed while they are inside takes effect), and players who are not inside keep
getting today's checks.
`````

## `rooms-rotate-restack` — Rotating the bottom item of a stack should restack it on top

- **Source:** PR #30, fix 21f4d60
- **Base the agent starts from:** da26cb3
- **Why it is hard:** The height must be measured after the item leaves its own tiles; the fix sits on the path wired moves share, and naive fixes break no-op moves or explicit heights.
- **Hidden tests (7):** `RotatingBottomItemInPlace_LandsOnTopOfTheStack`, `RotatingAgain_KeepsClimbing`, `RotatingLoneItemInPlace_KeepsFloorHeight`, `RotatingTopItemInPlace_StaysOnWhatIsUnderIt`, `NoChange_KeepsHeight`, `ExplicitHeight_IsRespectedWhenRotatingInPlace`, `MovingToAnotherTile_LandsOnThatTile`
- **Validation:** 5/7 at base (both restack tests fail) → 7/7 with fix

Issue text given to the agent:

`````markdown
# Rotating the bottom item of a stack leaves it underneath

In the hotel, rotating an item that sits at the bottom of a stack moves it to the top of the
stack, so players can keep "rotating items upward" to rearrange a pile. In Turbo the rotated
item keeps its old height and stays underneath the items that were on it.

The Flash client only sends x, y and the new direction for a rotate; it takes the item's height
from the server's update, so the height has to be decided on the server.

Expected:
- An item turned in place (same tile, new direction) with no explicit height lands on top of
  whatever else stands on its tiles, as in the hotel. Rotating it again keeps it climbing.
- An item rotated in place with nothing on top of it stays where it is.
- Normal moves to another tile behave as they do today, an explicit height is still respected,
  and a "move" that changes nothing keeps the item's height.
- Wired effects that move/rotate furni without an explicit height go through the same rules.
`````

## `rooms-stale-furni-click` — Clicks on absent furniture are logged as errors

- **Source:** PR #24, fix 6f3e7fc (only Turbo.Rooms/Grains/Modules/RoomActionModule.cs)
- **Base the agent starts from:** 25e38ff
- **Why it is hard:** Easy calibration case: one guard, but it must stay a no-op without hiding real failures.
- **Hidden tests (3):** `ClickOnAbsentItem_IsNoOp_AndNotLoggedAsError`, `ClickOnPickedUpItem_IsNoOp_AndNotLoggedAsError`, `ClickOnLiveItem_StillReachesIt`
- **Validation:** 1/3 at base → 3/3 with fix

Issue text given to the agent:

`````markdown
# Clicking furniture that is not (or no longer) in the room is logged as an error

The server logs an error with a `FloorItemNotFound` exception and stack trace whenever a
player clicks a furni the room does not hold. This happens constantly in normal play:

- the client lets a player click its placement preview before the place packet has arrived,
  and
- a player can click an item that another player has just picked up.

Neither is a server fault. Such a click should simply do nothing (and not be reported as an
error), while clicks on items that are in the room must keep working exactly as before.
`````

## `rooms-water-joining` — Adjacent water furni never join into one pool

- **Source:** PR #25, fix 8650dcf
- **Base the agent starts from:** 25e38ff
- **Why it is hard:** A new room system: logic assignment for four definitions that use default_floor, a client bit layout, neighbour recomputation on place/move/pickup, room-active state that must not persist, and cached snapshots for new entrants.
- **Hidden tests (10):** `SameWaterSideBySide_JoinsBothWays`, `SurroundedWater_HasEveryRingBit`, `VerticalNeighbours_UseSouthAndNorthBits`, `DifferentWaterTypes_KeepTheirBorders`, `DifferentHeights_DoNotJoin`, `MovingAway_ClearsTheOldNeighbour_AndMovingBackRejoins`, `PickingUpANeighbour_ClearsTheShore`, `NewEntrantSnapshot_CarriesTheCurrentShore`, `OtherFurniNextToWater_DoesNotJoin`, `LargerFootprint_UsesItsWholeRing`
- **Judge-only requirement:** Picking a water item up restores its original state; derived shore state is room-active (not persisted, does not dirty the item for the database).
- **Validation:** 4/13 at base (only guard tests pass) → 13/13 with fix

Issue text given to the agent:

`````markdown
# Adjacent water furni never join into one pool

The water furni `bw_water_1`, `bw_water_2`, `val13_water` and `stackable_water` are meant to
join seamlessly when placed next to each other: the client draws a shoreline on every edge
of a water tile *unless* the server tells it that a matching water tile is on that side.
Turbo never tells it, so every water tile always shows a full border and pools look like a
grid of separate puddles. (In the database these four definitions use the plain
`default_floor` logic.)

**What the client reads.** For a water item, the item's state (its legacy stuff-data
integer) is a neighbour mask: one bit per cell of the ring of tiles surrounding the item's
footprint. Using coordinates relative to the footprint's origin tile, with the footprint's
width `w` and length `l` *after rotation*, the bits are numbered in this order:

1. the row below the footprint (`y = l`), from `x = w` down to `x = -1`;
2. then each row of the footprint from `y = l - 1` down to `y = 0`: first the cell to the right
   (`x = w`), then the cell to the left (`x = -1`);
3. then the row above the footprint (`y = -1`), from `x = w` down to `x = -1`.

So a 1x1 item has 8 bits (bit 3 = the tile to its east, bit 4 = west, bit 1 = south,
bit 6 = north), and a 2x2 item has 12. A bit is set when that ring cell holds a water item of
the **same furniture definition at the same height**. Different water types keep their
border artwork between them (that is what draws the shallow/deep transition).

Expected:
- The masks are correct whenever water is placed, moved (both the old and the new
  neighbourhood), re-stacked to another height or picked up, for the item itself and for its
  neighbours.
- Players entering the room later receive the current masks with the room's items.
- The mask is live room state: it must not overwrite the item's stored data or cause
  database writes, and an item that is picked up goes back to its original state.
- Other furni and the behaviour of these items otherwise (walking, stacking) are unchanged.
`````

## `session-duplicate-login` — A second login for the same player leaves the first connection half-alive

- **Source:** PR #24, fix 6f3e7fc
- **Base the agent starts from:** 25e38ff
- **Why it is hard:** Needs routing invalidation, a concurrent-login notice on the old socket (not via presence, which now routes to the new one), a serializer payload, and closing — without touching the new session.
- **Hidden tests (5):** `OldConnection_IsToldConcurrentLogin`, `OldConnection_IsClosed`, `OldConnection_NoLongerActsAsThePlayer`, `NewConnection_IsNotToldToLeave`, `FirstLogin_KicksNobody`
- **Validation:** 2/5 at base (all 3 behaviour tests fail) → 5/5 with fix

Issue text given to the agent:

`````markdown
# Logging in twice leaves the first connection half-alive

If the same account logs in a second time (another tab, another machine), the second login
takes over the player's presence, but the first connection is left open and is still
treated as that player: packets it sends are still handled as coming from the player, and
the first client is never told what happened.

Expected, as in the hotel:
- The old connection is told why it is being disconnected with the client's
  `DisconnectReason` message, reason **2** ("concurrent login"). The client reads the reason
  as a single int; Turbo currently sends that message with no payload at all.
- The old connection is then closed.
- From the moment it is replaced, the old connection no longer acts as the player.
- The new connection is unaffected and keeps receiving everything for the player.
- A first login, or the same connection authenticating again, kicks nobody.
`````

## `session-late-disconnect` — A reconnecting player's old socket closing late logs the new connection out

- **Source:** PR #24, fix 6f3e7fc
- **Base the agent starts from:** 25e38ff
- **Why it is hard:** A race across the session gateway and the presence grain; the obvious gateway-only fix is incomplete because the grain's unregister has no idea which connection is asking.
- **Hidden tests (4):** `LateDisconnectOfOldConnection_KeepsReplacementRegistered`, `AfterLateDisconnect_ComposersStillReachReplacement`, `DisconnectOfOnlyConnection_Unregisters`, `ReplacementsOwnDisconnect_StillUnregisters`
- **Validation:** 2/4 at base (both behaviour tests fail) → 4/4 with fix

Issue text given to the agent:

`````markdown
# Reconnecting players get logged out when their old connection finally closes

When a player reconnects (for example after a network blip or reloading the client), the new
connection logs in and becomes the player's session. The old socket often only closes a
little later. When that late disconnect is processed, Turbo tears down the player's presence
routing, so the *new* connection stops receiving anything: the player looks online but is
frozen, and messages, room updates etc. go nowhere.

A disconnect must only affect the connection it belongs to. Once a replacement connection
is registered for the player, the old connection closing late must leave the replacement
fully working. A player whose only (or current) connection disconnects must still be
cleaned up as today.
`````
