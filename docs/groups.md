# Groups

Implementation plan for the group system: identity and badges, membership, the guild homeroom,
guild furni, the navigator's guild searches and group forums.

**The first ship is complete.** Phases 1 to 7 of section 12 are built: a group can be made,
seen, joined, run and deleted; its homeroom hands out the group's rights; its furni wears its
badge and colours; and the navigator finds it. Every group system placeholder that was in the
tree is gone.

The second ship — group forums, phases 8 and 9 — is not started. Groups report `hasBoard =
false`
and the client draws no forum link, which is the truth rather than a stub.

Each phase in section 12 says what it covers and marks itself *Done* when it lands. The rest of
this document is written as the plan it started as: the wire shapes and client rules in sections
1 to 4 hold whether or not the code behind them exists yet.

Client behaviour below was read from the deobfuscated Flash client
(`com/sulake/habbo/groups/**`, `com/sulake/habbo/friendbar/groupforums/**`,
`com/sulake/habbo/session/HabboGroupInfoManager.as`), wire shapes from `packet-tool`, and the
text and variable keys from the hotel's `ExternalTexts.json` / `ExternalVariables.json`. Verify
against those three before changing anything here. Where `packet-tool`'s generated field names
disagree with the SWF, the SWF wins and section 3 says so.

The client calls the feature *groups* in its texts and *guilds* on the wire. This document uses
whichever the thing being named uses: the packets and the code are `Guild*`, the user-facing
concept is a group.

## 1. What a group is

A named, badged club owned by one player, anchored to one room — its *homeroom*, which the
client also calls the base or HQ. The group hands its members rights in that room, a badge they
can wear, furni that recolours itself to the group's palette, and optionally a forum.

### Identity

Name, description, a badge built from parts, and two colours. The badge is a string code the
client never draws itself: it hands the code to an external imager
(`group.badge.url` = `.../habbo-imaging/badge/%imagerdata%.gif`) and shows the result. So the
server's whole responsibility for a badge is generating a stable code from the parts the client
picked.

A badge is five layers: one base and four symbols. Each layer is a `(partId, colorId, position)`
triple, and the client sends them as a flat int array in that order. The parts and the colours
it may pick from are not hotel data it guesses — the server sends them in `GuildEditorData`.

### The homeroom is chosen once

`group.edit.base.warning`: *"Choose your Groups homeroom carefully - the homeroom cannot be later
changed."* The room must be owned by the creator, and `GuildCreationInfo` marks each owned room
with `hasControllers` so the client can warn (`group.edit.error.controllers`) that existing room
rights will be overridden and cannot be restored. A room that is already some group's homeroom is
refused with edit failure `0`.

### Types and who may join

The editor offers three types; the wire carries five. Two exist for groups the hotel makes, not
players.

| Type | Wire | Client behaviour |
| --- | --- | --- |
| Regular | 0 | Anyone joins instantly. 5000 member limit. |
| Exclusive | 1 | Join sends a request; admins approve or reject. |
| Private | 2 | Closed. No members accepted. |
| Large | 3 | Not settable in the editor. |
| (open) | 4 | Not settable in the editor; joins like Regular. |

`HabboGroupDetailsData` derives the three buttons from type and the viewer's own status, and the
server must keep to the same rules or the client will show a button that fails:

- `joiningAllowed` — `status == NotMember && (type == 0 || type == 4)`
- `requestMembershipAllowed` — `status == NotMember && type == 1`
- `leaveAllowed` — `isGuild && !isOwner && status == Member`

### Ranks

`MemberData` carries one int for the member's rank. The client reads `admin` as `== 1`,
`owner` as `== 0`, `member` as `!= 3`, and `blocked` as `== 4`.

| Rank | Wire | Meaning |
| --- | --- | --- |
| Owner | 0 | The creator. Cannot leave; deletes the group instead. |
| Admin | 1 | Approves requests, kicks, blocks, decorates. |
| Member | 2 | |
| Requested | 3 | Pending approval. Not a member. |
| Blocked | 4 | Kicked with blocking on; cannot rejoin until unblocked. |

### Decoration rights

One setting, `rightsLevel`, decides who gets controller rights in the homeroom: `0` owner only,
`1` admins, `2` all members (`group.edit.settings.decoration.*`). `HabboGroupDetails` reports it
to the client as the `membersCanDecorate` boolean, which only draws an icon; the enforcement is
entirely server-side in `RoomSecurityModule`.

### Leaving, kicking and blocking

All three are the same packet. `GroupDetailsCtrl.onLeave` calls
`handleUserKick(ownAvatarId, groupId)`, exactly as the member list calls it for somebody else.
Before sending `KickMember`, the client asks `GetMemberGuildItemCount` and waits for
`GuildMemberFurniCountInHQ` so it can say *"%user% has %amount% Furnis in the Group homeroom"* in
the confirmation. The furni goes back to its owner when they leave.

### Deletion

Owner-only (or staff with security 5), gated client-side on `group.deletion.enabled`, and the
hotel also publishes `group.deletion.maximum.members = 500` — a group past that size is not
deletable from the client, and the server should hold the same line rather than trust it.
`group.deleteconfirm.desc` states the contract: **all** furni in the homeroom returns to its
owners' inventories, members are kicked, and the forum is deleted. `HabboGroupDeactivated`
follows.

### Badges around the hotel

A player picks one group as their favourite; that badge rides on their room avatar and shows in
their profile. Two separate mechanisms feed it:

