# Reward tracks

The Progression menu's reward track (AS3 `quest/rewardtrack`). The server sends the tracks at
login (`RewardTracks`, 2614), a task's count and the track's points when they change
(`RewardTrackProgress`, 844), and answers a prize claim (`ClaimRewardTrackPrize` 2885 →
`RewardTrackClaimResult` 3381, codes 0-8) and a premium purchase (`PurchaseRewardTrackPremium`
2717 → `RewardTrackPremiumPurchaseResult` 917, codes 0-9). The codes' meanings are the hotel
texts `reward_track.claim.notification.fail.<code>` and `reward_track.premium.notification.fail.<code>`.

## Tracks are in the database

The tracks are kept in `reward_tracks` (id, theme, on/off, start and end dates, order, premium:
boost, instant points, credit and diamond costs), `reward_track_tasks` (id, action type, parameter,
premium, order), `reward_track_task_levels` (count, points, premium) and `reward_track_prizes`
(id, points, product type, reward type id, extra params, amount, premium, furni definition, name,
order). The migration that makes them puts in Habbo's two tracks as its client was sent them at
login on 2026-10-18 (build WIN63-202610151745): Introduction (blue, 30 tasks, 61 levels, 49
prizes, premium 100 credits / 100 diamonds, boost 1.2) and Habboween 2026 (red, 17 tasks, 31
levels, 60 prizes from 100 to 1650 points, premium 75 / 75, boost 1.2). A track listed under the
old `Turbo:RewardTracks:Tracks` that the database lacks is moved in when the server starts (a
warning says so; remove it from the configuration then). `Turbo:RewardTracks` keeps `Enabled`,
`PetPrizeName` and `BotPrizeName`. Progress is kept per player and track in
`player_reward_tracks`, by track id, so it outlives a track removed and added again.

