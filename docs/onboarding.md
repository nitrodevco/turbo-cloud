# New user flow

Habbo greets a new account with two full-screen steps before the hotel view: **Choose your
style** (look and name) and **Choose your room** (a furnished starter room). Turbo runs the same
flow behind one switch, `Turbo:Onboarding:Enabled` (off by default, on the panel's Settings page).

What Habbo does here was recorded from Habbo (hhus, client `WIN63-202609091217`) on 2026-10-12
with a brand new account. Everything a hotel might want different is configuration, with Habbo's
behaviour as the default.

## Configuring it

| What | Where | Default |
| --- | --- | --- |
| The flow on or off | `Turbo:Onboarding:Enabled` | off |
| Each step | `Turbo:Onboarding:ChooseName`, `PickRoom` | both on |
| The starter rooms | `Turbo:Onboarding:StarterRooms` (below) | Habbo's room 10 |
| Names no one may take | `Turbo:Players:BlockedNames` | none |
| Suggestions for a taken name | `Turbo:Onboarding:NameSuggestions`, `SuggestionWords` | 3, Turbo's words |
| Room types the client offers | client `new.user.flow.roomTypes` | `10,11,12` |
| Their pictures | client `new.user.flow.room.image.url` (`%type%`) | Habbo's `nux_room_<type>_round.png` |
| Their names and descriptions | texts `room.name.<type>`, `room.description.<type>` | the hotel's texts |
| The looks the dice rolls | client `new.user.flow.figures.male`, `.female` (comma-separated) | Habbo's 25 each |
| Every other word on screen | texts `onboarding.*`, `rename.error.*`, `help.tutorial.name.changed` | the hotel's texts |

`Turbo:*` options are on the admin panel's Settings page (lists are edited as JSON) and apply after
a restart. Client options are the hotel's client configuration (Nitro Studio's external variables);
texts are its external texts.

### Starter rooms

Build each starter room in the hotel like any room - its floor plan, paint and furni - and list it
by the room type the client offers for it:

```json
"StarterRooms": [
  { "Type": "1", "RoomId": 4512, "Name": "%name%'s loft", "Description": "Welcome, %name%!" },
  { "Type": "2", "RoomId": 4513 },
  { "Type": "10" }
]
```

- `RoomId`: the room copied for every player who picks the type - its model (a floor plan of its
  own included), paint, wall height and thickness, and every furni with its state, owned by the
  new player. A teleporter's link to its partner is not copied; a wired box keeps its settings.
- No `RoomId`: Turbo's built-in template of that type (`10` is Habbo's, recorded).
- `Name` / `Description`: `%name%` is the player's name; left out, the source's are kept.
- An empty list offers the built-in templates.

The client's `new.user.flow.roomTypes` must list the same types, with a picture and texts for each.

### Blocked names

`Turbo:Players:BlockedNames` is a list of names and patterns, `*` standing for any run of
characters, compared whatever their case: `["Admin", "MOD-*", "*staff*"]`. It applies at sign-up
(and to the names the sign-up suggests) and in the flow's name box, which answers code `4`. Players
who already have such a name keep it.

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

The look is saved by the ordinary `UpdateFigureData` (and fitted to what the player may wear, as
any look is). The name box asks `CheckUserName` as the player types and `ChangeUserName` on
**I'm ready**. Answers, as Habbo gives them:

| Code | Meaning |
| --- | --- |
| `0` | Free (and, for us, the player's own name) |
| `2` / `3` | Too short / too long |
| `4` | Taken, with free suggestions made from it (`HutchCyclone`, `PurpleBill`); also a name the rules, the blocked names or the word filter refuse |
| `6` | The player has already chosen their name |

Turbo's sign-up already asks for a name, so a player may keep it: checking their own name answers
`0`. The name can be changed once, during the flow; afterwards every free name answers `6`, as on
Habbo.

## Starter room

`SelectInitialRoom` names a room type. Turbo makes the room (above), then answers
`SelectInitialRoom` (status `0`, the room) and `FlatCreated`; the client makes it home and walks in.
An unknown type, a finished step or a room that can't be made (room limit, model missing, a source
room that is gone) answers status `1` and room `0`, and the flow ends on the hotel view.

The room is made through the navigator like any other room, so the hotel's room limit and maximum
user count apply (Habbo's starter rooms take 75 users; Turbo's limit is
`Turbo:Navigator:MaxPlayersLimit`). Furni the hotel has no definition for are left out and logged.

Of Habbo's rooms only type `10` is recorded so far (`model_h`, wallpaper 2403, floor 610, 21 furni
of the `nt_newbie_room` line). Types `11` and `12` need one new Habbo account each to record; until
then a hotel using the built-in rooms should offer `new.user.flow.roomTypes = 10`.
