# Permissions, ranks and security levels

Implementation plan for a permission system: a rank per player carrying permissions, plus
permissions granted or taken from one player on their own. Nothing here is built yet.

Written for whoever implements it. Read `AGENTS.md` first — the grain rules and the constants
rules both bear on the shape below.

## 1. What is there now

Four channels already exist and three of them are inert.

| Channel | Where | State |
| --- | --- | --- |
| `SecurityLevelType` | `Turbo.Primitives/Players/Enums/` | Declared with nine members. **Nothing reads or writes it** except `SSOTicketMessageHandler`, which sends `None` flat. |
| `PlayerPerkFlags` | `Turbo.Primitives/Players/Enums/` | Thirteen flags with full legacy-string mapping, loaded into `PlayerLiveState.Perks` from `players.perk_flags` — and then **never read**. The SSO handler builds the `PerkAllowances` list out of hardcoded literals instead. |
| `RoomControllerType` | `Turbo.Primitives/Rooms/Enums/` | Live and enforced. Room-scoped only: owner, rights, group. Not a hotel-wide permission. |
| `NavigatorFlatCategoryEntity.MinRank` / `StaffOnly` | `Turbo.Database/Entities/Navigator/` | Columns exist; `NavigatorService` filters `!x.StaffOnly && x.MinRank <= 1`, with the 1 hardcoded because no player has a rank. |

There is no rank table, no permission table, and no column on `players` for either.

> **Read the two client surveys first.** `docs/permissions-client-gates.md` lists every place the
> client asks about a security level or a room controller level;
> `docs/permissions-client-perks.md` does the same for the `PerkAllowances` channel. Findings from
> both change the design below and are called out where they land.

## 2. What the client gates on, which is the contract we have to fit

Three separate things, and conflating them is the main trap.

**`hasSecurity(n)` is a threshold, not a role.** `SessionDataManager.hasSecurity(n)` is literally
`securityLevel >= n`, against the single int in `UserRights`. So the *order* of
`SecurityLevelType` is a wire contract, and its members already line up with the levels the client
asks for:

| Level | `SecurityLevelType` | What the client unlocks at it |
| --- | --- | --- |
| 2 | `Partner` | one chat command |
| 4 | `Employee` | `:kick`, `:mute`, the wired menu, saving a floor plan without Builders Club, staff options in room create, picking up anyone's furni on the info stand |
| 5 | `Moderator` | the moderation tool, the Builders Club catalog without a membership, deleting any guild, entering any room from the navigator, cancelling anyone's rent |
| 7 | `Community` | staff-only navigator categories |

**`isPerkAllowed(code)` is a capability list**, sent as `PerkAllowances` with a per-perk refusal
text. It is how the client greys a button out. It is **not** login-only — perks merge into the
client's dictionary and each update raises `PerksUpdatedEvent` — and it fails closed, so a code
the server never sends is denied. `PlayerPerkFlags` nearly mirrors it: of the nine codes the
client reads, eight have a flag and `NAVIGATOR_PHASE_ONE_2014` does not. See
`docs/permissions-client-perks.md`.

**`roomControllerLevel` is room-scoped** and already works.

So: the client needs one *number* and one *list of codes*. Neither is the server's authority —
both are a **projection** of the permissions below. Getting that the wrong way round (checking a
security level server-side because the client does) is how a permission system rots.

## 3. The model

```text
ranks                  id, name, security_level, is_staff, sort_order
rank_permissions       rank_id, permission            PK (rank_id, permission)
player_permissions     player_id, permission, granted PK (player_id, permission)
players                + rank_id  (FK, nullable → the hotel's default rank)
```

- **A rank carries a security level**, so `UserRights` has something real to send, and a set of
  permissions.
- **`player_permissions.granted`** is a three-state in two columns: a row with `granted = true`
  adds a permission the rank does not give, a row with `granted = false` takes one the rank does.
  **A revoke beats a grant beats the rank.** Taking something away has to be the strongest word
  available, or there is no way to sanction one account without inventing a rank for it.
- **Permissions are an enum**, `PermissionType`, in `Turbo.Primitives/Players/Enums/`. Not
  strings: each member exists because one code path gates on it, so the set is the server's to
  define and the compiler should be the one that catches a typo. Not `[Flags]`: the catalogue in
  §6 already passes what 64 bits would hold.
- The column stores the enum as an int, like every other enum in `Turbo.Database`.

### Why not reuse `PlayerPerkFlags` for this