Habbo's protocol has no dates: the client has a track only from the list sent at login. A track
is sent while it is on and within its dates; one that has ended counts and gives nothing more.
A change made in the panel is sent to everyone online at once with `reload` set (AS3
`RewardTrackController.onRewardTracks`: the window is rebuilt and, when open, says "Reward Track
updated"); a start or end date passing is seen at the next login.

## Panel API

Every route needs `admin.rewardtracks.manage`. Every change is written to the command log as
`rewardtracks <save|delete|import|reload> <track ids>` from `panel`, and sends the tracks again
to the players online.

| Route | Does |
| --- | --- |
| `GET /api/reward-tracks` | `{ tracks: [{ id, theme, enabled, startsAt, endsAt, live, hasPremium, tasks, levels, prizes, maxPoints }], actionTypes }`; `actionTypes` are the types the server counts |
| `GET /api/reward-tracks/{id}` | `{ track, warnings }`: the whole track (below) and what this hotel cannot do of it |
| `PUT /api/reward-tracks/{id}` | saves the track in the body (added, or replaced whole); 400 with the reason when it is invalid |
| `DELETE /api/reward-tracks/{id}` | removes it (204, or 404) |
| `POST /api/reward-tracks/import/preview` | the plan of an import, nothing changed |
| `POST /api/reward-tracks/import` | makes it: every added or changed track saved in one transaction |
| `POST /api/reward-tracks/reload` | reads the tables again (after an edit by hand) |

A track, as `GET` returns it and `PUT` takes it (`productType` is the client's product item type:
0 wall, 1 floor, 2 effect, 4 badge, 6 bot, 8 activity points, 9 chat style, 10 pet, 12 Habbicon):

```json
{
    "id": "habboween2026", "enabled": true, "startsAt": null, "endsAt": "2026-11-02T00:00:00Z", "theme": "red",
    "premium": { "taskPointsBoost": 1.2, "instantPoints": 0, "costCredits": 75, "costDiamonds": 75 },
    "tasks": [
        { "id": "pet_visit", "actionType": "PET_OTHER_USERS_ROOM", "parameter": "", "premium": false,
          "levels": [ { "requiredCount": 1, "points": 20, "premium": false } ] }
    ],
    "prizes": [
        { "id": "free30", "requiredPoints": 1650, "productType": 4, "rewardTypeId": "DAD18", "extraParams": "",
          "amount": 1, "premium": false, "furnitureDefinitionId": null, "name": "" }
    ]
}
```

An import takes the `RewardTracks` message as its client read it, `{ "capture": { "tracks": [...] },
"only": ["habboween2026"] }` (`only` optional): each track with `id`, `theme`, `hasPremiumConfig`,
`taskPointsBoost`, `instantPoints`, `costDiamonds`, `costCredits`, `tasks` (`id`, `actionType`,
`parameter`, `premium`, `levels`: `requiredCount`, `pointsReward`, `premium`) and `prizes` (`id`,
`requiredPoints`, `productItemTypeId`, `rewardTypeId`, `extraParams`, `rewardAmount`, `premium`); a
player's progress in it is not read. The plan lists per track `action` (added, changed, unchanged),
`premiumChanged`, `themeChanged`, the task and prize ids added, changed and removed, and
`warnings`: a task type the server does not count, a `CUSTOM_WIRED_ACTION` task (counted only by a
wired Progress Reward Track box naming it), a prize it cannot give (a furni sprite this hotel has
no furni for, a product type it cannot give). A changed track keeps the hotel's own choices: on or
off, its dates, and the furni definition and name of each prize it still has.

## Rules, from the AS3 RewardTrack classes

- A task's count crossing a level's count gives that level's points. A premium level gives them
  only to a premium owner; a premium task counts only for one. A task stops counting at its last
  level, where the client shows it complete.
- A premium owner's task points are multiplied by `TaskPointsBoost`; the client shows it as
  "(boost - 1) x 100 % faster progression".
- Buying premium debits `CostCredits` and `CostDiamonds`, gives `InstantPoints` and the points
  of the premium levels the player already passed (inference: they would otherwise be lost).
- A prize is claimable when it is not premium-locked and the points reach it. Activity points,
  badges, furni (`FurnitureDefinitionId` or the sprite in `RewardTypeId`, `Amount` items),
  Habbicons (`RewardTypeId` the Habbicon id, one of the hotel's Habbicon collections), pets
  (`ExtraParams` the figure "type palette colour", a palette of the hotel's pet breeds) and
  `GENERIC` bots (`ExtraParams` the look) can be given; anything else is refused with code 7.
  A pet or bot is called `Name`, else `PetPrizeName` / `BotPrizeName` (Habbo's prizes carry no
  name; "Pet" and "Bot" by default). Habbo's Introduction track ships with all 49 prizes.

## What counts

The task images the official client carries name thirty action types
(`reward_track_tasks_<type>`, `RewardTrackActionTypes`). The server counts those it already
records an achievement fact for (`RewardTrackFactListener`): `enter_other_users_room` (once
per room), `switch_item_state`, `change_figure`, `change_motto`, `give_respect`,
`pet_respect`, `pet_level`, `pet_eat` and `wear_badge` (a badge put on). From room events
(`RewardTrackRoomListener`, a player's own doing only, not wired's or a bot's) it counts `wave`,
`dance` (starting one), `place_item`, `place_builders_club_furni` (a Builders Club furni put
down, instead of `place_item`), `move_item` (a furni put on another tile), `rotate_item` (a floor
furni turned in place; `RoomItemMovedEvent.TileChanged`) and `chat_with_someone` (a chat line that
was not cancelled). Furni logic counts `find_hand_item` (a vending machine's drink handed over),
`teleport` (a teleport that sends its occupant on), `swim` (water putting a swimming effect on, 29,
30 or 185 as `AvatarInfoWidget.isSwimming` reads them) and `friend_furni_locked` (a love lock
both locked, for both). The messenger service counts `request_friend` (a request sent),
`send_messenger_message`, `send_messenger_invite` (once per invite that reached someone),
`follow_friend` (a follow that went through) and `set_relationship_status` (one set, not
cleared); the player grain `replenish_respect` (a replenish used); publishing a photo
`publish_picture`; the catalogue purchase grain `buy_from_catalogue` once a purchase landed; the
navigator service `create_room` once a room was created; and the Habbicon grain `use_habbicon`.
Habboween 2026 adds five more: `pet_other_users_room` (a player's own pet, not a monsterplant,
put down in a room someone else owns: "Take your pet to visit a friend"), `plant_monster_plant` (a
seed planted), `treat_monster_plant` (the plant menu's Treat), `breed_monster_plant` (a plant
breeding, for each of its two owners) and `custom_wired_action`, which no player action is: its
nine campaign tasks move only by the wired Progress Reward Track action below. What each type
counts is inference from the image's name and the task's hotel texts
(`reward_track.<track>.task.<id>.desc`); the official client only names them. All thirty-five are
counted.

## Wired

- Progress Reward Track (action 58, `wf_act_progress_reward_track`): string "track id<TAB>task
  id", ints add-to-existing (1) or set (0), number or variable, the number, the variable's
  target. The selected users' task count moves to the score, or on by it, forward only and up
  to the task's last level, each level passed giving its points as the player's own actions do.
- Reset Reward Track (action 59, `wf_act_reset_reward_track`): string the track id. The
  selected users' counts, points and claimed prizes go (a bought premium stays, inference) and
  the tracks are sent again with `reload` set, which rebuilds the client's window and shows
  "Reward Track updated" if it is open (AS3 `RewardTrackController.onRewardTracks`).

Habbo's furnidata has no furni for either box yet, so their class names are Turbo's.
