# Permissions, groups and security levels

Implementation plan for a permission system. **Every phase of §14 is built**: the node
constants, registry and resolver (`Turbo.Primitives/Players/Permissions/`, tested in `Turbo.Tests`);
the tables and seeds (§13); the grains, audit, expiry and the `perm` console command (§9); the
projection that tells the client (§8); the packet-boundary gate (§11); the gates in rooms, the
navigator, chat, trading, groups and the catalog (§11, §16 and the "as built" sections before §14);
limits through meta (§6); and the check that every node has a reader (§14, phase 8). What is left is in §17.4 and the Nitro opt-in of §8.

The shape is borrowed from LuckPerms rather than from the Habbo retros, on purpose. The retro
pattern — one rank per player, a `permissions` table with a column per permission, code comparing
`rank >= 7` — is the thing this plan exists to avoid. What replaces it: a player holds any number
of weighted **groups** that inherit from each other, permissions are string **nodes** with
wildcards and negation, grants can **expire**, groups and players carry **meta** (limits), every
change is **audited**, and any check can **explain** itself.

Written for whoever implements it. Read `AGENTS.md` first — the grain rules and the constants
rules both bear on the shape below.

## 1. What is there now

Four channels already exist and three of them are inert.

| Channel | Where | State |
| --- | --- | --- |
| `SecurityLevelType` | `Turbo.Primitives/Players/Enums/` | Declared with nine members. **Nothing reads or writes it** except `PlayerSubscriptionGrain.SendStatusAsync`, which sends `None` flat in `UserRights`. |
| `PlayerPerkFlags` | `Turbo.Primitives/Players/Enums/` | Thirteen flags with full legacy-string mapping, loaded into `PlayerLiveState.Perks` from `players.perk_flags` — and then **never read**. `SSOTicketMessageHandler` builds the `PerkAllowances` list out of hardcoded literals instead. |
| `RoomControllerType` | `Turbo.Primitives/Rooms/Enums/` | Live and enforced. Room-scoped only: owner, rights, group. Not a hotel-wide permission, and stays that way. |
| `NavigatorFlatCategoryEntity.MinRank` / `StaffOnly` | `Turbo.Database/Entities/Navigator/` | Columns exist; `NavigatorService` filters `!x.StaffOnly && x.MinRank <= 1`, with the 1 hardcoded because no player has a rank. |

There is no group table, no permission table, and nothing on `players` for either.

> **Read the two client surveys first.** `docs/permissions-client-gates.md` lists every place the
> client asks about a security level or a room controller level;
> `docs/permissions-client-perks.md` does the same for the `PerkAllowances` channel. Findings from
> both shape the projection in §8.

## 2. What the client gates on, which is the contract we have to fit

Three separate things, and conflating them is the main trap.

**`hasSecurity(n)` is a threshold, not a role.** `SessionDataManager.hasSecurity(n)` is literally
`securityLevel >= n`, against the single int in `UserRights`. The levels the client asks for:

| Level | `SecurityLevelType` | What the client unlocks at it |
| --- | --- | --- |
| 2 | `Partner` | the `:furni` chooser |
| 4 | `Employee` | `:kick` / `:mute` sent to the server, the wired menu, saving a floor plan without Builders Club, staff options in room create, picking up anyone's furni on the info stand |
| 5 | `Moderator` | control of every room (`isAnyRoomController`), the moderation tool, the Builders Club catalog without a membership, deleting any guild, entering any room from the navigator |
| 7 | `Community` | staff-only navigator categories |

**`isPerkAllowed(code)` is a capability list**, sent as `PerkAllowances`. It merges live and fails
closed: a code the server never sends is denied.

**`roomControllerLevel` is room-scoped** and already works.

So the client needs one *number*, one *boolean* (`isAmbassador`) and one *list of codes*. None of
them is the server's authority — all three are a **projection** of the resolved permissions
(§8). Checking a security level server-side because the client does is how a permission system
rots.

## 3. Vocabulary

| Term | Meaning |
| --- | --- |
| **Node** | A dotted permission string, `room.enter.locked`. Stored with a value: `true` grants, `false` denies. |
| **Wildcard** | A node ending in `*`: `room.enter.*`, `room.*`, `*`. Only allowed in *assignments*, never in *checks*. |
| **Group** | A named set of nodes and meta with a **weight**. Groups inherit from parent groups. Replaces "rank". |
| **Meta** | A key/value on a group or player: `limit.rooms = 50`. For numbers a permission cannot express. |
| **Registry** | The set of nodes and meta keys the running server knows, registered by core and by plugins at startup. |
| **Resolved set** | For one player: which registered nodes they hold, and the value of each meta key. What every check reads. |

## 4. Nodes and the registry

### Strings in storage, constants in code

Nodes are stored as strings so assignments can use wildcards and plugins can define their own.
Code never writes a node as a literal: core nodes are constants in
`Turbo.Primitives/Players/Permissions/PermissionNodes.cs`, grouped by domain —

```csharp
public static class PermissionNodes
{
    public static class Room
    {
        public const string CONTROL_ANY = "room.control.any";
        public const string ENTER_LOCKED = "room.enter.locked";
        // ...
    }
}
```

— so a typo is a compile error, and "find references" on a constant finds every gate.

### Registration

Every node a check can ask about is **registered** with a short description and, if the client
draws something for it, the security level the client needs (§8):

```csharp
public sealed record PermissionNodeDefinition(
    string Node,
    string Description,
    SecurityLevelType? ClientLevel = null,
    PlayerPerkFlags? Perk = null);
```

Core registers its catalogue (§7) through `CorePermissionNodeSource`, beside the constants in
`Turbo.Primitives/Players/Permissions/`, with the registry and the resolver. A plugin registers its own from
`ITurboPlugin.ConfigureServices` through an `IPermissionNodeSource` in DI, and must prefix its
nodes with its plugin id (`casino.table.open`); the registry refuses an unprefixed plugin node or
a duplicate. Meta keys register the same way, with a type and the config option that supplies
their default (§6).

The registry is what makes wildcards cheap and mistakes visible:

- **Wildcards expand against it.** `room.*` means "every registered node under `room.`", resolved
  once per player, not matched on every check.
- **A check for an unregistered node throws** in development and logs and denies in production.
  A gate that asks for a node nobody registered is a bug, not a denial.
- **An assignment of an unregistered node is kept but flagged.** Its plugin may simply be
  unloaded; the check trace (§10) and the console list it as unregistered rather than dropping
  the row.