- `HabboGroupBadges` — a map of groupId → badgeCode. `HabboGroupInfoManager` requests it **on
  every room ready**, and the client resolves every badge it draws through that map. The server
  answers with the groups relevant to the room the player is in.
- `FavoriteMembershipUpdate` — pushed when somebody in the room changes their favourite, carrying
  the room index, group id, status and name.

### The windows

For orientation when reading the client: `DetailsWindowCtrl` + `GroupDetailsCtrl` (the group
info popup), `GuildMembersWindowCtrl` (member list, paging, search), `GuildManagementWindowCtrl`
(the 5-step create/edit wizard), `GroupRoomInfoCtrl` (the badge attached to the room info),
`GroupCreatedWindowCtrl`, `ExtendedProfileWindowCtrl` (the group list on a profile, and where the
favourite is picked), `HcRequiredWindowCtrl` (shown for join failure 4 and edit failure 2).

## 2. What already exists in the tree

The packet layer was scaffolded ahead of the feature: every header, parser, serializer, message,
composer and handler already existed, with the parsers ignoring their payload, the composers
carrying `TODO: add properties if/when identified`, and the handlers being
`await ValueTask.CompletedTask`. The table below is that starting state, kept because it is
still true of everything phases 4 onwards will touch — the membership and forum packets are all
still in it.

| Layer | State |
| --- | --- |
| `Revision20260909/Headers.cs` | All 30 group and forum ids present, both directions. |
| `Parsers/Users/*Guild*`, `Parsers/Groupforums/*` | 18 + 12 files, each returning a fieldless record. |
| `Serializers/Users/*Guild*`, `Serializers/Groupforums/*` | 17 + 9 files with empty `Serialize` bodies. |
| `Messages/Incoming/Users`, `Messages/Incoming/Groupforums` | Records with no properties. |
| `Messages/Outgoing/Users`, `Messages/Outgoing/Groupforums` | Composers with no properties. |
| `PacketHandlers/Users`, `PacketHandlers/Groupforums` | 46 no-op handlers. |
| `Turbo.Primitives/Guilds/` | `GuildId`, `GuildInfoSnapshot`. |
| `Serializers/Users/Data/GuildInfoSerializer.cs` | Complete, and already matches the client's `GuildMembershipsDataParser` field for field. Reuse it; do not write a second one. |

Behind that there is now a `Turbo.Guilds` project, four tables, and the
`GuildDirectory`/`Guild`/`PlayerGuild` grains, carrying identity, editing, the whole roster, the
room side, guild furni and the navigator searches. The forum tables and grain do not exist.

These sites were written against a group system that returned nothing, and each started working
as the system behind it landed. **All of them are live now**; the table is kept because it is the
shortest description of what the group system actually reaches, and the cheapest list to re-check
when any of it is touched.

| Site | What it was |
| --- | --- |
| `RoomGrain.GetIsGroupRoomAsync` | `Task.FromResult(false)` |
| `RoomSecurityModule.GetControllerLevelAsync` | `if (isGroupRoom)` branch holds three comment-only TODOs; `RoomControllerType.GroupRights` / `GroupAdmin` already exist and are already honoured by everything downstream |
| `RoomSecurityModule.CanManipulateFurniAsync` | `canGroupDecorate` hardcoded `false` |
| `NavigatorService` | `case NavigatorSearchCodes.GROUPS: // There is no group system yet. return []` |
| `RoomSettingsSerializer` | the `RoomBitmaskFlags.GroupData` branch writes `0` / `""` / `""` |
| `RoomPlayerAvatar` | `GroupId`, `GroupStatus`, `GroupName` fixed at `-1`, `-1`, `""` |
| `WiredConditionGroupMember`, `WiredSelectorEntitiesInGroup` | already read `avatar.GroupId`; dead until it is populated |
| `PlayerGrain.GetExtendedProfileSnapshotAsync` | `Guilds = []` |
| `NavigatorSearchType` | `GuildBases = 14`, `MyGuildBases = 19` routed by handlers to a service with no case |
| `RoomConfig.BuildersClubInGroupRooms` | written, unreachable |
| `BUILDER_AT_WORK` perk | `docs/builders-club.md` §7.8 leaves it to whoever builds groups; its refusal text is `requirement.unfulfilled.group_membership` |

## 3. Packets

### Client to server

| Packet | Payload |
| --- | --- |
| `GetGuildCreationInfo` | — |
| `GetGuildEditorData` | — |
| `GetGuildEditInfo` | groupId |
| `CreateGuild` | name, description, roomId, colourA, colourB, `int[]` badgeParts |
| `UpdateGuildIdentity` | groupId, name, description |
| `UpdateGuildBadge` | groupId, `int[]` badgeParts |
| `UpdateGuildColors` | groupId, colourA, colourB |
| `UpdateGuildSettings` | groupId, guildType, rightsLevel |
| `DeactivateGuild` | groupId |
| `GetHabboGroupDetails` | groupId, openWindow |
| `GetGuildMemberships` | — |
| `GetHabboGroupBadges` | — |
| `SelectFavouriteHabboGroup` / `DeselectFavouriteHabboGroup` | groupId |
| `JoinHabboGroup` | groupId |
| `GetGuildMembers` | groupId, pageIndex, searchText, searchType |
| `ApproveMembershipRequest` / `RejectMembershipRequest` | groupId, userId |
| `ApproveAllMembershipRequests` | groupId |
| `AddAdminRightsToMember` / `RemoveAdminRightsFromMember` | groupId, userId |
| `GetMemberGuildItemCount` | groupId, userId |
| `KickMember` | groupId, userId, targetBlocked |
| `UnblockGroupMember` | groupId, userId |
| `GetGuildFurniContextMenuInfo` | objectId, category |
| `GuildBaseSearch` | adIndex |
| `MyGuildBasesSearch` | — |
| `GetForumsList` | listCode, startIndex, 20 |
| `GetForumStats` | groupId |
| `GetThreads` | groupId, startIndex, 20 |
| `GetThread` | groupId, threadId |
| `GetMessages` | groupId, threadId, startIndex, 20 |
| `PostMessage` | groupId, threadId, subject, body |
| `UpdateThread` | groupId, threadId, isSticky, isLocked |
| `ModerateThread` | groupId, threadId, state |
| `ModerateMessage` | groupId, threadId, messageId, state |
| `UpdateForumSettings` | groupId, read, postMessage, postThread, moderate |
| `UpdateForumReadMarker` | — |
| `GetUnreadForumsCount` | — |

