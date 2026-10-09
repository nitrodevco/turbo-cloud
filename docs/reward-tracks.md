# Reward tracks

The Progression menu's reward track (AS3 `quest/rewardtrack`). The server sends the tracks at
login (`RewardTracks`, 2614), a task's count and the track's points when they change
(`RewardTrackProgress`, 844), and answers a prize claim (`ClaimRewardTrackPrize` 2885 →
`RewardTrackClaimResult` 3381, codes 0-8) and a premium purchase (`PurchaseRewardTrackPremium`
2717 → `RewardTrackPremiumPurchaseResult` 917, codes 0-9). The codes' meanings are the hotel
texts `reward_track.claim.notification.fail.<code>` and `reward_track.premium.notification.fail.<code>`.

## Tracks are configuration

Habbo's Introduction track is not shown in full anywhere (30 tasks, 49 prizes), so the server
ships with no track. A hotel lists its tracks under `Turbo:RewardTracks:Tracks`; the server
refuses to start on a track with repeated ids, levels that do not count up from 1, a premium
boost below 1, or negative points, costs or amounts. Progress is kept per player and track in
`player_reward_tracks`.

```json
"RewardTracks": {
    "Enabled": true,
    "Tracks": [
        {
            "Id": "introduction",
            "Theme": "",
            "Premium": { "TaskPointsBoost": 1.5, "InstantPoints": 100, "CostCredits": 0, "CostDiamonds": 50 },
            "Tasks": [
                {
                    "Id": "visit_rooms",
                    "ActionType": "enter_other_users_room",
                    "Levels": [
                        { "RequiredCount": 1, "Points": 10 },
                        { "RequiredCount": 5, "Points": 20 },
                        { "RequiredCount": 20, "Points": 30 }
                    ]
                }
            ],
            "Prizes": [
                { "Id": "duckets_300", "RequiredPoints": 300, "ProductType": "ActivityPoints", "RewardTypeId": "0", "Amount": 50 },
                { "Id": "badge_300", "RequiredPoints": 300, "ProductType": "Badge", "RewardTypeId": "ABC01", "Premium": true },
                { "Id": "furni_500", "RequiredPoints": 500, "ProductType": "FloorItem", "RewardTypeId": "<sprite id>", "FurnitureDefinitionId": 123, "Amount": 10 }
            ]
        }
    ]
}
```

What the official client shows (2026-10-09 captures `introduction.png`, `rt-dragged.png`):
- the track id `introduction` (texts `reward_track.introduction.*`), 30 tasks and 49 prizes;
- "Visit rooms" with levels 1, 5 and 20 for 10, 20 and 30 points, which the example uses;
- prizes at 300, 350, 400, 450 and 500 points, free and premium, some of them x10. Which furni
  they are is not readable, so none is seeded.

## Rules, from the AS3 RewardTrack classes

- A task's count crossing a level's count gives that level's points. A premium level gives them
  only to a premium owner; a premium task counts only for one. A task stops counting at its last
  level, where the client shows it complete.
- A premium owner's task points are multiplied by `TaskPointsBoost`; the client shows it as
  "(boost - 1) x 100 % faster progression".
- Buying premium debits `CostCredits` and `CostDiamonds`, gives `InstantPoints` and the points
  of the premium levels the player already passed (inference: they would otherwise be lost).
- A prize is claimable when it is not premium-locked and the points reach it. Activity points,
  badges and furni (`FurnitureDefinitionId`, `Amount` items) can be given; anything else is
  refused with code 7.

## What counts

The task images the official client carries name thirty action types
(`reward_track_tasks_<type>`, `RewardTrackActionTypes`). The server counts those it already
records an achievement fact for (`RewardTrackFactListener`): `enter_other_users_room` (once
per room), `switch_item_state`, `change_figure`, `change_motto`, `give_respect`,
`pet_respect`, `pet_level`, `pet_eat` and `wear_badge` (a badge put on). From room events
(`RewardTrackRoomListener`, a player's own doing only, not wired's or a bot's) it counts `wave`,
`dance` (starting one), `place_item`, `move_item` (a furni put on another tile), `rotate_item`
(a floor furni turned in place; `RoomItemMovedEvent.TileChanged`) and `chat_with_someone` (a chat
line that was not cancelled). The catalogue purchase grain counts `buy_from_catalogue` once a
purchase landed and the navigator service `create_room` once a room was created. What each type
counts is inference from the image's name; the official client only names them. Not hooked:
`find_hand_item`, `follow_friend`, `friend_furni_locked`, `place_builders_club_furni`,
`publish_picture`, `replenish_respect`, `request_friend`, `send_messenger_invite`,
`send_messenger_message`, `set_relationship_status`, `swim`, `teleport` and `use_habbicon`.