## 5. Groups, inheritance and resolution

### Groups

- A group has a `name` (the key, lowercase, stable), a `display_name`, and a **weight**. Higher
  weight wins conflicts.
- A group may have any number of **parents** and inherits their nodes and meta. Cycles are refused
  on write and detected again on load.
- A player holds any number of groups, each optionally **temporary**. Every player implicitly
  holds `default`, which cannot be removed and has the lowest weight.
- There is no primary-group column. Where something needs one group to show (a staff badge, a
  profile line), it is the player's highest-weight group.

### Resolution

For each registered node, the value comes from the **first source that has an opinion**:

1. **The player's own nodes.** Beat everything, which is what makes a one-player sanction
   (`trade = false` for seven days) possible without inventing a group.
2. **Each group the player holds, directly or by inheritance, highest weight first.** A group
   reached through several paths is counted once, at its own weight.
3. **Nothing matched: denied.**

Within one source, several assignments can match the same node. The most specific wins: the exact
node, then the longest wildcard (`room.enter.*` beats `room.*` beats `*`). At equal specificity a
**temporary assignment beats a permanent one** — a player may hold both, and a timed sanction
outranks what it suspends without erasing it — and then **`false` beats `true`**. Specificity
comes first, as in LuckPerms: a permanent exact node beats a temporary wildcard. Expired
assignments do not exist for resolution.

Meta resolves by the selection its key is registered with (`PermissionMetaSelectionType`):
`Inheritance` takes the first source in the same order — player, then groups by weight, a
temporary value before a permanent one — while `HighestNumber` and `LowestNumber` take the
largest or smallest number any live source sets, which is what a limit several groups raise wants.

Every group the player reaches, directly, by inheritance or as `default`, also grants its
**membership node** `group.<name>`, as LuckPerms' `group.<name>` does. It is not registered and
cannot be assigned: `group.` is reserved (the registry refuses a node under it, and a write of one
answers `ReservedNode`), no wildcard reaches it, and a player denial of it changes nothing. Holding
the group is the only way to hold it, so `required_node = 'group.vip'` (§16) means "is in vip".
`perm check <player> group.<name>` shows the inheritance path. A membership node a CMS wrote into a
node table anyway is reported as unregistered, like any other.

This is LuckPerms' order with contexts removed — see §12.

## 6. Meta

Meta is for the limits retros kept as per-rank columns. `AGENTS.md` already says a limit is a
config option on its module's config class; meta does not change that. **The config option stays
the hotel default, and meta overrides it per group or per player.** Built in phase 7.

| Meta key | Hotel default | Read by |
| --- | --- | --- |
| `limit.friends` | `PlayerConfig.MessengerNormalFriendLimit` | `PlayerMessengerGrain`: every friend check, and the player's own limit in `MessengerInit` (the normal and extended tiers beside it stay the hotel's) |
| `limit.rooms` | `NavigatorConfig.MaxRoomsPerPlayer` | `NavigatorService.CanCreateRoomAsync`, which room creation goes through |
| `limit.favourite_rooms` | `PlayerNavigatorConfig.MaxFavouriteRooms` | `PlayerNavigatorGrain.AddFavouriteRoomAsync`, and the limit sent at login |

The code that enforces a limit asks for it with `IGrainFactory.GetLimitAsync(player, key,
configDefault)`, passing its own config option, so the default still lives where `AGENTS.md` puts
it and the grain still reads its own config. `PermissionMeta.ReadLimit` turns the meta value into
the limit: a whole number of zero or more replaces the default, anything else is ignored. All three
keys are registered with `HighestNumber`, so a player in two groups that both raise a limit gets
the larger — and a group can lower one too, since any value it sets replaces the default.

The floor plan area stays a node (`room.floorplan.large`, §13), because the client's
`BUILDER_AT_WORK` perk it has to agree with is a yes or no. Display meta (name prefixes, colours)
waits for a client that draws it. `PermissionNodeReaderTests` fails for a registered meta key that
nothing reads, as it does for nodes.
## 7. The node catalogue

The first members are not invented — they are the comments already sitting in the code and the
client gates from §2. Each names the one place that reads it.

| Node | Gates | Client level | Today |
| --- | --- | --- | --- |
| `room.control.any` | `RoomSecurityModule.IsRoomOwner` and `GetControllerLevelAsync` (returns `Moderator = 5`) | 5 | `// if has perm any_room_owner true`, `// if has perm room_rights Rights`. **One node, not two**: the client's only channel is `securityLevel >= 5`. |
| `room.furni.steal` | `GetFurniPickupTypeAsync` → `SendToRequester` | — | `// if can steal furni` |
| `room.furni.pickup_any` | `GetFurniPickupTypeAsync`: pickup of another player's furni | 5 | the client offers it with `isAnyRoomController` (`InfoStandFurniView.updatePickupMode`), so 5, not the 4 an older reading gave |
| `room.enter.locked` | `RoomEntryModule.CheckAccessAsync` (`bypassDoor`) | — | server-only: no client gate asks. The level 5 it once carried came from `NavigatorData`, whose `hasSecurity(5)` is `canEditRoomSettings` |
| `room.enter.full` | same | — | staff turned away by capacity |
| `room.enter.hidden` | `RoomEntryAccessType.HiddenByBuildersClub` | — | staff refused with everyone else |
| `room.moderate.any` | `RoomModerationModule` kick/mute/ban, over `ModSettings`; `IRoomPlayer.IsModerator` | 4 | |
| `room.floorplan.save_without_club` | `RoomGrain.SaveFloorPlanAsync` | 4 | see `docs/builders-club.md` §7 |
| `room.floorplan.large` | `RoomMapModule` area limit | — (perk `BUILDER_AT_WORK`) | |
| `catalog.builders_club.without_membership` | Builders Club catalog access | 5 | |
| `navigator.category.staff` | `NavigatorService`, `EnforceCategoryCtrl` | 7 | the hardcoded `MinRank <= 1` |
| `moderation.tool` | the moderation packets | 5 | |
| `wired.menu` | `WiredMenuController` | 4 | |
| `chat.furni_chooser` | `:furni` | 2 | |
| `role.ambassador` | `UserRights.IsAmbassador`, ambassador mutes, `ChatStyles.CanSpeakWith` `isAmbassador` | — | hardcoded `false` |
| `chat.style.staff` | `ChatStyles.CanSpeakWith` `isStaff` (staff bubbles; also meets the ambassador flag) | — | |
| `trade` | trading, server-enforced | — (perk `TRADE`) | sent `true`, enforced nowhere |
| `perk.camera`, `perk.mouse_zoom`, `perk.citizen`, `perk.navigator.thumbnail_camera`, `perk.navigator.phase_two`, `perk.navigator.phase_one`, `perk.guide_tool`, `perk.judge_chat_reviews`, `perk.call_on_helpers`, `perk.vote_in_competitions`, `perk.habbo_club_offer_beta` | `PerkAllowances` only | — | the hardcoded block in `SSOTicketMessageHandler` |
| `permissions.manage` | any future in-game editor | — | the console needs no node |