### Two traps

`packet-tool` names fields from the client's local variables, and gets these two wrong. Both were
checked against the SWF.

- **`UpdateGuildBadge`** is `groupId` then an `int[]` of badge parts. `packet-tool` renders the
  array's length prefix as `userId`, which makes it look like the same shape as
  `AddAdminRightsToMember`. `UpdateGuildBadgeMessageComposer.as` pushes `param1`, then
  `param2.length`, then the array.
- **`UpdateThread`** is `groupId, threadId, isSticky, isLocked`. The AS3 composer takes
  `(groupId, threadId, isLocked, isSticky)` and then writes `[p1, p2, p4, p3]`, reversing its own
  arguments. Both call sites (`ThreadListView.lockThread` / `stickThread`) pass locked first.

### Server to client

| Packet | Payload |
| --- | --- |
| `GuildCreationInfo` | costInCredits, `(roomId, roomName, hasControllers)[]`, `(partId, colorId, position)[]` |
| `GuildEditorData` | baseParts, layerParts, badgeColors, guildPrimaryColors, guildSecondaryColors — parts are `(id, fileName, maskFileName)`, colours are `(id, hexString)` |
| `GuildEditInfo` | ownedRooms, isOwner, groupId, name, description, baseRoomId, primaryColorId, secondaryColorId, guildType, guildRightsLevel, locked, url, badgeSettings, badgeCode, membershipCount |
| `GuildCreated` | baseRoomId, groupId |
| `GuildEditFailed` | reason |
| `HabboGroupDetails` | groupId, isGuild, type, name, description, badgeCode, roomId, roomName, status, totalMembers, favourite, creationDate, isOwner, isAdmin, ownerName, openDetails, membersCanDecorate, pendingMemberCount, hasBoard |
| `GroupDetailsChanged` | groupId — a nudge; the client re-requests details if it has that group open |
| `HabboGroupDeactivated` | groupId |
| `GuildMemberships` | array of `GuildInfoSnapshot` (`GuildInfoSerializer`) |
| `HabboGroupBadges` | count, then groupId/badgeCode pairs |
| `GuildMembers` | groupId, name, baseRoomId, badgeCode, totalEntries, `MemberData[]`, allowedToManage, pageSize, pageIndex, searchType, userNameFilter |
| `GroupMembershipRequested` | groupId, `MemberData` |
| `GuildMembershipUpdated` | groupId, `MemberData` |
| `GuildMembershipRejected` | groupId, userId |
| `HabboGroupJoinFailed` | reason |
| `GuildMemberMgmtFailed` | groupId, reason |
| `GuildMemberFurniCountInHQ` | userId, furniCount |
| `GuildFurniContextMenuInfo` | objectId, guildId, guildName, guildHomeRoomId, userIsMember, guildHasReadableForum |
| `FavoriteMembershipUpdate` | roomIndex, groupId, status, groupName |
| `ForumsList` | listCode, totalAmount, startIndex, `ForumBase[]` |
| `ForumData` | `ForumBase`, then read/postMessage/postThread/moderate permissions, five permission-error keys, canChangeSettings, isStaff |
| `ForumThreads` | groupId, startIndex, `ThreadData[]` |
| `ThreadMessages` | groupId, threadId, startIndex, `MessageData[]` |
| `PostThread` | groupId, `ThreadData` |
| `PostMessage` / `UpdateMessage` | groupId, threadId, `MessageData` |
| `UpdateThread` | groupId, `ThreadData` |
| `UnreadForumsCount` | count |

`MemberData` is rank, userId, userName, figure, memberSince — one shape used by `GuildMembers`,
`GroupMembershipRequested` and `GuildMembershipUpdated`, so it gets one serializer.

`ForumBase` is groupId, name, description, icon, totalThreads, leaderboardScore, totalMessages,
unreadMessages, lastMessageId, lastMessageAuthorId, lastMessageAuthorName,
lastMessageTimeAsSecondsAgo.

`ThreadData` is threadId, authorId, authorName, header, isSticky, isLocked,
creationTimeAsSecondsAgo, nMessages, nUnreadMessages, lastMessageId, lastMessageAuthorId,
lastMessageAuthorName, lastMessageTimeAsSecondsAgo, **state as a byte**, adminId, adminName,
adminOperationTimeAsSecondsAgo.

`MessageData` is messageId, messageIndex, authorId, authorName, authorFigure,
creationTimeAsSecondsAgo, messageText, **state as a byte**, adminId, adminName,
adminOperationTimeAsSecondsAgo, authorPostCount.

Times on the wire are *seconds ago*, not timestamps. The client counts up from them itself.

