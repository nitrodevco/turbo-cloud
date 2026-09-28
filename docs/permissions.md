# Permissions, groups and security levels

Implementation plan for a permission system. **Phases 1 to 3 of §14 are built**: the node constants,
the registry and the resolver in `Turbo.Primitives/Players/Permissions/`, tested in
`Turbo.Tests`; the tables, seeded groups and perk-flag carry-over (§13); and the grains, audit, expiry and
the `perm` console command (§9). Nothing enforces a permission or tells the client yet.

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
node, then the longest wildcard (`room.enter.*` beats `room.*` beats `*`). At equal specificity,
**`false` beats `true`**. Expired assignments do not exist for resolution.

Meta resolves in the same order: player, then groups by weight, then the registered default.

This is LuckPerms' order with contexts removed — see §12.

## 6. Meta

Meta is for the limits retros kept as per-rank columns. `AGENTS.md` already says a limit is a
config option on its module's config class; meta does not change that. **The config option stays
the hotel default, and meta overrides it per group or per player.**

```csharp
registry.AddMeta(MetaKeys.Messenger.FriendLimit, (PlayerConfig c) => c.MessengerNormalFriendLimit);
```

The grain that enforces the limit asks the player's resolved meta instead of reading the config
directly, and the answer already has the config default folded in. A key is registered with its
type (`int`, `bool`, `string`); a stored value that does not parse is logged and treated as unset.

Start with the limits that exist and that a VIP group would plausibly raise: friends, rooms owned,
and the floor plan area (which `LargeFloorPlans` in §7 currently models as a node — see §13).
Display meta (name prefixes, colours) waits for a client that draws it.

## 7. The node catalogue

The first members are not invented — they are the comments already sitting in the code and the
client gates from §2. Each names the one place that reads it.

| Node | Gates | Client level | Today |
| --- | --- | --- | --- |
| `room.control.any` | `RoomSecurityModule.IsRoomOwner` and `GetControllerLevelAsync` (returns `Moderator = 5`) | 5 | `// if has perm any_room_owner true`, `// if has perm room_rights Rights`. **One node, not two**: the client's only channel is `securityLevel >= 5`. |
| `room.furni.steal` | `GetFurniPickupTypeAsync` → `SendToRequester` | — | `// if can steal furni` |
| `room.furni.pickup_any` | info stand pickup of another player's furni | 4 | client-only today |
| `room.enter.locked` | `RoomEntryModule.CheckAccessAsync` (`bypassDoor`) | 5 | |
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
player's non-default flags into player nodes, and a later migration drops the column.

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
`Expired`, `ProtectedGroup`, `WouldCycle`, `AlreadyExists`, `NotFound`) and take the actor for the
audit — a player, or `null` for the console. Neither grain checks that the actor may make the
change; whoever calls it does (`permissions.manage`, once something in game calls it).