Because the two answer different questions. A perk is *what the client may draw*; a permission is
*what the server will do*. Several permissions have no perk (entering a locked room), several
perks have no permission (`MOUSE_ZOOM`), and a perk carries a refusal string for the UI. Keep
them separate and derive the perk list from the permissions at login.

## 4. Where the authority lives

Two grains, following the shapes already in the tree.

**`IRankDirectoryGrain`** — one per hotel, `[KeepAlive]`, shaped like `BadgeDirectoryGrain`:
rank definitions and their permission sets, read once on activation and answered from memory,
with a `ReloadAsync` for an operator who edits a rank. Nothing writes through it.

**`IPlayerPermissionGrain`** — one per player, shaped like `PlayerSubscriptionGrain`, holding the
**resolved** set: the rank's permissions, plus that player's grants, minus their revokes. It also
holds the rank's security level. It answers `HasAsync(PermissionType)` and
`GetSnapshotAsync()` from memory, so a check costs one grain call and no query.

A rank edit has to reach players already online: `RankDirectoryGrain.ReloadAsync` tells the
presence of every affected player, which tells their permission grain to re-resolve. Same
direction as everything else — directory → presence → player.

## 5. How gating is validated

This is the part worth getting right, because the failure mode of a permission system is a gate
nobody remembers to write.

### The problem

Gates live at three different depths, and they cannot all be checked the same way:

1. **At the packet boundary** — "may this player send `ModerateRoom` at all". Coarse, one check
   per message type.
2. **Inside a grain, asynchronously** — `RoomModerationModule.CanModerateAsync`, catalog
   purchases. A grain call is fine here.
3. **Inside a grain, synchronously** — `RoomSecurityModule.IsRoomOwner` and `HasRights` are
   deliberately synchronous "for callers that cannot await (the wired variables)". A grain call
   is *not* fine here, and `AGENTS.md` forbids blocking on one.

### The answer, in three layers

**Layer 1 — declare, never compare.** Every gate names a `PermissionType`. No code outside the
projection in §7 compares a `SecurityLevelType`, and no code re-derives a rank. A reviewer can
then read a gate and know what it gates on.

**Layer 2 — a pipeline behaviour for the packet boundary.** `Turbo.Pipeline` already supports
this: `IMessageBehavior<T>` wraps a handler with a `next` it may decline to call, ordered by
`[Order(n)]`. A `[RequiresPermission(PermissionType.X)]` attribute on a handler, plus one
behaviour that reads it and short-circuits, gates a whole handler in one line and cannot be
forgotten halfway down a method.

> **This needs a small change to shared infrastructure, and it is the one open decision.**
> `AssemblyExplorer.FindAssignees` skips `IsGenericTypeDefinition`, so an open-generic
> `PermissionBehavior<T>` will not be discovered — `MessageFeatureProcessor` would have to close
> the generic over each registered message type. That is a change to the pipeline every domain
> uses. The alternative is an injected `IPermissionService` and an explicit first line in each
> gated handler: no infrastructure change, but the gate is a line somebody can forget. My
> recommendation is the behaviour, because "cannot be forgotten" is the whole point; see §9.

**Layer 3 — carry the resolved set to where synchronous checks happen.** The room already solves
this twice, for badges and for `@is_hc`: the value is put on the avatar as it enters and pushed
again when it changes. `IRoomPlayer.Permissions` (the resolved set) and
`IRoomPlayer.SecurityLevel` do the same, loaded in `RoomAvatarModule` beside `LoadBadgesAsync`
and `LoadHabboClubAsync`. Then `RoomSecurityModule` stays synchronous:

```csharp
public bool IsRoomOwner(PlayerId playerId) =>
    _roomGrain._state.RoomSnapshot.OwnerId == playerId
    || HasPermission(playerId, PermissionType.AnyRoomOwner);
```

which is exactly what the `// if has perm any_room_owner true` comment there has been waiting for.

## 6. The permission catalogue

The first members are not invented — they are the comments already sitting in the code and the
client gates from §2. Each line names the one place that reads it.

