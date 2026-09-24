# What the client gates, and on what

Every place the Flash client asks about a player's security level or their control over a room,
read from `D:\Habbo\WIN63-202609091217-117204808\scripts-deob` on 2026-09-20. This is the
expectation the server has to satisfy; `docs/permissions.md` is the plan that fits it.

Read this before designing anything, because two of the findings change the design.

## The two findings that matter

**1. `isAnyRoomController` is not a separate flag. It is `securityLevel >= 5`.**

```actionscript
public function get isAnyRoomController() : Boolean { return §_-f1t§ >= 5; }
public function hasSecurity(param1:int) : Boolean  { return §_-f1t§ >= param1; }
```

Both read the same field, set once from `UserRights.securityLevel`. So "controls every room in the
hotel" is not something the client can be told separately — it *is* security level 5, and it
appears in seventeen places as `isAnyRoomController`, usually beside `roomControllerLevel >= 1`.
A server that wants a staff member to control every room has to send them `securityLevel >= 5`;
there is no other channel. That collapses the `AnyRoomOwner` and `AnyRoomRights` permissions in
`docs/permissions.md` §6 into one thing the projection must get right, rather than two
independent permissions.

**2. `UserRights` is re-readable, and `topSecurityLevel` is a high-water mark.**

```actionscript
§_-f1t§ = _loc2_.securityLevel;                       // replaced
§_-db§  = Math.max(§_-db§, _loc2_.securityLevel);     // never lowered
```

So sending `UserRights` again lowers `hasSecurity` live — a rank change does not need a
reconnect. But `topSecurityLevel` only ever rises, and the client sends it back to the server in
`RenderRoomMessageComposer` and `RenderRoomThumbnailMessageComposer` (mixed into a `status`
field). It is an anti-tamper breadcrumb for the camera, not a permission. Do not treat a
`topSecurityLevel` the client reports as authority for anything.

## Security levels: the complete table

`hasSecurity(n)` is `securityLevel >= n`, so a level inherits everything below it. Levels the
client actually asks for are **2, 4, 5 and 7** — and they line up with `SecurityLevelType` as it
already stands (`Partner = 2`, `Employee = 4`, `Moderator = 5`, `Community = 7`).

### Level 2 — `Partner`

| Gated | Where | Passing it |
| --- | --- | --- |
| `:furni` chat command (the furni chooser) | `ChatInputWidgetHandler:322` | `roomControllerLevel >= 1` **or** security 2 **or** `isAmbassador` |

### Level 4 — `Employee`

| Gated | Where | Passing it |
| --- | --- | --- |
| `:kick` and `:mute` go to the **server** instead of being handled locally | `ChatInputWidgetHandler:247,262` | Without security 4 the client kicks/mutes by itself and needs `roomControllerLevel >= 1`. With it, the command text falls through to the server's own parser. |
| `:uc` — classify room or hotel users | `:404` | security 4 only |
| `:anew`, `:avisit`, `:aalert` — ambassador tools | `:417,423,436` | `isAmbassador` **or** security 4 |
| `:csmm` — force a checksum mismatch | `:237` | security 4 only |
| Saving a floor plan, and its import/export dialog | `BCFloorPlanEditor:189,248`, `ImportExportDialog:36` | Builders Club seconds left **or** security 4 |
| The wired menu | `WiredMenuController:289` | security 4 |
| Wired variable-fx campaign icons | `VariableFxVisualizationSettingsPreset:91` | a config flag **or** security 4 |
| Guild forum selector in the catalog | `GuildForumSelectorCatalogWidget:20` | security 4 |
| Staff options in room create | `RoomCreateViewCtrl:353` | security 4 |
| The "door mode overridden" notice is **hidden** for staff | `RoomSettingsCtrl:726` | shown when `hiddenByBc && !hasSecurity(4)` |
| Picking up somebody else's furni from the info stand | `handler/§_-J2H§:132` | owns the furni **or** security 4 |
| An info stand detail | `InfoStandFurniView:467` | security 4 |
| Chat input styles | `RoomChatInputView:371` | security 4 |
| Snow war / game manager debug | `SnowWarUI:94`, `HabboGameManager:149` | security 4 |
| Reward track | `RewardTrackController:164` | security 4 **or** 5 |

### Level 5 — `Moderator`

| Gated | Where | Passing it |
| --- | --- | --- |
| **Control of every room** (`isAnyRoomController`) | seventeen sites; see the next section | security 5 |
| The moderation tool | `ModerationManager:191` | security 5 |
| Opening the Builders Club catalog without a membership | `HabboCatalog:1635` | `builders.club.enabled` **or** security 5 |
| A purchase confirmation path | `PurchaseConfirmationDialog:835` | security 5 |
| `:reload` and `:rollback` | `ChatInputWidgetHandler:344,350` | `roomControllerLevel >= 4` **or** security 5 |
| Deleting any guild | `GroupDetailsCtrl:127` | owns it **or** security 5 |
| Entering any room from the navigator | `NavigatorData:90` | a flag **or** security 5 |
| Cancelling anyone's rentable space | `RentableSpaceDisplayWidget:141` | owns the furni **or** security 5 |
| An info stand action | `InfoStandWidgetHandler:1553`, `handler/§_-E1f§:77` | security 5 |
| An info stand furni detail | `InfoStandFurniView:119` | shown when **not** security 5 |

### Level 7 — `Community`