After any change to its resolved set it will re-project and send `UserRights` and
`PerkAllowances` itself (the client applies both live — no reconnect), and push the new snapshot
to the room the player is in. That is phases 4 and 6.

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
perm user <player> info | audit [count]
perm user <player> group add <group> [duration] | group remove <group>
perm user <player> set <node> [true|false] [duration] | unset <node>
perm user <player> meta set <key> <value> [duration] | meta unset <key>
perm groups
perm group <group> info | audit [count] | create [weight] [display name] | delete
perm group <group> weight <weight> | rename <display name>
perm group <group> set <node> [true|false] [duration] | unset <node>
perm group <group> meta set <key> <value> [duration] | meta unset <key>
perm group <group> parent add|remove <parent>
```

Durations are `30s`, `15m`, `12h`, `7d`, `2w`; left out, the assignment is permanent.
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

## 11. How gating is enforced

The failure mode of a permission system is a gate nobody remembers to write. Gates live at three
depths.

1. **At the packet boundary** — "may this player send `ModerateRoom` at all". The handler carries
   `[RequiresPermission(PermissionNodes.Moderation.TOOL)]`, which declares the gate where a
   reviewer reads it, and its first line is the check:

   ```csharp
   if (!await permissions.HasAsync(ctx, PermissionNodes.Moderation.TOOL, ct))
       return;
   ```

   A test in `Turbo.Tests` (added in phase 1, §14) inspects every handler: one with the attribute
   whose body does not call `HasAsync` with the same node fails, and so does a check with no
   attribute. CI runs it, so the declaration and the enforcement cannot drift apart.

   A pipeline behaviour that reads the attribute would remove the line, but
   `AssemblyExplorer.FindAssignees` skips `IsGenericTypeDefinition`, so an open-generic
   `PermissionBehavior<T>` is not discovered, and closing it over every message type means
   changing `MessageFeatureProcessor` — the dispatch every domain and every plugin goes through.
   Not worth that risk for one line per gated handler. If the pipeline gains open-generic
   behaviours for another reason, the attribute is already in place to move onto.

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
permission_group_nodes        group_id, node, value, expires_at?           unique (group_id, node)
permission_group_meta         group_id, meta_key, value, expires_at?       unique (group_id, meta_key)
player_permission_groups      player_id, group_id, expires_at?             unique (player_id, group_id)
player_permission_nodes       player_id, node, value, expires_at?          unique (player_id, node)
player_permission_meta        player_id, meta_key, value, expires_at?      unique (player_id, meta_key)
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
and trading away from the whole hotel. The column stays until phase 4 stops loading it.
Seeded groups, all editable afterwards:

| Group | Weight | Parents | Nodes |
| --- | --- | --- | --- |
| `default` | 0 | — | the perks the SSO handler sends `true` today, and `trade` |
| `ambassador` | 20 | `default` | `role.ambassador`, `chat.furni_chooser` |
| `helper` | 30 | `default` | `perk.guide_tool`, `perk.judge_chat_reviews` |
| `moderator` | 50 | `helper` | `room.*`, `moderation.tool`, `wired.menu`, `catalog.builders_club.without_membership`, `chat.style.staff`, `navigator.category.staff` |
| `admin` | 100 | `moderator` | `*` |

`room.floorplan.large` is a node rather than meta because the client's `BUILDER_AT_WORK` perk is a
yes/no and the server must agree with it; if a hotel later wants graded area limits, it becomes
meta and the perk is projected from "limit above the client's 3025".

## 14. Build order

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
5. **The packet boundary.** `[RequiresPermission]`, `IPermissionService`, and the reflection test
   (§11).
6. **The room.** `IRoomPlayer.Permissions`, loaded on entry and pushed on change, then the gates in
   §7 converted one at a time. Each is a line, and each removes a comment. Then the navigator
   (§16: `MinRank` against the derived level, `StaffOnly`, and the `required_node` column), and the
   Builders Club gates.
7. **Meta.** The friend, room and area limits read through resolved meta.
8. **`scripts/permgap.py`.** Lists every registered node that nothing reads, in the style of
   `packetgap.py` and `wiredgap.py`. A node with no reader is a gate somebody meant to write and
   did not, which is exactly the failure this system invites.

## 15. Decisions

Settled:

- **Strings with a registry**, not an enum: wildcards, and plugins can define nodes; constants keep
  compile-time safety for core.
- **Multiple weighted groups with inheritance**, not one rank per player.
- **Temporary grants**, **meta**, **audit** and **check trace** are in the first ship.
- **The security level is derived** from held nodes, with an optional meta floor.

- **`[RequiresPermission]` without the pipeline change.** See §11: the attribute is enforced by an
  explicit check and a build-time test, not by a pipeline behaviour.
- **`MinRank` stays**, compared against the derived security level, beside a new optional
  `required_node`. See §16.

Both were put to Jev (TypeSafe) with the facts above. Keeping `MinRank` plus a node came out at
0.70 against 0.24 for dropping it, with a 0.73 probability that dropping it would alienate
existing setups; reinterpreting it as a group weight was rejected outright (0.78 that it would
confuse owners). The explicit-check-plus-test approach came out at 0.89, with the
`MessageFeatureProcessor` change rated a moderate risk to every domain and plugin.

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
set's derived level, never by comparing groups.