| `PermissionType` | Gates | Today |
| --- | --- | --- |
| `ControlAnyRoom` | `RoomSecurityModule.IsRoomOwner` and `GetControllerLevelAsync` | `// if has perm any_room_owner true` and `// if has perm room_rights Rights`. **One permission, not two**: the client's only channel for it is `securityLevel >= 5`, which its `isAnyRoomController` reads — see the client-gates doc. |
| `StealFurni` | `GetFurniPickupTypeAsync` → `SendToRequester` | `// if can steal furni` |
| `EnterLockedRoom` | `RoomEntryModule.CheckAccessAsync` (`bypassDoor`) | client's `hasSecurity(5)` |
| `EnterFullRoom` | same | staff should not be turned away by capacity |
| `EnterHiddenRoom` | `RoomEntryAccessType.HiddenByBuildersClub` | staff currently refused with everyone else |
| `ModerateAnyRoom` | `RoomModerationModule` kick/mute/ban, over `ModSettings` | `hasSecurity(4)` for `:kick` / `:mute` |
| `SaveFloorPlanWithoutClub` | `RoomGrain.SaveFloorPlanAsync` | `hasSecurity(4)`; see `docs/builders-club.md` §7 |
| `LargeFloorPlans` | `RoomMapModule` area limit | the `BUILDER_AT_WORK` perk |
| `BuildersClubWithoutMembership` | `HabboCatalog.toggleCatalog` | `hasSecurity(5)` |
| `StaffOnlyNavigatorCategories` | `NavigatorService`, `EnforceCategoryCtrl` | `hasSecurity(7)`, and the hardcoded `MinRank <= 1` |
| `UseModerationTool` | the moderation packets | `hasSecurity(5)` |
| `WiredMenu` | `WiredMenuController` | `hasSecurity(4)` |
| `IsAmbassador` | `UserRights.IsAmbassador`, ambassador mutes in `InfoStandWidgetHandler` | hardcoded `false` |

Note what this does to the Builders Club work: three of its gates (`SaveFloorPlanWithoutClub`,
`LargeFloorPlans`, `EnterHiddenRoom`) are recorded there as blocked on staff ranks, and the trial
rule "nobody else in the room" counts a staff member as an ordinary visitor where the client's own
check excludes moderators. All four are one-line changes once this exists.

## 7. The projection to the client

One place, and only one, turns permissions into what the client is told:

- **`UserRights.SecurityLevel`** ← the rank's security level. Sent by
  `IPlayerSubscriptionGrain.SendStatusAsync`, which already owns that packet and already sends
  `SecurityLevelType.None` with a comment saying it is waiting for this.
- **`PerkAllowances`** ← a `PermissionType` → `PlayerPerkFlags` map, applied over the player's
  resolved set, replacing the hardcoded block in `SSOTicketMessageHandler`. The map is **not a
  bijection**: four server flags gate nothing in this client and one perk it reads has no flag, so
  do not write it as one. The refusal strings stay where they are; they are UI text, not policy —
  and this client never reads them, so they are also not user-visible.
- **`IRoomPlayer.IsModerator`** ← `ModerateAnyRoom`, which is what makes it reach
  `RoomPlayerAvatarSnapshot` and the client's own moderator checks.

## 8. Build order

1. **Schema and enum.** `PermissionType`, the three tables, `players.rank_id`, a migration, and a
   data migration seeding a default rank plus one staff rank per security level the client asks
   for (2, 4, 5, 7). An existing hotel's players land on the default rank.
2. **The grains.** `RankDirectoryGrain`, `PlayerPermissionGrain`, the resolve rule, and the
   reload path for a rank edit. Testable alone: a player's resolved set is right.
3. **The projection.** `UserRights.SecurityLevel`, the real `PerkAllowances`, `IsModerator`.
   Testable: a staff account sees staff UI.
4. **The packet boundary.** `[RequiresPermission]` and the behaviour (or the service; §9).
5. **The room.** `IRoomPlayer.Permissions`, loaded on entry and pushed on change, then convert
   the fourteen gates in §6 one at a time. Each is a line, and each removes a comment.
6. **`scripts/permgap.py`.** Lists every `PermissionType` member that nothing reads, in the style
   of `packetgap.py` and `wiredgap.py`. A permission with no reader is a gate somebody meant to
   write and did not, and that is exactly the failure this system invites.

## 9. Decisions I would want confirmed before starting

1. **The pipeline change.** `[RequiresPermission]` needs `MessageFeatureProcessor` to close an
   open-generic behaviour over every message type — shared infrastructure every domain uses.
   Worth it, in my view, because a declarative gate cannot be forgotten and an inline one can.
   Say so if you would rather keep the pipeline untouched and accept explicit checks.
2. **Whether a rank's security level is its only staff signal.** `ranks.is_staff` above is
   redundant with `security_level >= 4` and could be dropped. I kept it because "is this account
   staff" reads better than a magic number, but it is one more thing to keep consistent.
3. **Enum over strings.** An operator cannot add a permission without a code change. That is the
   point — a permission with no reader does nothing — but it does mean a hotel cannot invent one
   for its own scripts.
