# Perks the client knows

The `PerkAllowances` channel, read from the Flash client
(`D:\Habbo\WIN63-202609091217-117204808\scripts-deob`) on 2026-09-20. Companion to
`docs/permissions-client-gates.md`, which covers security levels and room controller levels, and
to `docs/permissions.md`, which is the plan both feed.

## How the channel works

`PerkAllowances` carries a list of `{ code: string, isAllowed: boolean, errorMessage: string }`.
`PerkManager` keeps them in a dictionary keyed by code:

```actionscript
public function isPerkAllowed(param1:String) : Boolean
{
   return param1 in §_-J1r§ && §_-J1r§[param1].isAllowed;
}
```

Four properties of that follow, and each one matters:

1. **A code the server never sends is denied.** `param1 in dict` is false, so an unknown perk
   fails closed. The server does not have to send a perk it wants withheld — though sending it
   with `isAllowed = false` is what carries the refusal text.
2. **Perks accumulate rather than replace.** `onPerkAllowances` merges each perk into the
   dictionary, so a later `PerkAllowances` carrying one perk updates that one and leaves the rest
   alone. The server can change a single perk mid-session.
3. **The client reacts live.** Each update dispatches `PerksUpdatedEvent`; `RoomUI` re-applies
   `MOUSE_ZOOM` on it. Perks are not login-only.
4. **`errorMessage` is dead in this client.** `getPerkErrorMessage` is declared on
   `ISessionDataManager`, implemented, and **called from nowhere**. The strings the server sends
   (`requirement.unfulfilled.helper_level_6` and friends) are carried and discarded. Keep sending
   them — a later client may read them — but do not expect a player to see one.

## The nine perks the client actually reads

Only these appear as `isPerkAllowed("...")`. Each is listed with what it gates and what else has
to be true.

| Code | Gates | Also needs |
| --- | --- | --- |
| `CAMERA` | The camera button on the bottom toolbar; `CameraWidgetHandler.sendInitCameraMessage`, which is what asks the server for camera config at all; the `:camera` chat command | toolbar button needs `camera.launch.ui.position == "bottom-icons"` |
| `USE_GUIDE_TOOL` | The guide entry in the Me menu (old and new); `GuideSessionController` going on duty, and it is re-checked while on duty — losing the perk drops the session | `guides.enabled` |
| `JUDGE_CHAT_REVIEWS` | The guide tool's chat-review duty | — |
| `CITIZEN` | Which talent track the player is shown: **not** having `CITIZEN` puts them on `citizenship`, having it puts them on `helper` | `talent.track.citizenship.enabled` |
| `MOUSE_ZOOM` | Mouse-wheel zoom in the room, applied through `RoomDesktopMouseZoomEnableEvent` | — |
| `BUILDER_AT_WORK` | Floor plans larger than `(width - 1) * (height - 1) > 3025`; `FloorPlanCache` refuses to draw past it without the perk | the 64-per-axis limit still applies |
| `NAVIGATOR_ROOM_THUMBNAIL_CAMERA` | Room thumbnails: the camera button in the in-room info view and the room info popup, and whether search results may use thumbnail view modes at all — without it `BlockResultsView` forces view mode 0 outside `official_view` | — |
| `NAVIGATOR_PHASE_TWO_2014` | Which navigator the client runs. Drives `HabboNavigator` and `HabboNewNavigator` | — |
| `NAVIGATOR_PHASE_ONE_2014` | The phase-one navigator paths: `MainViewCtrl`, `OfficialRoomEntryManager`, and a comparison inside `HabboNavigator` | — |

Note the inversion on `CITIZEN`: it is the only perk where *absence* selects a feature rather
than hiding one.

Note also that `CAMERA` is the one perk that gates an **outgoing packet** rather than a button.
Without it the client never sends the camera init, so the server's camera handlers are never
reached — which is consistent with them all being no-ops today.

## The full table the client declares, and what it does not read

`com/sulake/habbo/communication/enum/perk/§_-p1m§.as` declares fifteen codes. **Nothing
references that class** — no `§_-p1m§.` usage anywhere — so it is a reference table and the nine
string literals above are the live set. The six it declares and never reads:

`GIVE_GUIDE_TOURS`, `VOTE_IN_COMPETITIONS`, `CALL_ON_HELPERS`, `TRADE`, `HEIGHTMAP_EDITOR_BETA`,
`HABBO_CLUB_OFFER_BETA`.

`TRADE` is worth singling out: the server sends it and the Flash client ignores it, so **trading
is not perk-gated client-side**. Whatever gates trading has to be enforced on the server. Same
for `CALL_ON_HELPERS` and `VOTE_IN_COMPETITIONS`.