| Gated | Where | Passing it |
| --- | --- | --- |
| Staff-only navigator flat categories, in room create and in enforce-category | `RoomCreateViewCtrl:188`, `EnforceCategoryCtrl:52` | a category that is not `staffOnly`, **or** security 7 |

This is the one the server already half-has: `NavigatorFlatCategoryEntity.StaffOnly` and `MinRank`
exist, and `NavigatorService` filters `!x.StaffOnly && x.MinRank <= 1` with a comment saying the
1 is hardcoded for want of a rank.

### What `isAmbassador` gates

Separate boolean in `UserRights`, not a level. It substitutes for security 4 on `:anew`,
`:avisit` and `:aalert`, and adds the ambassador mute options to the info stand
(`InfoStandWidgetHandler` — `RWUAM_AMBASSADOR_MUTE_*`). Nothing else.

## Room controller level: the complete table

`RoomSession.roomControllerLevel` accepts **0 to 5** and logs an "Invalid roomControllerLevel"
warning and falls back to 0 for anything else — so the server must stay inside that range. The
server's `RoomControllerType` already matches: `None 0, Rights 1, GroupRights 2, GroupAdmin 3,
Owner 4, Moderator 5`.

It is also **suppressed entirely in play-test mode**: the getter returns 0 when `_playTestMode`
is set, so a wired play-test session has no room rights by design.

| Level | Gated | Where |
| --- | --- | --- |
| `>= 1` | The floor plan editor button; furni move/rotate/pickup buttons; the dimmer, background-colour, area-hide, clothing-change, mannequin, playlist and jukebox widgets; the user chooser when the room disables it; `:kick` / `:mute` handled locally; the pet menu; hover cursors over furni; `:chooser` | `RoomInfoViewCtrl:268`, `RoomDesktop:1348,2159`, `FurnitureDimmerWidgetHandler:103`, `FurnitureBackgroundColorWidgetHandler:105`, `FurnitureAreaHideWidgetHandler:122`, `FurnitureClothingChangeWidgetHandler:83`, `MannequinWidget:129`, `PlayListEditorWidgetHandler:135`, `PetMenuView:102`, `RoomEngine:814`, `InfoStandFurniView:635,644`, `ChatInputWidgetHandler:249,264,315` |
| `== 1` exactly | "You have rights here" navigator states — deliberately *not* the owner | `HabboNavigator:415` (`== 1 && !isRoomOwner`), `RoomEventInfoCtrl:67` |
| `>= 2` | Dragging catalog furni into a **guild** room | `HabboCatalog:2834` (`isRoomOwner \|\| isGuildRoom && level >= 2`) |
| `>= 3` | Borrowing Builders Club furni; `:floor` opening the editor; pickup mode 1 on the info stand | `HabboCatalog:2864`, `ChatInputWidgetHandler:395`, `InfoStandFurniView:718` |
| `>= 4` | `:reload` / `:rollback`; the external-image widget's owner controls | `ChatInputWidgetHandler:344,350`, `ExternalImageWidgetHandler:107` |
| `== 5` exactly | The external-image widget's sender-name field | `ExternalImageWidget:145` |

Two shapes worth noticing. `== 1` is used to mean "has rights but is not the owner", so a server
that reported 1 for an owner would break the navigator's wording. And `== 5` means the server has
to send exactly `Moderator`, not merely "at least 4", for that one field to render.

## What this tells us about the design

1. **`AnyRoomOwner` and `AnyRoomRights` are not two permissions.** They are one — "controls every
   room" — and its only client channel is `securityLevel >= 5`. So a rank that grants it must
   carry security level 5 or the client and server will disagree about the same player.
2. **The level is a threshold, so a rank's permissions and its level can contradict each other.**
   A rank with `ModerateAnyRoom` but security level 4 would let the server accept a kick the
   client never offers a button for. The projection needs a rule — the safest is that a
   permission which the client gates on a level *implies* that level, and the rank's configured
   level is a floor, not a ceiling. Worth deciding explicitly.
3. **`roomControllerLevel` is a single number with exact-match readers**, so it cannot carry a
   permission set. Room-scoped staff power has to arrive as `Moderator = 5`, which is what
   `RoomSecurityModule.GetControllerLevelAsync` should return for a player holding the
   control-every-room permission. It already has `RoomControllerType.Moderator` defined and
   unused.
4. **Rights can change live.** Re-sending `UserRights` is enough, so a rank edit does not need
   the player to reconnect — which the plan's "directory → presence → player" reload path already
   assumed.
5. **Four permissions in the plan are client-invisible**: `StealFurni`, `EnterFullRoom`,
   `EnterHiddenRoom`, `LargeFloorPlans`. The client offers no button and asks no question, so
   these are server-only and their gate is the only thing that enforces them.
6. **Nothing the client asks needs a per-player permission list.** It needs one number, one
   boolean (`isAmbassador`) and one perk list. Per-player grants and revokes are therefore
   entirely a server-side concept whose only visible effect is the level and perks they resolve
   to — which is an argument for keeping the resolved set off the wire entirely.

## Caveats

- This is the **Flash** client, which is what `AGENTS.md` names as the authority. nitro-next may
  gate differently or not at all; where the two disagree, the server should satisfy Flash.
- Obfuscated file names (`handler/§_-E1f§.as`, `handler/§_-J2H§.as`, `handler/§_-N2v§.as`) are
  quoted as-is; the surrounding code says what they do, the class names do not.
- A handful of level-4 sites are debug or campaign tooling (`:csmm`, snow war, variable-fx icons)
  that this server has no equivalent for. They are listed for completeness, not as work.