`perk.*` nodes exist because the perk list has to come from somewhere; they gate nothing on the
server. `trade` and `room.floorplan.large` are real server gates that also project to a perk.

Three Builders Club gates (`room.floorplan.save_without_club`, `room.floorplan.large`,
`room.enter.hidden`) are recorded in `docs/builders-club.md` as blocked on staff ranks, and the
trial rule "nobody else in the room" counts staff as ordinary visitors. All four become one-line
changes once this exists.

## 8. The projection to the client

Built in phase 4: `PermissionProjection` (`Turbo.Primitives/Players/Permissions/`) turns a
resolved set into a `PermissionClientSnapshot`, and `PlayerPermissionGrain` sends it. The grain
owns `UserRights` now, reading the club level it carries from the subscription grain, and sends it
with `PerkAllowances` at login (`SSOTicketMessageHandler`, whose hardcoded perk list is gone) and
again whenever the projection changes: a write, a group push, an expiry, a registry change. The
subscription grain asks for a resend when the club changes, without awaiting it, since the
permission grain reads the club back from it. The perk refusal texts moved onto the node
definitions (`PerkRefusal`). `IsModerator` is projected but reaches the room avatar in phase 6.

### What a level leaks, and the Nitro opt-in

The level is a threshold, so a group given one node that needs 7 is also offered everything
that needs 7 or less. Every client understands only this, so it stays the default contract, and
it is never capped: a capped level would let the server allow something the client offers no
button for. Instead operators are shown the consequence. `PermissionProjection.ReportLevel`
names the node (or meta floor) that set the level and every registered node at or below it the
player does not hold, and `perm user <p> info` and `perm group <g> info` print it as "the client
will also offer, and the server refuse".

Finer control is opt-in, and built: the `permission.nodes` extension of
`docs/client-capabilities.md`. A client that asks after login is sent `TurboPermissionNodesMessage`
— every held node that is **client-visible** (it has a `ClientLevel`, or a plugin marked it
`ClientVisible`) — after `UserRights` and again whenever either changes. nitro-next gates on the
node when it has the list and on the level when it does not, so it still works against other
emulators, and a client that never asks never sees the packet.

One place, and only one — a `PermissionProjection` in `Turbo.Players` — turns a resolved set into
what the client is told:

- **`UserRights.SecurityLevel`** ← **derived**: the highest `ClientLevel` among the registered
  nodes the player holds, or the meta key `client.security_level` if that is higher (a floor, for
  the rare group that wants the client's staff UI without any server power). Never configured on
  its own, so a player's level and their permissions cannot contradict each other.
- **`UserRights.IsAmbassador`** ← `role.ambassador`.
- **`PerkAllowances`** ← each registered node with a `Perk`, sent with its resolved value. The
  refusal strings stay as they are; this client never reads them.
- **`IRoomPlayer.IsModerator`** ← `room.moderate.any`.

The trade-off of a derived level, written down so nobody rediscovers it: the level is a threshold,
so a player holding only `navigator.category.staff` (7) is also shown the moderation tool (5)
client-side. The server still refuses every moderation packet they send. **The client showing a
button the server refuses is the acceptable failure; the server allowing something the client
cannot reach is not.** A hotel avoids the first by granting staff nodes in coherent groups.

`players.perk_flags` and `PlayerLiveState.Perks` are retired: the data migration turns each
player's non-default flags into player nodes, and `UnmapPerkFlags` stops mapping the column (§13).

## 9. Where the authority lives

Two grains and a registry provider, built in phase 3 (`Turbo.Players/Grains/Permissions/`,
`Turbo.Players/Permissions/`).

**`IPermissionRegistryProvider`** — a singleton holding the live `PermissionRegistry`. Core's
source is always in it; `PermissionNodeFeatureProcessor` registers each public
`IPermissionNodeSource` a plugin assembly declares as the plugin loads, and the registration is
disposed when it unloads. Every change builds a whole new registry, so a clash fails the plugin's
load with nothing changed. Nothing is pushed on a registry change: a player grain compares the
registry it resolved against by reference and resolves again on its next read.

**`IPermissionGroupDirectoryGrain`** — one per hotel, `[KeepAlive]`, shaped like
`BadgeDirectoryGrain`. Holds every group — nodes, meta, parents, weight — loaded on activation and
answered from memory as an immutable, versioned `PermissionGroupDirectorySnapshot`. It is **the
only writer of group data**: create, delete, reweight, rename, set/unset node or meta, add/remove
parent. Each write is saved with its audit row (§10), the group is read back, and the new snapshot
is pushed to every **subscribed** player grain. A player permission grain subscribes on activation
and unsubscribes on deactivation, so the subscribers are exactly the active ones. The push goes
straight to the grain, not through the presence: `AGENTS.md` keeps the presence as a transport for
packets, and the directory needs no presence state to decide anything. Group edits are rare
operator actions, so every subscriber is told rather than working out who is affected; an older
push arriving after a newer one is dropped by version.

**`IPlayerPermissionGrain`** — one per player, shaped like `PlayerSubscriptionGrain`, write-through.
Holds the player's own nodes, meta and memberships, and the **resolved set** built from them, the
directory's snapshot and the registry. Answers from memory:

- `HasAsync(node)` — an unregistered node is logged and denied. `GetMetaAsync(key)`,
  `GetResolvedAsync()` — the resolved set, for the room and the projection.
- `ExplainAsync(node)` — the check trace (§10). `GetAssignmentsAsync()`, `GetAuditAsync(count)`.
- Writes for this player: add/remove group, set/unset node or meta, each with an optional expiry.
  The default group can be neither joined nor left.

Writes return a `PermissionChangeResultType` (`Changed`, `Unchanged`, `UnknownGroup`, `Invalid`,
`Expired`, `ProtectedGroup`, `WouldCycle`, `AlreadyExists`, `NotFound`, `ReservedNode`) and take the actor for the
audit — a player, or `null` for the console. Neither grain checks that the actor may make the
change; whoever calls it does (`permissions.manage`, once something in game calls it).

After any change to its resolved set it will re-project and send `UserRights` and
`PerkAllowances` itself (the client applies both live — no reconnect), and push the new snapshot
to the room the player is in. That is phases 4 and 6.

It also raises **`PlayerPermissionsChangedEvent`** (`Turbo.Primitives/Players/Events/`) on the
event system whenever a node or meta value the player holds changes, whatever caused it: a write
to them or a group they reach, an expiry, a reload, a plugin's nodes registering. It carries the
previous and current resolved sets, with `Gained` and `Lost` worked out. Activation raises
nothing, since it is not a change. The event is not awaited, so a plugin handler may call back
into the player's permission grain; LuckPerms' `NodeAddEvent`, `UserDataRecalculateEvent` and
`UserPromoteEvent` all come down to this one.

**Expiry** is a one-shot grain timer on each grain, set for the earliest expiry it knows of and
capped at `PermissionConfig.ExpiryCheckMaxMs`. The player grain's fires for its own rows and for
group rows that took part in its resolution; it deletes and audits its own expired rows and
resolves. The directory's deletes and audits expired group nodes and meta and publishes. Rows
that ran out while nobody was looking are ignored by resolution anyway, and swept the next time
the grain activates, because a timer set for a time already passed fires at once. A failed sweep
retries after `ExpiryRetryMs`.

### The console

`perm` on the server console (`Turbo.Main/Console/PermissionConsoleCommand.cs`) drives both grains
as the console:

```text
perm check <player> <node>
perm reload
perm search <node> [count]
perm log [count] | log search <text> [count]
perm user <player> info | audit [count] | reload | verbose on [prefix] | verbose off
perm user <player> group add <group> [duration] [--extend] | group remove|removetemp <group>
perm user <player> set <node> [true|false] [duration] [--extend] | unset|unsettemp <node>
perm user <player> meta set <key> <value> [duration] [--extend] | meta unset|unsettemp <key>
perm groups
perm group <group> info | audit [count] | members [count] | create [weight] [display name] | delete
perm group <group> weight <weight> | rename <display name>
perm group <group> set <node> [true|false] [duration] [--extend] | unset|unsettemp <node>
perm group <group> meta set <key> <value> [duration] [--extend] | meta unset|unsettemp <key>
perm group <group> parent add|remove <parent>
```

Durations are `30s`, `15m`, `12h`, `7d`, `2w`; left out, the assignment is permanent. A
temporary assignment is its own row beside any permanent one: `unsettemp` / `removetemp` remove
it, `unset` / `remove` the permanent one. Setting a temporary one again replaces its expiry;
`--extend` adds the new duration to what it has left (`PermissionExpiryModeType`), LuckPerms'
`temporary-add-behaviour` minus `deny`.

Both grains are write-through and answer from memory, so a row written straight into the tables
(a retro CMS, a housekeeping panel) is not seen until it is read again. `perm reload` is
LuckPerms' `sync`: the directory reads every group again and publishes, and every active player
permission grain reads its own rows again, resolves, and tells the client and room of any
difference. `perm user <player> reload` does one player. Neither is audited; they change nothing
in the tables.

Three lookups answer "who", which the per-player and per-group commands cannot: `perm group <g>
members` lists the players in a group directly (default has no rows, so it lists nobody);
`perm search <node>` lists every group and player given the node, exactly or by a wildcard that
covers it, granting or denying — who was given it, not who ends up holding it, which `perm check`
answers for one player; `perm log` is the audit of everyone, newest first, and `perm log search
<text>` narrows it to rows whose node, key or group name contains the text. All three are queries
on the directory grain, capped by `Permissions.LookupPageLimit` and `AuditPageLimit`.

## 10. Audit and the check trace

**`permission_audit`** records every write: when, who (a player id, or null for the console or
the system), the target (a player or a group), the action (node set/unset, meta set/unset, group
added/removed, parent added/removed, group created/deleted/reweighted, **expired**), the node or
key, the value, and the expiry. Written by the two grains in the same save as the change. Nothing
else writes it and nothing edits it.

**The check trace** answers "why can this person do that", which is what a permission system gets
asked most:

```text
> perm check Alice room.enter.locked
room.enter.locked = true
  decided by  group moderator (weight 50) : room.enter.* = true
  path        Alice -> staff -> moderator
  overrides   group default (weight 0)    : room.* = false
```

`ExplainAsync` returns the deciding assignment, its source, the inheritance path, its expiry, and
the lower-priority assignments it beat. The console prints it; a later in-game editor can show
the same record.

**Verbose** answers the question before that one — "which node does this feature want?" —
by watching the checks as they happen, as LuckPerms' `verbose` does:

```text
> perm user Alice verbose on room.
  Verbose: player 12 checked room.enter.locked in room 40: False
> perm user Alice verbose off
```

The filter is a node prefix, left out for every node. It lives on the player's permission grain
and is copied onto every resolved set it makes, so the room's copy carries it too: the grain logs
the checks and meta reads it answers, and `RoomSecurityModule.HasPermission(IRoomPlayer, node)` —
which every room check of a player's node goes through — logs those made from the avatar. It is
not something the player holds, so turning it on raises no `PlayerPermissionsChangedEvent`, and it
ends with the grain's activation.

## 11. How gating is enforced

The failure mode of a permission system is a gate nobody remembers to write. Gates live at three
depths.

1. **At the packet boundary** — "may this player send `ModerateRoom` at all". Built in phase 5.
   The handler carries `[RequiresPermission(PermissionNodes.Moderation.TOOL)]`, and the pipeline
   enforces it: as `MessageFeatureProcessor` registers a handler, it wraps the invoker of any
   handler with the attribute in `PermissionGate` (`Turbo.Messages/Registry/`). The wrapped
   handler is reached only by a signed-in player holding one of the declared nodes; anyone else's
   packet is dropped without a reply, after the behaviours and before the handler is called. The
   handler's body holds no check. The attribute takes several nodes when any one of them will do
   (`AmbassadorAlertMessageHandler`: `role.ambassador` or `room.moderate.any`, because the client
   offers the ambassador tools at security level 4 too).

   The gate asks through `HasPermissionAsync`, the one extension every gate uses. It sits on
   `IGrainFactory` beside `HasActiveClubAsync`, rather than behind a new service: it is one grain
   call, and that is where the other "does this player have" helpers already live.

   The hook is `EnvelopeFeatureProcessor.DecorateHandler`, which runs once per handler at
   registration and leaves the invoker as it is by default. That is narrower than a pipeline
   behaviour: `AssemblyExplorer.FindAssignees` skips `IsGenericTypeDefinition`, so an open-generic
   `PermissionBehavior<T>` would not be discovered, and a behaviour is keyed by message type, so it
   could not see the handler's attribute anyway. A handler without the attribute, and every other
   dispatch, is untouched. Plugin handlers are registered through the same processor, so their
   attributes are enforced the same way.

   `PermissionGateTests` (`Turbo.Tests/PacketHandlers/`) holds every handler in
   `Turbo.PacketHandlers` to that: every node a handler declares must be registered, and no
   handler may call `HasPermissionAsync` itself (read from the compiled IL of `HandleAsync`). A
   gate at the packet boundary is the attribute or nothing, so what a reviewer reads is what runs.
   `PermissionGateBehaviorTests` (`Turbo.Tests/Messages/`) covers the gate: held, not held, any of
   several, no signed-in player, and no attribute. `PermissionNodeReaderTests` counts a declared
   node as read.

   Gated so far: the 21 moderation packets (`moderation.tool`; still stubs, so the gate is in place
   before the tool is), `ToggleStaffPick` (`navigator.staff_pick`, replacing
   `NavigatorConfig.StaffPickPlayerIds`, which is gone), and the ambassador alert (replacing its
   "moderator-level controller" stand-in).
2. **Inside a grain, asynchronously** — `RoomModerationModule.CanModerateAsync`, catalog
   purchases. One call to `IPlayerPermissionGrain.HasAsync`.

3. **Inside a grain, synchronously** — `RoomSecurityModule.IsRoomOwner` and `HasRights` are
   deliberately synchronous for the wired variables, and `AGENTS.md` forbids blocking on a grain
   call. The room already solves this for badges and `@is_hc`: the value is put on the avatar as it
   enters and pushed when it changes. `IRoomPlayer.Permissions` (the resolved snapshot) is loaded
   in `RoomAvatarModule` beside `LoadBadgesAsync` and `LoadHabboClubAsync`, and replaced by the
   push from §9:

   ```csharp
   public bool IsRoomOwner(PlayerId playerId) =>
       _roomGrain._state.RoomSnapshot.OwnerId == playerId
       || HasPermission(playerId, PermissionNodes.Room.CONTROL_ANY);
   ```

   Built in phase 6. `IRoomPlayer.Permissions` is loaded before the avatar is made rather than
   beside the badges, so the moderator flag (now `Permissions.Has(room.moderate.any)`) is right
   when the avatar attaches, and it is replaced when it changes: the permission grain tells the
   presence and the presence tells the room the player is in, the Habbo Club path, with a change to
   `room.control.any` re-sending the controller level. `HasPermission` reads the avatar for
   synchronous callers; `HasPermissionAsync` reads it too when the player is in the room, which
   every furni move asks, and otherwise asks the permission grain.

   `IsRoomOwner` / `GetIsRoomOwnerAsync` now mean "may act as owner", `room.control.any` included;
   `IsOwnedBy` means whose room it is, for what ownership itself decides — an owner does not rate
   their own room, and the wired `@is_owner` flag. `GetControllerLevelAsync` returns `Moderator` for
   `room.control.any`; the entry check honours `room.enter.locked` / `full` / `hidden`; the room
   moderation checks, the room mute and the ban list honour `room.moderate.any`; pick-up honours
   `room.furni.pickup_any` and `room.furni.steal` (`FurniturePickupType.SendToCtx`).

   `SeedDenyFurniSteal` denies `room.furni.steal` to `moderator` and `admin`, whose wildcards would
   otherwise send every furni they picked up into their own inventory, and
   `perk.navigator.phase_one` to `admin`, whose `*` would otherwise switch its members to the
   phase-one navigator. A hotel that wants either grants it on purpose.

In all three, a gate names a node constant. No code outside the projection compares a
`SecurityLevelType`, and no code asks which group a player is in to decide what they may do.

## 12. What is deliberately left out

- **Contexts.** LuckPerms scopes a node to a server or world. The nearest Habbo equivalent is a
  room, and room-scoped power already exists as `RoomControllerType`. Nothing needs "moderator,
  but only in these rooms" today.
- **Tracks** (promote/demote ladders). Useful once there is a staff UI; until then the console's
  group add/remove is the same thing.
- **An in-game editor.** The console is the v1 surface; `permissions.manage` is registered so a
  nitro-next housekeeping panel can be gated when it exists.
- **Chat commands.** There is no `:command` system in the server. When there is, each command
  registers a `command.<name>` node and the same machinery gates it.

## 13. Schema

Built as `AddPermissions` (entities in `Turbo.Database/Entities/Permissions/`) and seeded by
`SeedPermissions`. Every table is a `TurboEntity` like the rest of the schema — an `id` and the
timestamps — so the pairs below are unique indexes rather than composite keys.

```text
permission_groups             name (unique), display_name, weight
permission_group_parents      group_id, parent_group_id                    unique (group_id, parent_group_id)
permission_group_nodes        group_id, node, value, expires_at?, is_temporary       unique (group_id, node, is_temporary)
permission_group_meta         group_id, meta_key, value, expires_at?, is_temporary   unique (group_id, meta_key, is_temporary)
player_permission_groups      player_id, group_id, expires_at?, is_temporary         unique (player_id, group_id, is_temporary)
player_permission_nodes       player_id, node, value, expires_at?, is_temporary      unique (player_id, node, is_temporary)
player_permission_meta        player_id, meta_key, value, expires_at?, is_temporary  unique (player_id, meta_key, is_temporary)
permission_audit              actor_player_id?, target_type, target_id, action, subject, value?,
                              expires_at?                                  index (target_type, target_id, created_at)
```

`target_type` and `action` are enums stored as ints (`PermissionAuditTargetType`,
`PermissionAuditActionType`), like every other enum in `Turbo.Database`. `node`, `meta_key` and
`subject` are capped at `PermissionNodeFormat.MAX_LENGTH`. Deleting a group or a player cascades
to its rows; `permission_audit` has no foreign keys, so history outlives both. Row-to-snapshot
mapping is `PermissionEntityExtensions`.

`SeedPermissions` also carries `players.perk_flags` over: a set flag that `default` does not
already grant becomes a granted player node. An unset flag becomes nothing — the column was
never read, so it is zero for nearly everyone, and reading that as a denial would take the camera
and trading away from the whole hotel. Nothing reads the column any more: `UnmapPerkFlags` takes it out of the model (and `PlayerSummarySnapshot.Perks` with it) but leaves it in the table, because a CMS that still inserts `perk_flags` would fail against a table without it. Drop it once nothing writes it.
Seeded groups, all editable afterwards:

| Group | Weight | Parents | Nodes | Client level |
| --- | --- | --- | --- | --- |
| `default` | 0 | — | the perks the SSO handler sent `true`, `trade`, `chat.speak` | 0 |
| `vip` | 10 | `default` | `room.floorplan.large`; meta `limit.friends = 500`, `limit.rooms = 100`, `limit.favourite_rooms = 60` | 0 |
| `ambassador` | 20 | `default` | `role.ambassador`, `chat.furni_chooser`, `perk.no_video_offers` | 2 |
| `helper` | 30 | `ambassador` | `perk.guide_tool`, `perk.judge_chat_reviews`, `perk.vote_in_competitions` | 2 |
| `builder` | 35 | `default` | `room.floorplan.save_without_club`, `room.floorplan.large`, `room.furni.branding`, `wired.menu`, `chat.furni_chooser` | 4 |
| `events` | 40 | `default` | `room.event.edit_any`, `room.enter.locked`, `room.enter.full`, `room.furni.youtube_any`, `room.furni.vimeo_edit`, `chat.furni_chooser` | 5 |
| `trial_moderator` | 45 | `helper` | `moderation.tool`, `room.moderate.any`, `room.enter.locked`, `room.enter.full`, `chat.style.staff`, `perk.no_video_offers` | 5 |
| `moderator` | 50 | `trial_moderator` | `room.*` but not `room.control.any`, `moderation.tool`, `wired.menu`, `catalog.builders_club.without_membership`, `catalog.guild.any_group`, `catalog.gift.hide_sender`, `guild.delete_any`, `chat.style.staff`, `chat.furni_chooser`, `perk.no_video_offers` | 5 |
| `senior_moderator` | 60 | `moderator` | `room.control.any` | 5 |
| `manager` | 70 | `senior_moderator`, `builder`, `events` | `navigator.category.staff`, `navigator.staff_pick` | 7 |
| `admin` | 100 | `manager` | `*`, meta `client.security_level = 8` | 8 |

The levels are the real hotel's, so every client draws staff UI as expected without knowing
anything about nodes (`SeedCommunityGroup`; the first seed had `navigator.category.staff` on
`moderator`, which put every moderator on 7). The ladder — `senior_moderator`, `manager`, `admin`
— holds every lower-level node its level makes the client offer, so `perm group <g> info` reports
nothing shown-but-refused for it. `moderator` shows one: controlling every room is a senior's
(`RefineStaffLadder`), but the client ties it to level 5, which the moderation tool needs.
`community` was renamed `manager` there, and `helper` inherits `ambassador`.

The specialist groups (`SeedRetroStaffGroups`) cannot: a builder, event staff or a trial moderator
given every node at their level would be given moderation or every room. They hold only their job,
and a level-only client (Flash) offers them some buttons the server refuses — the acceptable
failure (§8). `builder` stays at 4, the lowest its nodes allow; `events` and `trial_moderator` need
5, because the client itself gates event editing and the moderation tool there. A client using the
`permission.nodes` extension (nitro-next) shows each of them exactly their own buttons.

`room.floorplan.large` is a node rather than meta because the client's `BUILDER_AT_WORK` perk is a
yes/no and the server must agree with it; if a hotel later wants graded area limits, it becomes
meta and the perk is projected from "limit above the client's 3025".

### Floor plans and Builders Club, as built

`RoomGrain.SaveFloorPlanAsync` accepts a save without a Builders Club membership from a holder of
`room.floorplan.save_without_club`, and passes `room.floorplan.large` down so the area limit
(`RoomConfig.FloorPlanMaxArea`) is lifted for exactly the players the client lets past it; the axis
limit still holds. The Builders Club trial rule ("nobody else in the room") leaves out avatars that
moderate every room, as the client's own check does. Opening the Builders Club catalog without a
membership is gated by the client alone, on the level `catalog.builders_club.without_membership`
projects to.
### The other gates, as built

| Node | Where | Before |
| --- | --- | --- |
| `chat.style.staff`, `role.ambassador` | `RoomChatSystem.ResolveStyleIdAsync`, from the speaker's avatar | both hardcoded `false` |
| `chat.speak` | `RoomChatSystem.IsHotelMutedAsync`: a player without it is muted everywhere, told the time left when the denial is temporary | — |
| `trade` | `RoomTradeGrain`, for both sides of a trade | `RoomConfig.TradeRequiresPerk` over `players.perk_flags`; the option is gone |
| `room.event.edit_any` | `RoomGrain.UpdateEventAsync` / `CancelEventAsync` | owner only |
| `guild.delete_any` | `GuildGrain.DeactivateAsync` (the member cap still applies) | owner only |
| `room.furni.rent_cancel_any` | `FurnitureRentableSpaceLogic` | renter or room owner |
| `room.furni.youtube_any` | `FurnitureYoutubeLogic`, playlist and playback | room rights |
| `catalog.guild.any_group` | `CatalogPurchaseGrain`, group furni for a group the buyer is not in (which must exist) | members only |
| `room.furni.branding`, `room.furni.custom_variables`, `room.furni.vimeo_edit` | `RoomActionModule.SetObjectDataAsync`: any one of them | any furni editor could write any object data |

`SetObjectData` has exactly three senders in the Flash client — ad furni branding (security level
4), the info stand's custom variables and the Vimeo display (both 5) — and nitro-next uses it only
for Vimeo, so no player without one of those nodes has a client path to it. The gate does not yet
tell the three furni kinds apart; any of the nodes opens the packet for any furni.

`catalog.gift.hide_sender` has nothing to gate: buying a gift is still a stub
(`PurchaseFromCatalogAsGiftMessageHandler`), and `ShowPurchaserName` is parsed and unread. When
gifts are built, a sender may hide their name only with the node.
## 14. Build order

The nodes the audit found (§17.2) are registered as of phase 4 — so the projection already
raises the level they need — and `SeedAuditPermissionNodes` grants `chat.speak` to `default`
and the group, gift and group-furni nodes to `moderator`. Their server gates are phase 6 work
with the rest of §7.

Each phase is testable on its own and ends with the tree building.

1. **Registry and resolver, no storage.** `PermissionNodes`, `PermissionNodeDefinition`, the
   registry with plugin sources, and the resolver as a pure function from (player assignments,
   group snapshot, registry) to a resolved set and its explanation. The solution has no test
   project yet (`docs/patterns/UnitTestPattern.cs`), so this phase adds `Turbo.Tests` — xunit and
   FluentAssertions in `Directory.Packages.props`, run by the `Code Quality` workflow — in the
   shape that pattern describes. Its first tests carry every rule in §5: player beats group,
   weight order, inheritance and diamonds, specificity, `false` beats `true`, expiry, cycles,
   unregistered nodes.
2. **Schema.** The tables in §13, the migration, the seeded groups, and the data migration from
   `players.perk_flags`.
3. **The grains.** Directory and player grains, writes, audit, expiry timers, the re-resolve path
   for a group edit. Console: `perm check`, `perm user <name> group add|remove`, `perm user <name>
   set|unset`, `perm group <name> set|unset|parent|weight`, each with an optional duration.
4. **The projection.** `UserRights`, `PerkAllowances` replacing the SSO literals, `IsModerator`,
   live resend on change. Testable: a player added to `moderator` sees the mod tool without
   reconnecting, and loses it when a temporary grant expires.
5. **The packet boundary.** `[RequiresPermission]`, enforced by `PermissionGate` at handler
   registration, and the IL test (§11).
6. **The room.** `IRoomPlayer.Permissions`, loaded on entry and pushed on change, then the gates in
   §7 converted one at a time. Each is a line, and each removes a comment. Then the navigator
   (§16: `MinRank` against the derived level, `StaffOnly`, and the `required_node` column), and the
   Builders Club gates.
7. **Meta.** The friend, room and favourite-room limits read through resolved meta (§6).
8. **The reader check.** Built as a test rather than a script, so CI enforces it:
   `PermissionNodeReaderTests` reads the IL of every server assembly and fails for a registered
   node that nothing reads, unless it is listed with the reason nothing on the server can (a perk,
   a node only the client gates on, or one waiting on an unbuilt feature). A node that gains a
   reader must come off the list. Its first run found `wired.menu` unread: the client's wired menu
   treats staff at level 4 as the room's owner (`WiredMenuController.isRoomOwnerOrStaff`), and the
   server had no such rule, so `RoomGrain.GetWiredPermissionsAsync` now gives holders of
   `wired.menu` the owner's wired permissions in any room.

## 15. Decisions

Settled:

- **Strings with a registry**, not an enum: wildcards, and plugins can define nodes; constants keep
  compile-time safety for core.
- **Multiple weighted groups with inheritance**, not one rank per player.
- **Temporary grants**, **meta**, **audit** and **check trace** are in the first ship.
- **The security level is derived** from held nodes, with an optional meta floor.

- **`[RequiresPermission]` is enforced at handler registration.** See §11. The first build
  enforced it with an explicit check in each handler and an IL test tying the two together,
  because the alternative considered then, a pipeline behaviour closed over every message type,
  was rated a moderate risk to every domain and plugin. Wrapping only the invokers of handlers
  that carry the attribute, through a hook that changes nothing by default, avoids that risk and
  removes the per-handler check, so it replaced the first approach.
- **`MinRank` stays**, compared against the derived security level, beside a new optional
  `required_node`. See §16.

`MinRank` was put to Jev (TypeSafe) with the facts above. Keeping `MinRank` plus a node came out
at 0.70 against 0.24 for dropping it, with a 0.73 probability that dropping it would alienate
existing setups; reinterpreting it as a group weight was rejected outright (0.78 that it would
confuse owners).

## 16. Navigator categories: `MinRank`, `StaffOnly` and `required_node`

`MinRank` is never on the wire — `UserFlatCats` carries only `staffOnly`, and both Flash and
nitro-next hide a `staffOnly` category below security level 7. It is a **database convention**
from the retro emulators (`navigator_flatcats.min_rank`), and CMS and housekeeping tools read and
write it. Dropping it would break those for no gain, so it keeps its name and its meaning, with
"rank" read as the security level the player's permissions project to (§8). The retro default
rank ids (1 user … 7 admin) already line up with security levels closely enough that imported
values mean what their owner expects.

A category is visible and usable when **all** of these hold:

| Column | Rule | Why |
| --- | --- | --- |
| `StaffOnly` | the player holds `navigator.category.staff` | the wire flag; that node projects to level 7, so the client agrees |
| `MinRank` | derived security level `>= MinRank` | retro compatibility; replaces the hardcoded `<= 1` |
| `required_node` (new, nullable) | the player holds that node | what `MinRank` cannot say: a VIP-only or event-team category |

`MinRank` is the one place outside the projection that reads a security level, and it is allowed
to because the column *is* a security level by definition. It is still read through the resolved

Built in phase 6, in `NavigatorCategoryAccess.CanSee`, which `NavigatorService.GetFlatCategoriesForPlayerAsync`
uses. That one method answers the category list the client is sent, the categories the hotel view
previews, the category search, and which category a player may create a room in or move one to
(`RoomSettingsSaveExtensions`), so a staff-only category is closed to a crafted packet as well as
hidden. A regular player (security level 0) counts as rank 1, as in the retro emulators, so a
category left at the retro default of `min_rank = 1` is everyone's. `required_node` is added by
`AddNavigatorCategoryRequiredNode`.
set's derived level, never by comparing groups.

## 17. Audit against LuckPerms and the clients (2026-09-28)

Read against the LuckPerms wiki source (`LuckPerms/wiki`, `pages/*.md`) and against every client
on disk: AIR `WIN63-202603212315-320263584` and `WIN63-202609091217-117204808` (both in
`SWF Sources`), its TypeScript transpile in `mikkel-project`, and nitro-next. The two AIR
revisions read the same nine perks and the same `hasSecurity` levels; September adds two level-4
sites (`RewardTrackController`, `VariableFxVisualizationSettingsPreset`) the client-gates survey
already has. mikkel matches March exactly.

### 17.1 Wrong in what is built — fixed

All four are fixed: `AllowTemporaryBesidePermanent` adds `is_temporary` to each unique key, the
resolver ranks temporary over permanent at equal specificity, writes take a
`PermissionExpiryModeType`, meta keys register a `PermissionMetaSelectionType`, and
`chat.style.staff` carries `Employee`. What follows is the finding as it was made.

1. **A temporary node overwrites a permanent one.** The unique index is `(target, node)`, so
   `perm user x set trade false 7d` on a player who holds `trade = true` of their own replaces the
   grant, and when the sanction runs out the grant is gone too. LuckPerms stores both and lets
   the temporary one win while it lasts ("temporary permissions will override non-temporary
   permissions"). Fix: allow one permanent and one temporary row per `(target, node)` (and per
   meta key and membership), and in the resolver let a live temporary assignment beat a
   permanent one at equal specificity, before `false` beats `true`.
2. **Re-adding a temporary assignment silently replaces its expiry.** LuckPerms makes this a
   choice (`temporary-add-behaviour`: `accumulate`, `replace`, `deny`). Keep replace as the
   default, and add an `extend` form to the console (`perm user x group add vip 30d --extend`)
   for "add another month of VIP".
3. **Meta has one selection rule.** First-by-weight is wrong for limits: a player in `vip`
   (weight 10, `limit.rooms = 50`) and `builder` (weight 5, `limit.rooms = 200`) gets 50.
   LuckPerms has `meta-value-selection` per key (`inheritance`, `highest-number`,
   `lowest-number`). Register the rule with the meta key in `PermissionMetaDefinition`; limits
   default to highest.
4. **`chat.style.staff` has no client level.** The client offers staff bubbles only at
   `hasSecurity(4)` (`RoomChatInputView`), so the node must carry `ClientLevel = Employee` or a
   holder is allowed a bubble the client never shows.

### 17.2 Client gates the catalogue (§7) is missing

| Node | Client level | Client site | Server today |
| --- | --- | --- | --- |
| `room.event.edit_any` | 5 | navigator `eventMod`, set in `IncomingMessages.onUserRights`; `RoomEventInfoCtrl` | `RoomGrain.UpdateEventAsync` checks owner only |
| `navigator.staff_pick` | 7 | navigator `roomPicker`; `RoomInfoViewCtrl` | **`NavigatorConfig.StaffPickPlayerIds`**, a hardcoded id list — replace |
| `guild.delete_any` | 5 | `GroupDetailsCtrl` | `GuildGrain.DeactivateAsync`: owner only |
| `catalog.guild.any_group` | 4 | `GuildForumSelectorCatalogWidget`, nitro `CatalogGuildSelectorWidgetView` | not yet checked |
| `catalog.gift.hide_sender` | 5 | `PurchaseConfirmationDialog.isModerator` (hide your face on a gift) | field not read |
| `room.furni.rent_cancel_any` | 5 | `RentableSpaceDisplayWidget` | not yet checked (`CancelSpaceRentInteraction`) |
| `room.furni.branding` | 4 | `InfoStandFurniView` "save_branding_configuration" → `SetObjectData` | **any furni editor may write any object data** — gate ad furni on this |
| `room.furni.custom_variables` | 5 | `InfoStandFurniView` custom variable list | — |
| `room.furni.youtube_any` | 4 | `FurnitureYoutubeDisplayWidgetHandler` (owner or staff) | — |
| `room.furni.vimeo_edit` | 5 | `FurnitureVimeoDisplayWidgetHandler`, nitro `FurnitureVimeoWidget` | — |
| `perk.no_video_offers` | 1 | `VideoOfferManager` turns video ads off at `securityLevel >= 1` | — |
| `chat.speak` | — | server-only: hotel mute | `ModMute` is a stub |

Two more are the ambassador role rather than new nodes: nitro-next lets an ambassador into a
`NoobLobby` room (`registerNavigatorHandlers`), which the server does not model, and
`AmbassadorAlertMessageHandler` stands in with "moderator-level controller" until
`role.ambassador` exists. Note also that the navigator's `eventMod` and `roomPicker` are set on
`UserRights` and **never cleared**: lowering a player's level live does not take them away until
relog, like `topSecurityLevel`.

### 17.3 Code already waiting for this system

`NavigatorService` (`MinRank <= 1`), `NavigatorConfig.StaffPickPlayerIds`,
`AmbassadorAlertMessageHandler`, `PlayerSubscriptionGrain.SendStatusAsync`,
`RoomFurniModule.BuildersClub` (trial rule counts staff), `RoomChatSystem` (staff and ambassador
bubbles), and the 21 moderation handlers, all stubs. `ModTradingLock` and `ModMute` are
temporary player denials of `trade` and `chat.speak` once the mod tool is built, and
`SanctionStatus` can report them; bans stay their own table.

### 17.4 LuckPerms features, and whether to take them

| LuckPerms | Here | Verdict |
| --- | --- | --- |
| Groups, weights, inheritance, negation, wildcards, temporary nodes, meta, action log, `permission check` | built | — |
| Default group not configurable (rename by display name, extend by parent) | same | keep |
| Temporary beats permanent; `temporary-add-behaviour` | missing | **fix** (17.1) |
| `meta-value-selection` per key | missing | **fix** (17.1) |
| `sync` — reload after the database was edited by something else | built: `perm reload` (§9) | — |
| Argument-based command permissions (who may grant what) | missing | **take before any in-game editor**: a manager may only grant nodes they hold and groups lighter than their heaviest (`permissions.manage.*`) |
| Verbose (watch checks live) | built: `perm user <player> verbose` (§10) | — |
| `group listmembers`, `log recent`/`search`, `search <node>` (who holds it) | built: `perm group <g> members`, `perm log [search]`, `perm search` (§9) | — |
| `group.<name>` as a node (membership checkable like a permission) | built (§5) | — |
| Events (`NodeAddEvent`, `UserDataRecalculateEvent`, `UserPromoteEvent`) | built: `PlayerPermissionsChangedEvent` (§9) | — |
| Log notify (tell online staff of changes) | missing | later, with the mod tool |
| Tracks, clone/rename group key, clear, bulk update, export/import, web editor | missing | later; bulk renames of a node are a migration, backups are database dumps |
| Contexts (server/world), regex and shorthand nodes, prefix/suffix stacking, messaging service | — | skip: room rights are the context, wildcards cover shorthand, no client draws prefixes, Orleans is the messaging |