# Importing a Morningstar hotel

- [Introduction](#introduction)
- [Before you start](#before-you-start)
- [Running it](#running-it)
- [What comes across](#what-comes-across)
- [What does not](#what-does-not)
- [Ranks](#ranks)
- [Logging in after the move](#logging-in-after-the-move)
- [Furniture and its definitions](#furniture-and-its-definitions)
- [Wired](#wired)
- [Reading the report](#reading-the-report)
- [When it stops halfway](#when-it-stops-halfway)
- [For people changing the importer](#for-people-changing-the-importer)

## Introduction

`Turbo.Main import morningstar` copies a hotel that runs on Arcturus Morningstar (3.x, and the 4.0
development schema) into Turbo's database: its players and everything they own (wallets, badges,
friends, rooms, furniture, pets, bots, groups) and the parts of the hotel's setup that furniture
and rooms stand on. It reads the Morningstar database and never writes to it, then exits; the
server is started as usual afterwards.

Morningstar's ids are kept. Player 42 is player 42 in Turbo, room 1003 is room 1003, and item
900001 is furniture 900001, so a CMS that names players by id, or a link to a room, still works.

## Before you start

1. **Stop both servers.** Morningstar writes players and rooms back as they leave; Turbo's grains
   read rows once, when they load.
2. **Back up both databases.**
3. **Give Turbo an empty hotel.** The target must have no players, rooms, furniture, pets, bots or
   groups: a fresh database, or one emptied with `scripts/sql/fresh-start.sql`, which keeps the
   hotel's setup. The import refuses anything else before it writes a row.
4. **Import Habbo's furnidata into Turbo first** (admin panel, Gamedata & assets → Furniture;
   [Gamedata](gamedata.md)). Every Morningstar item base whose classname Turbo then has uses
   Turbo's definition, with Turbo's behaviour for it. Only what is left (a hotel's own custom furni)
   is created from Morningstar's `items_base`.
5. **Have a MySQL user that can read the Morningstar database.** `SELECT` is all it needs.

## Running it

```bash
dotnet Turbo.Main.dll import morningstar --source "Server=127.0.0.1;Database=arcturus;User=reader;Password=..." --dry-run
```

`--dry-run` reads and translates everything and writes nothing: run it first and read its
[report](#reading-the-report). Then run it without `--dry-run`. Turbo's own database is the one in
`Turbo:Database:ConnectionString`, as for the server; it is migrated first, as the server would at
start.

| Option | What it does |
|---|---|
| `--source <cs>` | The Morningstar database. Required. |
| `--dry-run` | Read and report, write nothing. |
| `--rank <id>=<group>` | Put Morningstar rank `<id>` in Turbo permission group `<group>`. Repeatable. See [Ranks](#ranks). |
| `--catalog` | Also copy the catalog's pages and offers, under Turbo's root page beside its own. |
| `--chatlogs` | Also copy room chat logs, and private messages as messenger history. |

Any other `--Turbo:...=value` goes to the configuration, as for the server. The exit code is 0
when it finished, 1 when it stopped (the log says why) and 64 for a command line it did not
understand.

Rows are written a thousand to a statement: on a local MariaDB, 200,000 items took under five
seconds, so a hotel of millions of items takes minutes.

## What comes across

| Morningstar | Turbo | Notes |
|---|---|---|
| `users` | `players` | Name, motto, look, gender, created, last online. A name that differs from another only in case gets `_<id>` appended. |
| `users.credits`, `users_currency` | `player_currencies`, `currency_types` | Type 0 is duckets, 5 diamonds; a type the target lacks is added. |
| `users_subscriptions`, `users_settings.club_expire_timestamp` | `player_subscriptions` | Habbo Club, the later of the two. |
| `users_settings` | `players`, `player_settings` | Respect, online time, volumes, privacy switches, chat bubble and style, home room. |
| `users.rank` | `player_permission_groups` | See [Ranks](#ranks). |
| `users_badges`, `users_wardrobe`, `users_effects`, `users_clothing` | `player_badges`, `player_outfits`, `player_effects`, `player_figure_sets` | An effect keeps its running time and its waiting copies. |
| `messenger_friendships`, `messenger_friendrequests`, `messenger_categories`, `users_ignored` | `messenger_*` | |
| `users_saved_searches`, `users_favorite_rooms` | `player_navigator_saved_searches`, `player_favorite_rooms` | |
| `rooms`, `room_models`, `room_models_custom` | `rooms`, `room_models` | A room's own floor plan becomes `room_<id>`, as Turbo's floor plan editor names one. A model neither hotel has falls back to `model_a`. Name, description, tags and visitor limit are held to this hotel's room limits (`Turbo:Rooms`). |
| `navigator_flatcats` | `navigator_flatcats` | Matched by name; a category the target lacks is added. |
| `room_rights`, `room_bans` (running), `room_votes`, `room_wordfilter` | `room_rights`, `room_bans`, `room_ratings`, `room_filter_words` | |
| `guilds`, `guilds_members`, `guilds_forums_*` | `guilds`, `guild_members`, `guild_forum*` | A group's favourite flag is the member's `users_settings.guild_id`. |
| `items_base` | `furniture_definitions` | See [Furniture](#furniture-and-its-definitions). |
| `items`, `items_teleports`, `trax_playlist`, `soundtracks` | `furniture`, `songs` | Teleporter pairs, presents, jukebox playlists, LTD serials, dimmer presets. |
| `users_pets`, `bots` | `pets`, `bots` | A pet's level is worked out from its experience by Turbo's `Turbo:Pets` rules. |
| `bans` (running) | `player_sanctions` | An IP, machine or super ban becomes a ban of its account. |
| `catalog_pages`, `catalog_items` (`--catalog`) | `catalog_*`, `ltd_series` | Each page keeps its images and texts in the order its layout sent them. |
| `chatlogs_room`, `chatlogs_private` (`--chatlogs`) | `room_chatlogs`, `messenger_messages` | A private message is cut to `Turbo:Players:MessengerMaxMessageLength`. |

Where something cannot come across whole it still comes across where it can: furni in a room that
was not copied goes back to its owner's inventory, furni whose owner was deleted stays in its room
with the room's owner, a marketplace listing goes back to its seller, and a pet or bot in a missing
room waits in the inventory. Each is counted in the report.

## What does not

- **Passwords, e-mail addresses and SSO tickets.** See [Logging in](#logging-in-after-the-move).
- **Achievement progress.** Turbo counts achievements from facts it records itself
  ([Achievements](achievements.md)); the badges earned come across, the counters do not.
- **The hotel's configuration** in `emulator_settings` and `emulator_texts`, the word filter, polls,
  crafting recipes, calendar campaigns and YouTube playlists: set them up in the admin panel.
- **Logs** (room entries, trades, commands, CFH tickets, sanction history).
- **Pets, bots and club offers in the catalog**, which Turbo sells its own way.
- **Bot kinds.** A bartender or visitor log bot becomes a plain bot.
- **Highscore boards' scores** and **wired reward history** (who already got a reward).
- **Asset movements.** The import writes the hotel's starting rows, as a migration would, and
  records no movements for them ([Asset Movements](movements.md)).

## Ranks

Morningstar's ranks are one row of switches each (`permissions`); Turbo's are permission groups of
nodes ([Permissions](permissions.md)). The switches do not carry over one by one: each rank is put
in the group that does its job.

- Rank 1 is the default group, which every player is in without a row.
- `--rank 7=admin --rank 5=moderator` names the group for a rank.
- A rank not named is matched by its name: `Administrator`, `Owner`, `Founder` or `Developer` to
  `admin`; `Manager` to `manager`; `Senior Moderator` to `senior_moderator`; `Moderator` to
  `moderator`; and so on for helpers, ambassadors, builders, event staff and VIPs.
- A rank nothing matches is the default group, and the log says so.

The log lists every rank and the group it went to, with why: read it in the dry run.

## Logging in after the move

Turbo's own site signs players in with Discord ([Public Site](public-site.md)), so it has no
passwords to copy. A hotel that keeps its CMS keeps Morningstar's accounts there: the CMS goes on
checking passwords against its own table and writes the login ticket for the same player id into
Turbo's `security_tickets`, which the client then presents. Because ids are kept, nothing has to be
mapped.

## Furniture and its definitions

An item base is matched to a Turbo definition by type and classname (`item_name`), in any case,
as Turbo's gamedata import matches Habbo's furnidata. A match keeps Turbo's definition as it is:
its logic, its sprite id, its configuration. What is not matched is created from Morningstar's
row: its size and flags, a logic chosen from its `interaction_type` (a teleport is `teleport`, a
dimmer `dimmer`, a wired box its own name), its vending items as a `vending` section and its
crackable rewards (`items_crackable`) as a `crackable` section. If its sprite id is taken, it is
given the next free one.

The logic an item's state is written for is the one its room will run: the definition's logic,
or what the room makes of it by classname, category or custom params (Turbo's own `bb_counter`
is plain furniture by its logic and a game timer in a room). The import asks the server's own
rules (`RoomObjectLogics`), with the logic core registers; plugins are not loaded for an import,
so a plugin's furni logic is not known to it.

Each item's state is rewritten as its Turbo logic reads it ([Furni Logic](furni-logic.md)):
trophies, badge displays, post-its, mannequins, background toners, room ads, love locks, group
furni, music discs and rentable spaces each have their own format in both emulators. A dimmer
takes its room's presets, which Morningstar keeps on the room and Turbo on the dimmer. A present
becomes Turbo's present row holding the items it wraps, in a box and ribbon this hotel wraps
in (`Turbo:Catalog:GiftWrapping`), else the free box.

## Wired

A placed wired box keeps its settings: its picked furni, its delay (500 ms pulses in both), its
text and its options, rewritten into the integer parameters of Turbo's box of the same name. Its
picks and delay are held to this hotel's wired limits (`Turbo:Wired:SelectedItemsLimit`,
`ActionDelayMaxPulses`). A match-snapshot box keeps the state it remembers, heights as the room
compares them. Morningstar's boxes have the same names as
Turbo's for nearly everything; the few Turbo has no twin of (give hand item, give respect, team win
and lose triggers, alert) come across as furni that does nothing, and the report lists them. A
give-reward box keeps its badge and furni rewards; credits, points and catalog rewards have no
place in Turbo's box and are reported.

Morningstar clears a box's settings when it is picked up, so a box in an inventory had none to copy.

## Reading the report

The import ends with what it wrote to each table, then everything it left out or changed, grouped
by why, with the first few rows of each:

```
Rows written:
  players                                         18,204
  furniture                                    1,402,118
  ...
Left out or changed:
       312  item put in its owner's inventory: its room was not copied
              e.g. item 88213 room 4417
        41  item left out: its item base was not copied
              e.g. item 90021 base 7710
```

Nothing is left out silently. Read the dry run's report before the real run: a large count under
"item base was not copied" usually means the hotel's furnidata was not imported into Turbo first.

## When it stops halfway

The setup it adds (definitions, models, categories, currencies, songs) is merged with what is
there, so a second run uses what the first added. The players and everything of theirs are not:
run `scripts/sql/fresh-start.sql` on the target, then the import again.

## For people changing the importer

The importer is `Turbo.Import` (`Turbo.Import/Morningstar/`): one step a table or a few
(`Steps/`), each translator of a Morningstar format in `Formats/`. It reads through
`Source/SourceDatabase`, which tolerates missing tables and columns, so one importer reads every
Morningstar version, and writes through `Target/TableWriter`, multi-row inserts built from Turbo's
EF model. `Turbo.Tests/Import/` holds the tests: an in-memory Morningstar database (`MorningstarDb`)
imported into Turbo's SQLite test database, and one test against a real MySQL server
(`TURBO_TEST_MYSQL`).

What the importer needs from the server comes in `MorningstarImportOptions`, which
`ImportStartup.Options` builds from the host's configuration (the tests build theirs the same way,
from the default configuration): how pets level (`PetRules`), the limits what is copied keeps to
(`ImportLimits`), and which furni logic exists and which one a definition runs. A value the server
already names comes from where the server keeps it: an extra data section is written as its
record (`FurnitureExtraDataSections.ToNode`), a logic or section name, a length or a default
figure is the shared constant, text is cut with `ClientText.Truncate`, and a limit an operator
tunes is an `ImportLimits` field, not a number in a step.
