# The First Hotel

- [Introduction](#introduction)
- [What a New Hotel Has and Lacks](#what-a-new-hotel-has-and-lacks)
- [Getting In](#getting-in)
- [Taking Habbo's Data In](#taking-habbos-data-in)
- [Room Models](#room-models)
- [The Navigator](#the-navigator)
- [The Catalog](#the-catalog)
- [Currencies and Credits](#currencies-and-credits)
- [Furniture That Does Something](#furniture-that-does-something)
- [Checking the Result](#checking-the-result)
- [Scripting It](#scripting-it)

## Introduction

[Installation](installation.md) leaves you with a server that starts, migrates its own database
and takes a client's connection. It does not leave you with a hotel: Turbo ships no database
dump, by design. The schema and the rules come from the migrations, and the content comes from
two places you drive yourself: Habbo's own files, which the server downloads and you take in from
the admin panel, and the panel's editors and generators. Until you do, the hotel view opens on an
empty catalog, a navigator with no tabs, a purse at zero and a "Create room" that refuses every
layout.

This page is the sequence from an empty database to a hotel you can play in, as installed from
nothing on 2026-10-29 and checked against `main` since. Each step says what is missing, what
fills it and how to tell it worked. Most of the time goes to downloads from Habbo.

> [!NOTE]
> Everything here is done once per hotel. Every step is idempotent: run it again and it changes
> nothing.

## What a New Hotel Has and Lacks

After the first start the migrations have seeded what the code needs to run: permission groups,
chat styles, pet breeds, guild badge parts and colours, the CFH topics, the quest campaigns, the
Habbo Club memberships (on a hidden catalog page) and the furni behaviours table. They have not
seeded anything a player sees:

| Table | Fills | Without it |
| --- | --- | --- |
| `furniture_definitions`, `gamedata_texts`, `gamedata_products`, `gamedata_figure_records` | [Taking Habbo's data in](#taking-habbos-data-in) | One furni, no clothing, no texts of its own |
| `room_models` | [Room models](#room-models) | Every room creation refused: `Rejected room creation by player 1: unknown model model_a` |
| `navigator_top_level_contexts`, `navigator_flatcats` | [The navigator](#the-navigator) | A navigator with no tabs and no categories |
| `catalog_pages`, `catalog_offers` | [The catalog](#the-catalog) | An empty shop, and nothing for the catalog generator to hang pages on |
| `currency_types` | [Currencies and credits](#currencies-and-credits) | A purse at zero, prices that cannot be charged, `:give` that knows no currency |
| `players`, `security_tickets` | [Getting in](#getting-in) | No one to log in as |

## Getting In

The client logs in with a login ticket in its address (`?sso=<ticket>`,
[Connecting a client](installation.md#connecting-a-client)). The hotel issues tickets in two
places, the [public site](public-site.md)'s Play button and the
[admin panel](admin-panel.md#login-tickets)'s Login ticket card, and both need something first:
the site a Discord application, the panel a passkey. On a development hotel the shortest path
is the panel:

1. Name yourself the owner in `appsettings.Development.json`
   (`Turbo:Owner:Name`, [Becoming the owner](installation.md#becoming-the-owner)) and turn the
   admin API on (`Turbo:Admin:Enabled`, [Setting it up locally](admin-panel.md#setting-it-up-locally)).
2. Start the server. Nothing happens yet: the owner has no player.
3. Make the player. The panel cannot, since nobody can sign in to it, so insert the row:
   `INSERT INTO players (name, name_key, motto) VALUES ('YourName', 'yourname', '')`, the name
   key being the name in lower case (`PlayerNames.Key`). Restart: the log says
   `YourName (player 1) is now the hotel's owner` and prints the panel's setup link.
4. Open the setup link, create your passkey, and on your player's page issue a reusable login
   ticket. Open the client with it.

> [!NOTE]
> A ticket the server did not issue also works when written to `security_tickets` as it is, with
> `is_locked = 1` so a login does not use it up: that is what the load-test bots do
> ([Load-test bots](loadbots.md#accounts)), and it saves the panel round trip on a machine of your
> own. Never on a hotel other people reach.

## Taking Habbo's Data In

On every start, and every `Turbo:Gamedata:ReleaseCheckMinutes`, the server reads Habbo's public
gamedata (`www.habbo.<HabboDomain>/gamedata/`) and keeps what it finds: the furniture data, the
texts, the product data, the figure data and the external variables. It applies none of it until
you do ([Gamedata](gamedata.md#how-it-works)). The asset sync, which downloads each furni's file
and converts it to a bundle the client draws ([Asset bundles](asset-bundles.md#syncing-from-habbo)),
starts on its own after the first check.

In the panel, under **Gamedata**, take in the furniture release, then the texts, the products
and the figures, one after another: the server runs one import at a time and answers
"Something of Habbo's is being taken in; wait for it to finish" to a second. The furniture import
reads every furni's file for its states, about 14,000 files on the first run, and takes ten to
twenty minutes; the others take seconds. The same from the API, with a panel session's token
([Calling the admin API](admin-panel.md#calling-the-admin-api)):

```bash
B=http://127.0.0.1:8090/api/gamedata; A="Authorization: Bearer $TOKEN"
curl -s -H "$A" $B                            # latestRelease.id, latestTexts.id, ... and importedAt
curl -s -X POST -H "$A" $B/habbo/import/1     # the furniture; then poll $B/habbo/import/job
curl -s -X POST -H "$A" $B/texts/import/1
curl -s -X POST -H "$A" $B/products/import/1
curl -s -X POST -H "$A" $B/figures/import/1
```

A definition the import makes gets the logic its classname, its file's client logic or its
furnidata category names, and the behaviour Sulake's data gives its class, hand items of the
vending machines included ([Gamedata](gamedata.md#how-it-works)). What it cannot know is under
[Furniture that does something](#furniture-that-does-something).

## Room Models

A room is created on a model: the floor plan the client offers in its room creation window,
`model_a` to `model_z` and `model_0` to `model_9`, which it names by letter
(`CreateFlatMessageComposer`'s `model_<name>`). The server looks the name up in `room_models`
([NavigatorProvider.GetRoomModelIdByNameAsync](../Turbo.Navigator/NavigatorProvider.cs)), and the
table starts empty: Habbo's gamedata has no room models, and Turbo ships none. The built-in
starter room ([New user flow](onboarding.md#starter-rooms)) needs `model_h` as well.

A model row is a name, a heightmap, the door and two flags:

| Column | Holds |
| --- | --- |
| `name` | What the client asks for: `model_a` |
| `model` | The heightmap, one text row per tile row, rows separated by a line break. `x` is no tile; `0` to `9` and `a` to `z` are a tile at that height (`a` is 10). Rows may differ in length: a short row is `x` to the right. |
| `door_x`, `door_y`, `door_rotation` | The door tile and the way in (`0` north, `2` east, `4` south, `6` west) |
| `enabled` | Offered to room creation |
| `custom` | A plan the floor plan editor saved for one room; never offered |

A small plan, the top left corner of the classic `model_a`:

```
xxxxxxxxxxxx
xxxx00000000
xxxx00000000
xxxx00000000
xxxx00000000
xxx000000000
xxxx00000000
```

with the door at `3, 5` facing east. The classic plans are the same in every emulator that
plays Habbo's client, and a hotel takes them from wherever it already has them; a model that
does not read is left out of the load with an error that names it, and the others load. Restart
after inserting them, or create one room with the floor plan editor: the provider reads the
table at startup ([RoomModelProvider](../Turbo.Rooms/Providers/RoomModelProvider.cs)).

## The Navigator

The navigator's tabs are rows of `navigator_top_level_contexts`, the room categories rows of
`navigator_flatcats`, and both start empty, so the window opens with no tabs at all. In the panel,
under **Content > Navigator** ([Content](admin-panel.md#content)), add the four tabs Habbo's
client knows, by search code and in this order: `official_view`, `hotel_view`, `roomads_view`,
`myworld_view`. Then the categories a room may be filed under; Habbo's own are Habbo Life,
Trading, Games, Chat & Discussion, Fansites, Help & Support, Role Play, Builds & Art, Parties &
Events, Agencies. From the API:

```bash
B=http://127.0.0.1:8090/api/content/navigator; J="Content-Type: application/json"
curl -s -X POST -H "$A" -H "$J" -d '{"searchCode":"hotel_view","visible":true,"orderNum":2}' $B/tabs
curl -s -X POST -H "$A" -H "$J" -d '{"name":"Habbo Life","visible":true,"orderNum":1}' $B/categories
```

The change reaches the navigator at once. The Public tab lists the rooms staff put in its
categories ([Public rooms](admin-panel.md#content)); a new hotel has none, which is fine.

## The Catalog

The catalog is a tree of pages under one root, and the editor makes every page under another
(`POST /api/catalog/pages` answers "Say which page to put it under" without a parent). The root
itself is made by hand, once:

```sql
INSERT INTO catalog_pages (parent_id, localization, name, layout, display)
SELECT NULL, 'Catalog', 'root', 'default_3x3', 2
FROM (SELECT 1) AS seed
WHERE NOT EXISTS (SELECT 1 FROM catalog_pages WHERE parent_id IS NULL);
```

`display` 2 is both catalogs. Then, under **Catalog**, **Generate** builds a whole catalog from
the furniture you took in: a front page, the Habbo Club pages, the furni by line on shelves by
theme, the wired, the pets, the extras, the rares, the groups and the Builders Club pages, each
with the layout and icon that suit it ([Catalog](admin-panel.md#catalog), "Generate"). Pick a
price for the new offers and **Replace this catalog**; on an empty catalog there is nothing to
move aside. Then **Publish**: nothing goes live before it. From the API:

```bash
B=http://127.0.0.1:8090/api/catalog
curl -s -X POST -H "$A" -H "$J" \
  -d '{"mode":"replace","reuseOffers":true,"maxPerPage":100,"costCredits":3,"costCurrency":0}' $B/generate
curl -s -X POST -H "$A" $B/publish
```

Generating takes a minute or two for Habbo's 18,000 furni and answers with the pages and offers
made (500 pages and 18,700 offers on 2026-10-29). The rares land on hidden pages, to be priced
first.

## Currencies and Credits

Every wallet credit, every catalog price and `:give` name a row of `currency_types`, and a new
hotel has none. Under **Content > Currencies** add the three the client draws in the purse:
`credits` (type Credits), `duckets` (activity points, type 0) and `diamonds` (activity points,
type 5); the change reaches the wallets at once. ([#502](https://github.com/nitrodevco/turbo-cloud/pull/502)
proposes seeding these three.) Then give yourself something to spend, in the hotel:

```
:give YourName credits 10000
```

or on your player's page in the panel ([Players](admin-panel.md#players)). The purse updates at
once. A hotel with no row of a kind is told nothing: the purse shows zero and a price in that
currency cannot be charged.

## Furniture That Does Something

A definition's `logic` column says what a furni does when it is used, stepped on or placed.
The import sets it from three things it can read: a logic registered under the classname (the
wired boxes), the client logic the furni's file names (`furniture_dice` is `dice`), and the
furnidata's category (`vending_machine`, `gate`, `teleport`, `roller`, which the file calls
`furniture_multistate` like any other). The behaviours table gives the known vending machines
their hand items and the crackables their contents as the definition is made.

Three groups it cannot place, because nothing in Habbo's data says what they are:

- **Gates in the `other` category** (`classic1_gate`, `xmas_c17_gate`, 32 on 2026-10-29): by
  name and their two states, on the furniture list, choose the rows and set `gate`.
- **Pet food, toys and accessories** (108): the `pet_*` logics, by name.
- **Hand-item providers in the `food` category** (75 trays and plates): `vending_machine` with
  their items, on the Behaviour editor.

A hotel that imported before the import read the category or the behaviours (before 2026-10-29)
has its fridges and gates on `default_floor`: the furniture list's **logic-mismatch** filter
flags them and one call fixes them (`POST /api/gamedata/furniture/logic` with `{}`), and
**Repair vending** gives the known machines their items ([Gamedata](gamedata.md)). Rooms load the
new logic as they load again.

## Checking the Result

- The server log at start: `Loaded room models: TotalModels=59, LeftOut=0`,
  `Loaded currency type mapping: Count=3`, `Loaded catalog snapshot: Type=Normal, TotalPages=...`,
  `Loaded navigator snapshot: TotalTopLevelContexts=4, FlatCategories=10`.
- In the client: the navigator has four tabs, **Create room** makes a room on `model_a`, the
  shop opens on the generated front page, a fridge bought and placed hands a drink out, and
  `:give` fills the purse.
- The panel's **Gamedata** overview counts the definitions without a bundle, the ones not sold
  and the logic mismatches: the first two should be near zero once the asset sync and the
  generator are done.

## Scripting It

Everything above is SQL and calls to the admin API, so a hotel of your own can hold it in one
script and run it after `bootstrap`: insert the owner and the room models, take Habbo's files in,
add the tabs and categories, insert the root, generate and publish, give the credits, restart
where the server reads a table at startup (the room models). The panel's session token is the
one its sign-in stores (`turbo-admin-session` in the browser's local storage), or, on a machine of
your own, a row of `admin_sessions` holding the SHA-256 of a token you made up, upper-case hex,
read when the auth grain activates.