## 4. Enums

Per `AGENTS.md`, each of these is an enum in `Turbo.Primitives/Guilds/Enums/`, one type per file,
and the serializer does the cast. None of them becomes an `int` field with a cast at the call
site.

- `GuildType` — `Regular = 0`, `Exclusive = 1`, `Private = 2`, `Large = 3`, `Open = 4`
- `GuildMembershipStatus` — `NotMember = 0`, `Member = 1`, `Pending = 2`
- `GuildMemberRank` — `Owner = 0`, `Admin = 1`, `Member = 2`, `Requested = 3`, `Blocked = 4`
- `GuildMemberSearchType` — `All = 0`, `Admins = 1`, `Pending = 2`, `Blocked = 3`. The client
  only offers Pending and Blocked when `allowedToManage`, and Blocked only when the hotel sets
  `group.blocking.enabled`; a client that asks for them anyway is answered as `All`.
- `GuildRightsLevel` — `Owner = 0`, `Admins = 1`, `Members = 2`
- `GuildForumPermission` — `Everybody = 0`, `Members = 1`, `Admins = 2`, `Owner = 3`. Read may be
  anything; postMessage may not be looser than read, postThread not looser than postMessage, and
  moderate is clamped to at least `Admins` (`ForumSettingsView.initControls` enforces the same
  ladder client-side, so the server is re-checking, not inventing).
- `GuildForumModerationState` — `Open = 0`, `Restore = 1`, `DeletedByAdmin = 10`,
  `DeletedByStaff = 20`. `GroupForumController.deleteThread` picks 10 for a group moderator and
  20 for staff; `unDelete*` always sends 1.
- `GuildForumListCode` — `Active = 0`, `Popular = 1`, `Mine = 2`

The failure codes come from the hotel's own text file, so these members are the real set rather
than a guess. Anything the client has no text for should not be sent.

`GuildJoinFailedType`

| Member | Wire | `group.joinfail.<n>` |
| --- | --- | --- |
| `GroupFull` | 0 | This Group is full |
| `TooManyGroups` | 1 | 50 groups, 400 with HC |
| `GroupClosed` | 2 | This Group is closed |
| `RequestsNotAccepted` | 3 | Cannot accept membership requests at the moment |
| `HcRequired` | 4 | no text — the client opens `HcRequiredWindowCtrl` instead |
| `TargetNotHc` | 5 | the *player being accepted* is not HC and is at 50 |
| `TargetAtMaxMemberships` | 6 | the player being accepted is at 400 |

`GuildEditFailedType`

| Member | Wire | `group.edit.fail.<n>` |
| --- | --- | --- |
| `RoomAlreadyHomeroom` | 0 | Room is already a homeroom |
| `InvalidName` | 1 | Invalid Group name |
| `HcRequired` | 2 | no text — `HcRequiredWindowCtrl` |
| `TooManyGroupsOwned` | 3 | only 100 groups total |
| `AccountLocked` | 4 | account is safety locked |

`GuildMemberMgmtFailedType` — `NoLongerMember = 0`, `AlreadyRejected = 1`, `AlreadyAccepted = 2`.
All three are the race between two admins acting on the same request, which is exactly what the
grain boundary in section 6 removes; they are still sent, because two admins can be one
reactivation apart.

## 5. Data model

Two migrations, one per ship: `AddGuildSystem` for the first four tables below, and
`AddGuildForums` for the four `guild_forum_*` ones when phase 8 starts. Entities under
`Turbo.Database/Entities/Guilds/`, and every row-to-snapshot mapping as an extension method in a
new `Turbo.Database/Extensions/GuildEntityExtensions.cs` — never `new XSnapshot { ... }` inside a
grain.

- `guilds` — name, description, badge code, primary/secondary colour **id**, type, rights level,
  owner id, `room_id`, created at. `room_id` carries a **unique** index.

  The colours are ids into `guild_colors`, not hex. The edit window preselects its palette by id,
  so the id is what has to survive a round trip; the hex is a lookup, and keeping only the id
  means retuning a palette entry repaints every group using it instead of leaving stale copies
  behind. The snapshot carries both, resolved when it is built.
- `guild_members` — guild id, player id, rank, and whether it is the player's favourite group.
  Member-since is `created_at`, so there is no column for it. Unique on (guild, player); indexed
  on player alone for "my groups", and on (guild, rank) for the member search. The rank column
  is `member_rank`, not `rank`: `rank` is reserved in MySQL 8, and the seed migrations write raw
  SQL where that would bite.
- `guild_badge_parts` — base and symbol parts (`part_type`, `part_id`, `file_name`,
  `mask_file_name`), seeded. Feeds `GuildEditorData`. `part_id` is not the row id: it is the
  number written into a badge code, so it must never be pointed at a different picture.
- `guild_colors` — all three palettes in one table, told apart by a `slot` column, seeded. The
  client reads badge, primary and secondary as three separate arrays; three tables holding the
  same two columns would only be the same table written out three times.
- `guild_forum_settings` — the four permission levels, per guild. A guild with no row has no
  forum, which is what `hasBoard` reports.
- `guild_forum_threads` — guild id, author, header, sticky, locked, state, admin id, admin acted
  at, created at, plus denormalised message count and last message pointer.
- `guild_forum_messages` — thread id, index within the thread, author, body, state, admin id,
  admin acted at, created at.
- `guild_forum_read_markers` — guild id, player id, last read message id.

### Which side owns the homeroom link

