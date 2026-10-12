# New user flow

Habbo greets a new account with two full-screen steps before the hotel view: **Choose your
style** (look and name) and **Choose your room** (a furnished starter room). Turbo runs the same
flow behind one switch, `Turbo:Onboarding:Enabled` (off by default, on the panel's Settings page).

Everything here was recorded from Habbo (hhus, client `WIN63-202609091217`) on 2026-10-12 with a
brand new account.

## How the client is told

Login's `AuthenticationOK` carries a list of *suggested login actions*. The Flash client
(`HabboLandingView.isOnboardingRequired`) draws the flow when the list holds:

| Action | Step | Option |
| --- | --- | --- |
| `0` | Look and name | `ChooseName` |
| `1` | Starter room | `PickRoom` |

Habbo sent `[5, 6, 0, 1]`; 5 and 6 are not read by this flow and Turbo does not send them.

A player created while the switch is on gets both steps (`players.onboarding_name_pending`,
`players.onboarding_room_pending`, migration `AddPlayerOnboardingSteps`). Each step stays until the
player finishes it, so a player who closes the client mid-flow sees it again at their next login.
Players created while the switch was off never see it.

## Look and name

The look is saved by the ordinary `UpdateFigureData`. The name box asks `CheckUserName` as the
player types and `ChangeUserName` on **I'm ready**. Answers, as Habbo gives them:

| Code | Meaning |
| --- | --- |
| `0` | Free (and, for us, the player's own name) |
| `2` / `3` | Too short / too long |
| `4` | Taken, with three free suggestions made from it (`HutchCyclone`, `PurpleBill`); also a name the rules or the word filter refuse |
| `6` | The player has already chosen their name |

Turbo's sign-up already asks for a name, so a player may keep it: checking their own name answers
`0`. The name can be changed once, during the flow; afterwards every free name answers `6`, as on
Habbo. `SuggestionWords` holds the words suggestions are made with.

## Starter room

`SelectInitialRoom` names a room type (`new.user.flow.roomTypes`, `10,11,12` in Habbo). Turbo makes
the room from `Turbo.Navigator/Onboarding/StarterRooms/<type>.json`, then answers
`SelectInitialRoom` (status `0`, the room) and `FlatCreated`; the client makes it home and walks in.
An unknown type, a finished step or a room that can't be made (room limit, model missing) answers
status `1` and room `0`, and the flow ends on the hotel view.

The room is made through the navigator like any other room, so the hotel's room limit, model and
maximum user count apply (Habbo's starter rooms take 75 users; Turbo's limit is
`Turbo:Navigator:MaxPlayersLimit`). Furni the hotel has no definition for are left out and logged.

Only type `10` is recorded so far (`model_h`, wallpaper 2403, floor 610, 21 furni of the
`nt_newbie_room` line). Types `11` and `12` need one new Habbo account each to record; until then
the client should offer `new.user.flow.roomTypes = 10`.
