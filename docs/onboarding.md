# New user flow

- [Introduction](#introduction)
- [Configuring it](#configuring-it)
    - [Starter rooms](#starter-rooms)
    - [New users: the lobby and the journey](#new-users-the-lobby-and-the-journey)
    - [New user gifts](#new-user-gifts)
    - [Blocked names](#blocked-names)
- [How the client is told](#how-the-client-is-told)
- [Look and name](#look-and-name)
- [Starter room](#starter-room)

## Introduction

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
| The starter rooms | `:starterroom` in the hotel (below), `Turbo:Onboarding:MaxStarterRooms` | Habbo's room 10 |
| Names no one may take | `Turbo:Players:BlockedNames` | none |
| How long a player counts as new | `Turbo:Onboarding:NewUserHours` | 24 |
| The new user lobbies | `:nooblobby` in the hotel (below) | none |
| The new user gifts and how long they are offered | `Turbo:Onboarding:Gifts`, `GiftDays` (below) | none, 7 days |
| The reward tracks (Introduction, Habboween 2026) | the panel's Reward Tracks (`/api/reward-tracks`, docs/reward-tracks.md) | Habbo's two |
| Suggestions for a taken name | `Turbo:Onboarding:NameSuggestions`, `SuggestionWords` | 3, Turbo's words |
| Room types, pictures and names in the client | written by `:starterroom` (client `new.user.flow.roomTypes`, `new.user.flow.room.image.url`, texts `room.name.<type>` / `room.description.<type>`) | Habbo's |
| The looks the dice rolls | client `new.user.flow.figures.male`, `.female` (comma-separated) | Habbo's 25 each |
| Every other word on screen | texts `onboarding.*`, `rename.error.*`, `help.tutorial.name.changed` | the hotel's texts |

`Turbo:*` options are on the admin panel's Settings page (lists are edited as JSON) and apply after
a restart. Client options are the hotel's client configuration (Nitro Studio's external variables);
texts are its external texts.

### Starter rooms

Build the room in the hotel like any room - floor plan, paint, furni - then, standing in it:

1. **`:starterroom add`** offers it (permission node `command.starterroom`, not granted by default;
   up to `Turbo:Onboarding:MaxStarterRooms`, 3, as many as the picker lays out). Turbo writes the
   client's side for you: the room joins `new.user.flow.roomTypes` (its id is its room type),
   `new.user.flow.room.image.url` points at the gamedata host's `/gamedata/starter-rooms/%type%.png`, and
   `room.name.<id>` / `room.description.<id>` are the room's own name and description.
2. **The starter room camera opens** (nitro-next). Move the room or the window until the room sits
   well in the circle, step out of it yourself, and take the picture. Turbo draws it into Habbo's
   round frame - the white ring, the shadow, front furni breaking out of the frame - from the
   camera's own view, so walls and floors look as you see them. `:starterroom picture` takes it
   again whenever the room changes.
3. **`:starterroom list`** shows what is offered and which rooms still need a picture;
   **`:starterroom remove`**, in a room, stops offering it.

Every player who picks the room gets a copy of it as it is then - its floor plan, paint, walls and
every furni with its state; a teleporter's link is left out, a wired box keeps its settings. Editing
the room changes what later players get. The pictures need `Turbo:Gamedata:PublicUrl` (the gamedata
host's address the client loads from). With no starter rooms of the hotel's, the built-in ones are
offered: Habbo's room 10 and its picture.

### New users: the lobby and the journey

A player who signed up less than `Turbo:Onboarding:NewUserHours` ago is told at login they are a
new user (`NoobnessLevel` 2, "real noob"; Habbo's own length is not known). For them, when the
hotel has a lobby, the client:

- puts up **"Welcome to Habbo - Enter a room to meet other new users"** a while after they enter
  their home room (`nux.noob.lobby.popup.delay` seconds: Habbo has 90, Flash's default is 70);
  **Enter the room!** asks for `predefined_noob_lobby`;
- sends them to a lobby when they leave a room (the toolbar's reception button too) instead of the
  hotel view, and home when they leave a lobby, where the toolbar shows a home button.

A lobby is a room with door mode 4: only new users, staff (`room.enter.locked`) and whoever has
rights get in, and Turbo sends a new user to the fullest lobby with room left. Build the room, stand
in it and **`:nooblobby add`** (permission node `command.nooblobby`, also needed to pick door mode
4 in the panel's room editor; the game's room settings window cannot). `:nooblobby remove` opens
it again and `:nooblobby list` shows the lobbies and who is in them. Turbo turns the client's
`nux.lobbies.enabled` on while there is a lobby and off when the last one goes.

Habbo's lobby (2026-10-12) was "Fresh Brew Coffee" by official_rooms, for 80, with a bot that says
"Double-click the doors to enter other rooms and meet new friends!" - furnishing is the hotel's.

"**Start your Habbo Journey!**" is the client's new feature notification
(`notifications.new_feature.active` `rewardtrack_intro`), shown while the `introduction` reward
track is not complete. With no tracks of its own, Turbo offers Habbo's Introduction track as Habbo
sent it to the new account (30 tasks, its points and premium) with the 16 of its 49 prizes Turbo can
give: duckets, diamonds, badges and furni, found by sprite. Its 30 Habbicons, 2 pets and a bot are
left out until Turbo can give those. Turbo counts 17 of the 30 task types so far.

### New user gifts

Habbo's client has a window that cannot be closed, **"Choose gift option"** - "Get your free gift!"
over up to three gifts side by side, each with a picture, its name and **Get this**. A step is one
row of gifts; with more than one step due the window goes through them one after another
("Choose gift option 1/2") and sends every pick at the end. Turbo offers the steps of
`Turbo:Onboarding:Gifts` (a list, edited as JSON on the panel's Settings page; empty, the default,
offers none):

```json
[
  { "Day": 1, "Step": 1, "Options": [
    { "Thumbnail": "nux/gift_chair.png", "Products": [ { "ProductCode": "chair_norja", "Amount": 2 } ] },
    { "Products": [ { "ProductCode": "pet_cat", "LocalizationKey": "nux.gift.cat", "Pet": "1 0 ffffff", "PetName": "Kitty" } ] }
  ] }
]
```

| Field | Meaning |
| --- | --- |
| `Day` | Offered from this day on; day 1 is the day the player signed up |
| `Step` | Tells a day's steps apart; each day and step is picked once |
| `Thumbnail` | The gift's picture, a path under the client's `image.library.url`; empty for none |
| `ProductCode` | The client names the product by its product data (else the text `product_<code>_name`); also the furni given |
| `LocalizationKey` | A text the client names the product by instead (Habbo's `nux.gift.cat`, `.dog`, `.pig`) |
| `Furni` | The furni definition given, when it is not the product code's |
| `Pet`, `PetName` | A pet given instead of a furni, as a pet figure: "type palette colour" |
| `Amount` | How many are given (1) |

A player is offered the steps due to them that they have not picked from as they enter a room,
until `Turbo:Onboarding:GiftDays` (7) days after sign-up; 0 offers them whatever the player's age.
The window cannot be closed, so a reload or a room change brings it back at the next room. Picks
of a step not due, already picked or naming furni or a pet the hotel does not have are left out,
and the step stays open. A pick is kept as a free product claim (`player_product_claims`, claim id
`nux:<day>:<step>`); the gifts are moved with cause `onboarding.gift`.

What Habbo offers and when is not recorded: its texts speak of a gift on the second day ("For your
gift today, you have three options" - a cat, a dog or a pig) and a refrigerator on the first.

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