Settled: `guilds.room_id`, not `rooms.guild_id`. A room has at most one group and a group has
exactly one room, so either direction can express it, but the group is the row that is created,
edited and deleted as a unit — putting the link on `rooms` means a group delete has to reach into
a second table to stay consistent, and the homeroom is immutable anyway. `RoomGrain` resolves its
guild through `GuildDirectoryGrain`, which holds the reverse index in memory; that is one grain
call on room activation, not a query per room.

No migration should add a guild column to `rooms`. If one is ever wanted for a query's sake, it
is a denormalisation to argue for on its own terms, not the source of truth.

Note that `deleted_at` is populated on every row of every table by `TurboEntity`, so a "deleted"
guild is not distinguishable that way — deletion here is a real delete, and it has to return
furni first.

## 6. Grains

New project `Turbo.Guilds`, shaped like `Turbo.Navigator`: `GuildModule : IHostPluginModule`,
`Configuration/GuildConfig.cs`, `Grains/`. Added to `Turbo.Cloud.sln` and referenced from
`Turbo.Main.csproj`. Grain contracts live in `Turbo.Primitives/Guilds/Grains/`; implementations
are `internal sealed`, inject `ILogger<T>`, and log every caught exception.

- **`GuildGrain`**, keyed by guild id. Identity, settings, badge, colours, the roster, pending
  requests and the blocked list. This is where the single-threading earns its keep: two admins
  approving the same request, or a join racing a type change, serialise here with no lock. It
  sends its own outbound — a rank change pushes `GuildMembershipUpdated` itself rather than
  returning something for a handler to send.

  **It calls no room grain and no player grain, and this is load-bearing.** From phase 5 the
  room grain asks it for a member's rank on every controller-level check; grains are not
  reentrant, so a call in the other direction deadlocks the pair. That is why `GetViewAsync`
  answers with what the group itself knows and stops there: the homeroom's name, the owner's
  name and whether the viewer wears the badge are read beside it by the handler, the way
  `ExtendedProfileExtensions` reads a profile and its badges side by side. Anything this grain
  ever needs from a room must arrive as an argument or through the directory, never as a call.
- **`GuildDirectoryGrain`**, singleton, `[KeepAlive]`, a read-through cache in the shape of
  `BadgeDirectoryGrain`. Holds id → name/badge for `HabboGroupBadges`, the room → guild reverse
  index, and the guild-base listings the navigator asks for. Kept alive because reactivating
  means re-reading every guild while every room activation waits on it.
- **`GuildForumGrain`**, keyed by guild id. A separate grain because the forum is the heavy I/O
  half and the rule is one grain per responsibility — the group must stay responsive while
  somebody pages through ten thousand posts. Read markers and the denormalised counters follow
  the `RoomPersistenceGrain` timer-flush pattern: queue dirty, flush on `RegisterGrainTimer`,
  flush again on `OnDeactivateAsync`.
- **`PlayerGuildGrain`**, keyed by player id, living in `Turbo.Guilds/Grains/` with its contract
  in `Turbo.Primitives/Players/Grains/Guilds/`. Holds the membership list and the favourite, and
  is what the profile, the catalog's group picker, the wired editor and room entry all read; it
  also owns creating a group, because that spends credits and counts against a per-player limit
  and must not happen twice on a double click.

  It is keyed by player but does not live in `Turbo.Players`: it enforces the group system's
  limits and so needs `GuildConfig`, which `Turbo.Players` cannot see — `Turbo.Guilds` references
  it, not the other way. `CatalogPurchaseGrain` is the precedent, keyed by player and living in
  the catalog.

Independent calls across guilds (resolving the badges of everybody in a room) go through
`Task.WhenAll`; identical calls hoist out of the loop; batch deletes are one
`WHERE ... IN (...)`, not a loop of `ExecuteDeleteAsync`. Furni return on delete is a tracked
`Remove` + `SaveChangesAsync` so it is atomic with the guild row going away.

## 7. Room integration

**Which way the calls run.** The room asks the group grain for a member's rank on every
controller-level check. So the group grain never calls a room grain while the group exists, and
the room never calls the player's guild grain while that grain is the caller. Where a push is
genuinely needed in the barred direction, it goes in the handler, after the grain call returns.
Everything below keeps to that.

- `RoomGrain.GetIsGroupRoomAsync` resolves through `GuildDirectoryGrain` — the directory, not a
  group grain, because the directory calls nothing back — and holds the answer. `_guildResolved`
  is separate from `_guild` so a room that is nobody's homeroom does not re-ask on every check.
  The resolve runs **while the room loads**, not on first ask: the group is not on the room row,
  `GetSnapshotAsync` awaits nothing, and a lazy resolve meant the first caller to want the
  room's listing got one with no group in it.
  It lives in `IRoomGrain.Guild.cs` / `RoomGrain.Guild.cs`, matching the `FloorPlan` split.
- `RoomSecurityModule.GetControllerLevelAsync` fills in its commented branch: owner →
  `Owner`; guild owner or admin → `GroupAdmin`; member, when `rightsLevel == Members` →
  `GroupRights`. `CanManipulateFurniAsync`'s `canGroupDecorate` becomes that check.
  `GiveRightsToPlayerAsync`, `RemoveRightsFromPlayerAsync`, `RemoveAllRightsAsync` and
  `RemoveOwnRightsAsync` already refuse outright in a group room and stay that way — group rights
  are not per-player rights.
- A rank change, a join, a kick or a rights-level change calls
  `RefreshControllerLevelForPlayerAsync` for everyone affected, which already pushes the status
  and publishes `PlayerControllerLevelChangedEvent`.