`HEIGHTMAP_EDITOR_BETA` is a dead code in this revision — the floor plan editor is gated on
Builders Club and `hasSecurity(4)` instead (see `docs/builders-club.md` §7).

## Against what the server has

`PlayerPerkFlags` declares thirteen, with full legacy-string mapping both ways, and
`SSOTicketMessageHandler` sends twelve of them as hardcoded literals.

| | Client declares | Client reads | Server `PlayerPerkFlags` | Server sends at login |
| --- | --- | --- | --- | --- |
| `CAMERA` | yes | **yes** | yes | `true` |
| `USE_GUIDE_TOOL` | yes | **yes** | yes | `false` |
| `JUDGE_CHAT_REVIEWS` | yes | **yes** | yes | `false` |
| `CITIZEN` | yes | **yes** | yes | `true` |
| `MOUSE_ZOOM` | yes | **yes** | yes | `true` |
| `BUILDER_AT_WORK` | yes | **yes** | yes | `false` |
| `NAVIGATOR_ROOM_THUMBNAIL_CAMERA` | yes | **yes** | yes | `true` |
| `NAVIGATOR_PHASE_TWO_2014` | yes | **yes** | yes | `true` |
| `NAVIGATOR_PHASE_ONE_2014` | yes | **yes** | **missing** | not sent |
| `TRADE` | yes | no | yes | `true` |
| `VOTE_IN_COMPETITIONS` | yes | no | yes | `false` |
| `CALL_ON_HELPERS` | yes | no | yes | `true` |
| `HABBO_CLUB_OFFER_BETA` | yes | no | yes | `true` |
| `GIVE_GUIDE_TOURS` | yes | no | **missing** | not sent |
| `HEIGHTMAP_EDITOR_BETA` | yes | no | **missing** | not sent |
| `UNITY_TRADE` | **not declared** | no | yes | not sent |

Three things fall out of that:

- **`NAVIGATOR_PHASE_ONE_2014` is missing from `PlayerPerkFlags` and the client reads it.** It is
  the only read perk the server cannot express. Whether that matters depends on which navigator
  the hotel wants; today the server sends `NAVIGATOR_PHASE_TWO_2014 = true`, so phase one is off
  and its absence is consistent. Worth adding the flag so the choice is expressible rather than
  accidental.
- **`UNITY_TRADE` is in the server enum and in no client table.** It is a nitro/Unity-era code.
  Harmless, and not sent.
- **`GIVE_GUIDE_TOURS` and `HEIGHTMAP_EDITOR_BETA` are missing from the server enum**, and the
  client does not read either, so neither is a gap today.

`PlayerLiveState.Perks` is loaded from `players.perk_flags` on activation and **never read**: the
SSO handler builds its list from literals instead. So the column exists, the mapping exists, and
nothing joins the two. That join is step 3 of `docs/permissions.md`.

## What this means for the permission plan

1. **Perks are a projection, and a lossy one.** Only nine of thirteen server flags reach a
   decision in this client, and one decision the client makes (`NAVIGATOR_PHASE_ONE_2014`) has no
   server flag. So a `PermissionType` → `PlayerPerkFlags` map is the right shape, but it is not a
   bijection and should not be written as one.
2. **Fail-closed is already the client's behaviour**, so the projection can send only the perks a
   player holds and omit the rest. Sending the denials as well costs nothing and carries the
   refusal text, which is what the hardcoded block does today — but since `errorMessage` is never
   read by this client, that is style rather than function.
3. **Perks change live**, which the plan's "directory → presence → player" reload path already
   supports: re-sending `PerkAllowances` with the changed perks is enough, and the client merges.
4. **Four perks gate nothing client-side** (`TRADE`, `CALL_ON_HELPERS`, `VOTE_IN_COMPETITIONS`,
   `HABBO_CLUB_OFFER_BETA`). If the hotel wants those gated, the gate is server-side and belongs
   in the `PermissionType` catalogue, not in the perk list. `TRADE` is the live one: the server
   currently sends it `true` for everyone and nothing anywhere enforces it.
5. **`BUILDER_AT_WORK` is confirmed as the large-floor-plan perk** and nothing else, which
   settles the open question in `docs/builders-club.md` §7.8: the server already enforces the same
   `3025` limit that `FloorPlanCache` does, so granting the perk and granting the permission have
   to happen together or the client and server will disagree about what may be drawn.

## Caveats

- Flash only. nitro-next may read perks this client ignores; where they differ, `AGENTS.md` names
  Flash as the authority.
- Config flags sit in front of several perks (`guides.enabled`, `talent.track.citizenship.enabled`,
  `camera.launch.ui.position`). A perk can be allowed and the feature still hidden, so a hotel
  that grants a perk and sees nothing should check `ExternalVariables.json` before the server.