- `RoomSettingsSerializer` sets `RoomBitmaskFlags.GroupData` when the room has a guild and writes
  the real id, name and badge code.
- `RoomPlayerAvatar` carries the favourite group's id, status and name, loaded on entry beside
  the badges. This alone makes `WiredConditionGroupMember` and `WiredSelectorEntitiesInGroup`
  work; they need no change.
- `SelectFavouriteHabboGroup` / `DeselectFavouriteHabboGroup` update `PlayerGuildGrain` and the
  room avatar, and the room grain sends `FavoriteMembershipUpdate` to the room.
- `GetHabboGroupBadges` answers with the badge of every group represented in the room — the
  avatars' favourites and any guild furni standing in it. The client asks on every room ready.
- `BUILDER_AT_WORK` can finally be granted for real; see `docs/builders-club.md` §7.8 for what it
  lifts.

## 8. Guild furni

Two logic classes, registered the way every other logic is, by the name in the definition's
`logic` column:

- `[RoomObjectLogic("guild_customized")]` — the recolouring furni. Its stuff data is a
  `StringStuffData` of five values: state, guild id, badge code, colour 1, colour 2.
  `FurnitureGuildCustomizedLogic.as` reads exactly those indices, and clicking it opens the
  context menu rather than using the furni.
- `[RoomObjectLogic("guild_forum")]` — the forum terminal; the same context menu, leading to the
  forum.

`GetGuildFurniContextMenuInfo` answers with `GuildFurniContextMenuInfo`: the object id, the
guild's id, name and homeroom, whether the viewer is a member, and whether they may read the
forum — the last two decide which entries the menu draws, and both are answered per viewer. The
room grain builds it, and asking the group grain from there is the allowed direction.

`GuildFurnitureLogicNames` lives in `Turbo.Primitives/Guilds/` rather than in the room module,
because the catalog has to recognise a guild furni before the item exists.

The fan-out that repaints a group's furni is in `GuildFurniRefreshExtensions`, beside the
handlers that trigger it, not in the group grain — the room answers a rights check by asking the
group grain, so a group grain calling out to rooms while it was still running would have the two
waiting on each other. It is the same rule that put the settings push in a handler in section 7,
and it is worth checking against any new push out of the group grain.

Buying one is an ordinary catalog purchase whose `ExtraParam` is the group id. The purchase path
validates that the buyer belongs to that group before writing the item, and the group's badge and
colours are filled in from the guild rather than stored on the item, so a group that recolours
repaints its furni everywhere. The catalog page itself is hotel data: the group details window
opens the page named `guild_custom_furni`, and the client picks the group with
`GetGuildMemberships`.

## 9. Navigator

- `NavigatorSearchCodes.GROUPS` returns the player's groups' homerooms — the `myworld_view` block
  that currently returns `[]`.
- `NavigatorSearchType.GuildBases` (14) is the hottest guild bases, reached from the group window's
  "show groups" link; `MyGuildBases` (19) is the player's own.
- `GroupNameSearch` (13) searches groups by name.

All three read their listings from `GuildDirectoryGrain` and merge with live rooms the way the
existing cases do, rather than querying per search.

## 10. Forums

A forum exists when the guild has a `guild_forum_settings` row; `hasBoard` on the group details
and `hasForum` on the membership snapshot both report that.

Permissions are evaluated per request and reported twice: as the four levels (so the settings
window can draw them) and as five error keys (so the client can explain a refusal). An empty
string means allowed; otherwise it is the suffix of `groupforum.view.error.<key>` —
`not_member`, `not_admin`, `not_owner`, `muted`, `not_citizen`, `forum_disabled`. The client
derives `canRead`, `canPostMessage`, `canPostThread`, `canModerate` and `canReport` purely from
those strings being empty, so the server must never send an empty key for something it will then
refuse.

`PostMessage` with `threadId == 0` starts a thread and is answered with `PostThread`; otherwise
it appends and is answered with `PostMessage`. Moderation never deletes a row — it sets the state
to 10 or 20 and records who did it and when, which is what the client renders as a tombstone,
and 1 restores it.

Read markers drive `unreadMessages`, `nUnreadMessages` and `UnreadForumsCount`.
`UpdateForumReadMarker` carries no payload: it marks whatever forum the player currently has
open, so the grain has to be holding that.

## 11. Config

`GuildConfig`, section `Turbo:Guilds`, bound from `appsettings.json`. Every limit below is an
option with the hotel default it ships with, read by the grain through `IOptions<GuildConfig>`;
none of them is a `const` in a grain and none is passed in by a handler. Numbers the client
chooses (a page index, a page size it hopes for) are clamped against these.

| Option | Default | Why |
| --- | --- | --- |
| `CreationCostInCredits` | 10 | sent in `GuildCreationInfo` |
| `NameMaxLength` | 30 | `MAX_NAME_LENGTH` in the editor |
| `DescriptionMaxLength` | 255 | `MAX_DESCRIPTION_LENGTH` |
| `MembershipsMax` | 50 | join failure 1 |
| `MembershipsMaxWithClub` | 400 | join failure 1 |
| `OwnedGuildsMax` | 100 | edit failure 3 |
| `RegularGuildMembersMax` | 5000 | `group.edit.settings.type.regular.help` |
| `MembersPageSize` | 14 | the server tells the client its own page size |
| `DeletionEnabled` | true | mirrors `group.deletion.enabled` |
| `DeletionMaxMembers` | 500 | mirrors `group.deletion.maximum.members` |
| `BlockingEnabled` | true | mirrors `group.blocking.enabled` |
| `ForumsEnabled` | true | `forum_disabled` |
| `ForumPageSize` | 20 | the size the client asks for |
| `ForumThreadHeaderMaxLength`, `ForumMessageMaxLength` | | |
| `CreationRequiresClub` | false | join failure 4 / edit failure 2 |

One refusal has no client text at all: failing to afford a group. The hotel publishes no
`group.edit.fail.*` entry and no notification variable for it, so `GuildCreationFailureType`
keeps it separate from `GuildEditFailedType` and the handler logs it rather than sending one of
the other four reasons, which would put the wrong sentence on screen. A hotel that wants the
player told should add a notification key and send it here.

## 12. Build order

Two ships, settled up front. **Phases 1–7 are the group system**, and that is a complete, useful
hotel on its own: groups exist, are created and managed, hand out rights in their homeroom,
recolour their furni and fill the navigator's guild blocks. It reports `hasBoard = false`
everywhere and the client simply does not draw a forum link. **Phases 8–9 are the forum**, built
and released separately afterwards. Nothing in 1–7 may be shaped around the forum arriving later
— `GuildForumGrain` is a grain of its own precisely so that adding it is additive.

1. **Foundation.** *Done.* The `Turbo.Guilds` project, `GuildConfig`, ten enums, the snapshots,
   the four entities, `AddGuildSystem`, `SeedGuildBadgeParts`, `GuildEntityExtensions` and
   `GuildDirectoryGrain`. `GuildBadgeCodes` builds the code: a base token and up to four symbol
   tokens, each `(part, colour, position)`, concatenated in layer order.

   The part and colour ids are this hotel's own. Nothing outside reads them — the editor is told
   the id-to-asset map by the server and the code is built from that same map — so any numbering
   renders, as long as an id is never repointed at a different picture once a badge has used it.
   The seeded parts are the frames of the client's `group_badge` asset bundle (29 bases, 159
   symbols), collapsed so a part drawn from two frames is one row: `file_name` is tinted with
   the layer's colour and `mask_file_name` is laid over it untinted. Symbol ids stop at 199,
   because a symbol from 100 up is written with a `t` prefix and its key minus 100, and past
   that the two digits would collide with another part.
2. **The read path.** *Done.* `GuildGrain` and `PlayerGuildGrain`, then `HabboGroupDetails`,
   `GuildMemberships` (reusing `GuildInfoSerializer`), `HabboGroupBadges` and the profile's
   `Guilds` list. Nothing can be created yet, but a group seeded by hand is visible, which makes
   every later phase testable.

   `HabboGroupBadges` answers with the viewer's own groups. That is the whole correct answer
   today rather than a stub: the client asks for the badges of the groups represented in the
   room, and no avatar advertises a group until phase 5 puts one on `RoomPlayerAvatar`. The
   handler says where the room's own groups join the list when they exist.

   Grain code here uses no `ConfigureAwait`. Orleans' `ORLEANS0014` rejects `ConfigureAwait(false)`
   in a grain, and no grain in the tree uses it; handlers and services still do.
3. **Create and edit.** *Done, less one piece.* `GetGuildCreationInfo`, `GetGuildEditorData`,
   `CreateGuild`, `GetGuildEditInfo`, the four `UpdateGuild*`, `DeactivateGuild`,
   `GuildEditFailed`, `GroupDetailsChanged`, `HabboGroupDeactivated`.

   **Deletion does not yet return homeroom furni to its owners.** Everything else about it works:
   owner-only, refused above `DeletionMaxMembers`, the group and its roster go in one
   transaction, every member's grain is told and every member gets `HabboGroupDeactivated`. The
   furni is left where it is, which is a real gap against `group.deleteconfirm.desc` — it
   promises the furniture comes back. Returning it means the room handing its items over, and
   the room grain calls into the group grain from phase 5; per `AGENTS.md` that side effect
   belongs in a service rather than in either grain. It is phase 5's to finish, and nothing
   about the current deletion has to change for it.

   `GroupDetailsChanged` goes to the actor alone, for the same reason: reaching everyone who
   might have the group open means the room.

   The group and its owner's membership are written in one `SaveChangesAsync`; two saves would
   let a group exist with nobody in it, whose owner then had no rank and so no rights in their
   own homeroom. The write sits in a `try`, and a failure after the charge refunds it — the
   wallet is another grain, so the two can never share a transaction.

   Creating a group tells its homeroom too, from the handler. The room very likely has the
   question cached as "not a group room" — the wizard is opened from inside it — and without the
   push the owner has none of the rights their own new group just gave them.

   Creation is on `PlayerGuildGrain` because it spends credits and counts against a per-player
   limit, and one player clicking twice must not make two groups — the same argument that puts
   buying on `CatalogPurchaseGrain`. That is also why that grain **moved into `Turbo.Guilds`**:
   it needs `GuildConfig`, and `Turbo.Players` cannot see it. `CatalogPurchaseGrain` is the
   precedent, keyed by player and living in the domain whose rules it enforces.
4. **Membership.** *Done.* Join and request, approve, reject, approve-all, add and remove admin,
   kick, leave, block, unblock, `GetGuildMembers` with its four search types and paging,
   `GetMemberGuildItemCount` → `GuildMemberFurniCountInHQ`, and the three failure composers. The
   membership caps read `PlayerSubscriptionGrain` for the club tier. Every guild composer in the
   tree now carries a real payload; the stubs that remain are other domains'.

   Leaving is not its own packet: the client sends `KickMember` with itself as the target, so
   `KickAsync` covers both and the difference is only which permission check applies.

   Who may do what: the **owner** alone appoints and removes admins, removes an admin, and edits
   the group; the **owner and admins** approve, reject, kick a plain member, block and unblock,
   and see the pending and blocked filters. A client that asks for a management filter without
   the right to is answered with the whole roster rather than refused, because its own dropdown
   would not have offered it.

   The roster page is the one read that queries rather than answering from the roster held in
   the grain: filtering by name needs the players' names, and those belong to the players.

   Approving somebody who is at **their own** group limit is not a management failure — the
   hotel words it as being about them (`group.joinfail.5` and `.6`), so it comes back as a
   `HabboGroupJoinFailed` rather than a `GuildMemberMgmtFailed`. `GuildMemberMgmtResultSnapshot`
   carries the two kinds separately so a handler cannot send the wrong one.

   Every membership change tells the homeroom to refresh that one player
   (`RefreshGuildRoomMemberAsync`). Rights there are the group's, so somebody standing in it when
   they joined, left or were promoted would otherwise keep what they walked in with.

   `GetMemberGuildItemCount` counts from the **live room**, not from the furniture rows — an item
   placed a moment ago has not been flushed, and the player is about to be told how much they
   stand to get back. That needed a new `IRoomGrain.GetItemCountByOwnerAsync`, and the handler
   asks the room directly, which is what keeps the group grain off the room grain.
5. **Room integration.** *Done.* Everything in section 7. The two wired group boxes started
   working without being touched, which is what the phase was for.

   Deletion now returns the homeroom's furni, closing the gap phase 3 left. The room does it, on
   one call in from the group grain, with a system context so each item goes to its owner rather
   than to whoever pressed delete. That call is the **only** one the group grain makes into a
   room, and it is safe for one reason: it happens after the group is gone from the directory, so
   the room resolves no group and cannot ask the group grain anything back.

   A settings change cannot use that route, because the group still exists and the room answers
   a rights check by asking the group grain — the two would wait on each other. So
   `UpdateGuildSettingsMessageHandler` tells the room once the grain call has returned. This is
   the `AGENTS.md` rule about side effects that call back belonging outside the grain, and it is
   the shape to copy for anything similar.
6. **Guild furni.** *Done.* The two logic classes, the context menu packets, and the purchase
   path validating `ExtraParam` against the buyer's memberships.

   The item stores **only** the group id. The badge and the two colours are looked up from the
   group when the item attaches and never written to the row — a persisted copy would go stale
   the moment the group was recoloured, in every room the furni stands in.

   A recolour reaches furni everywhere at once. Editing a group's badge or colours fans out to
   every **loaded** room, each of which repaints that group's furni and shows the result; rooms
   that are not loaded need no telling, because the furni reads whatever is current when it next
   attaches. That makes the loaded set exactly the set that can be holding the old look, and the
   room directory already knows it, so nothing dormant is woken and no index has to be kept in
   step. A room holding none of that group's furni answers after one dictionary scan, which is
   the usual case.

   Deleting a group fans out too: its furni standing in other rooms did not go back with the
   homeroom's, and would otherwise keep wearing a badge that no longer resolves.

   The homeroom keeps its **own** copy of the group's summary, which its navigator listing draws
   the name and badge from, and the furni fan-out does not touch it — the furni reads the
   directory. So all four `UpdateGuild*` handlers refresh that copy as well; without it a rename
   left the room advertising the old name for as long as it stayed loaded.

   `guild_forum` is `guild_customized` with a different logic name. The client picks its menu
   entries from the furni's own class name, not from anything the server sends, so there is
   nothing else to differ.
7. **Navigator.** *Done.* The searches in section 9: the `groups` block of the player's own
   world, `MyGuildBases` (the same block, reached the legacy way), `GuildBases` (the hotel's
   biggest groups, which the group window's "show groups" link opens) and `GroupNameSearch`.

   All four answer from the guild directory and the player's own guild grain rather than from a
   query, and hand their room ids to the same `GetRoomsInOrderAsync` the favourites and history
   blocks use — so live population, visibility and ordering work the way they already did.
   **End of the first ship.**
8. **Forums.** Settings and the permission ladder, threads and messages, sticky and lock,
   moderation states, read markers, `UnreadForumsCount`, the three list codes, `GetForumStats`.
   Roughly a third of the total, and the second ship. `hasBoard` and `hasForum` start telling the
   truth here; until then they are honestly `false`, not stubbed.
9. **The edges.** `CallForHelpFromForumMessage` / `CallForHelpFromForumThread`,
   `Game2GetTotalGroupLeaderboard` / `Game2GetWeeklyGroupLeaderboard`, and group entries in the
   badge leaderboard.

## Hotel data, not code

These are served to the client by the hotel, not by this server, and the feature looks broken
without them:

- `group.badge.url` and `group_logo_url_template` — the imager. No badge renders without it.
- `groupRoomInfo.enabled`, `groupRoomInfo.badge.enabled`, `groupRoomInfo.attach.enabled` — the
  badge on the room info panel.
- `group.deletion.enabled`, `group.deletion.maximum.members`, `group.blocking.enabled`,
  `groupMembers.enabled` — client-side gates whose server-side twins are in `GuildConfig`.
- `group.homepage.url`, `link.format.guild` — the external group page.
- `groupforum.poll.period`.
- A catalog page named `guild_custom_furni`, and furni definitions whose `logic` is
  `guild_customized` and `guild_forum`.

## Validation

```bash
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate
```
