# The database and its migrations

Turbo keeps its state in PostgreSQL, and the schema moves forward with the server through
EF Core migrations. **By default the server migrates itself**: start it against an empty database,
or one made by an older version, and it brings the database up to date, says what it did, and
carries on. Nothing else is needed to get going, and nothing needs the .NET SDK on the server.

```
Database core: creating the database, 2 migration(s)
Database core: applying 20261011075549_PostgresBaseline (1/2)
Database core: applied 20261011075549_PostgresBaseline in 1.6 s
Database core: applying 20261011075715_SeedReferenceData (2/2)
Database core: applied 20261011075715_SeedReferenceData in 195 ms
Database core: up to date at 20261011075715_SeedReferenceData, 2 migration(s) applied in 3.4 s
```

## Getting a database

PostgreSQL 16 or newer, with a user that may create the database (or create the database yourself
and make the user its owner). The migrator connects to the server's `postgres` database for its
lock and to create the hotel's database, so that one must be reachable too. For local work, Docker
is the quickest:

```bash
docker run -d --name turbo-postgres -e POSTGRES_USER=turbo -e POSTGRES_PASSWORD=turbo-dev \
  -p 127.0.0.1:5432:5432 postgres:16
```

then set `Turbo:Database:ConnectionString` (`appsettings.Development.json`, or the environment as
`Turbo__Database__ConnectionString`):

```
Host=127.0.0.1;Port=5432;Username=turbo;Password=turbo-dev;Database=turbo
```

and start Turbo. That is the whole setup.

## Running PostgreSQL in production

What a hotel's own server needs, in the order you set it up.

1. **Install PostgreSQL 16 or newer** (Ploi: Server > Databases, or the distribution's
   `postgresql` package). It can share the machine with Turbo on a small hotel.
2. **Make the database's cluster with checksums.** `initdb --data-checksums` (or
   `pg_createcluster ... -- --data-checksums` on Debian and Ubuntu) has PostgreSQL check every page
   it reads, so a disk that silently corrupts data is caught at the page it broke rather than
   copied into every backup after it. PostgreSQL 18 turns them on by default; before that it has to
   be chosen when the cluster is made, so check with `SHOW data_checksums;` before the first
   deploy, while making the cluster again costs nothing.
3. **Make two roles: one that owns the database, one the server runs as.** The server's role reads
   and writes rows and can change nothing else, so a bug, a plugin or a stolen password can't drop
   a table. Run `scripts/sql/least-privilege.sql` as a superuser once (`sudo -u postgres psql -f
   scripts/sql/least-privilege.sql`, after putting in two long random passwords). It makes:

   - `turbo_owner`, which owns the database and runs migrations, the emulator's and every
     plugin's. Turbo never needs a superuser: the one extension it uses, `pg_trgm` (for searching
     chat and room names), is "trusted", so the database's owner can create it.
   - `turbo_app`, the server's role. It may `SELECT`, `INSERT`, `UPDATE` and `DELETE` on every table
     the owner makes, now and later (default privileges), and nothing else: no `CREATE`, `ALTER`,
     `DROP` or `TRUNCATE`.
   - Limits on the server's sessions. A transaction left open (a crash between two writes) gives its
     row locks back after a minute (`idle_in_transaction_session_timeout`). A statement waiting on a
     lock gives up after ten seconds (`lock_timeout`), so requests don't pile up behind one stuck
     row. A runaway statement stops after a minute (`statement_timeout`). Migrations run as the owner
     and aren't limited.

   Then point `Turbo:Database:ConnectionString` at `turbo_app` and
   `Turbo:Database:MigrationConnectionString` at `turbo_owner` (Ploi: `TURBO_DB_USERNAME` /
   `TURBO_DB_PASSWORD`, and `TURBO_DB_MIGRATION_USERNAME` / `TURBO_DB_MIGRATION_PASSWORD`), and
   deploy: `Turbo.Main migrate` makes the tables as the owner. Keep PostgreSQL listening on
   `127.0.0.1` unless something else really must reach it.

   A hotel that wants one role can still have one: leave `MigrationConnectionString` empty and give
   `ConnectionString` the database's owner, as `CREATE ROLE turbo LOGIN PASSWORD '...'; CREATE
   DATABASE turbo OWNER turbo;` does.
4. **Size the memory once.** PostgreSQL's defaults are for a tiny machine. In `postgresql.conf`
   (or `ALTER SYSTEM SET ...` and a restart):

   | Setting | Start at | Why |
   | --- | --- | --- |
   | `shared_buffers` | 25% of RAM (no more than 40%) | PostgreSQL's own advice for a dedicated server; it also uses the OS cache. On a machine shared with Turbo, take the 25% from what Turbo leaves. |
   | `effective_cache_size` | 50-75% of RAM | Tells the planner how much the OS will cache; allocates nothing. |
   | `work_mem` | 16MB | Per sort or hash in each query, so many can be held at once; the default 4MB spills the admin panel's bigger sorts to disk. |
   | `maintenance_work_mem` | 256MB | Vacuum and index builds; one at a time per session, so it can be larger. |
   | `random_page_cost` | 1.1 on SSD/NVMe | The default 4 assumes spinning disks and makes the planner avoid indexes it should use. |

   `max_connections` can stay at 100: one Turbo silo's pool (Npgsql) holds what it uses, and a
   connection pooler is only worth it once a CMS, the panel and cron jobs share the server.
5. **Turn on query statistics.** Add `shared_preload_libraries = 'pg_stat_statements'`, restart, and
   run `CREATE EXTENSION pg_stat_statements;` as a superuser once. `SELECT query, calls,
   mean_exec_time FROM pg_stat_statements ORDER BY total_exec_time DESC LIMIT 20;` then shows what
   the database spends its time on, which is where the next index goes.
6. **Back up, and practise restoring.**
   - Nightly: `pg_dump -Fc -f turbo-$(date +%F).dump turbo`, copied off the machine (object storage,
     another server). A custom-format dump restores with `pg_restore -d turbo_restored file.dump`.
   - A dump loses everything since it was taken. To lose at most minutes, archive the WAL as well
     (PostgreSQL's continuous archiving, or a tool such as pgBackRest), which restores to any
     moment.
   - Restore one into a scratch database now and then. A backup nobody has restored is a guess.
7. **Leave autovacuum on** (the default). Furniture and wallets are updated all day and PostgreSQL
   keeps old row versions until vacuum clears them; `SELECT relname, n_dead_tup FROM
   pg_stat_user_tables ORDER BY n_dead_tup DESC;` shows whether it keeps up.

## The settings (`Turbo:Database`)

| Setting | Default | What it does |
| --- | --- | --- |
| `ConnectionString` | | The server's connection. |
| `MigrationConnectionString` | empty | The connection migrations run with: the owner role, when the server's own role may not change the schema (step 3 above). Empty: migrations use `ConnectionString`. |
| `MaxAutoPrepare` | `50` | How many of its most used statements each connection keeps prepared, so PostgreSQL plans them once. Measured 20 to 32% faster on the inventory and room loads and the wallet update. `0` turns it off, which only PgBouncer older than 1.21 in transaction mode needs; a `Max Auto Prepare` in the connection string wins. |
| `Migrate` | `Auto` | `Auto` applies what is pending at startup. `Check` applies nothing and refuses to start while anything is pending. `Off` does not look at the schema. |
| `AllowDestructiveMigrations` | `false` | Whether a migration that drops a table or a column may run on a database that has data. |
| `MigrationLockSeconds` | `60` | How long a migration waits for another one to finish. |
| `MigrationCommandTimeoutMinutes` | `30` | How long one statement of a migration may run. The usual 30 seconds is too short for an `ALTER` on a table with millions of rows. |

In the Ploi environment these are `TURBO_DB_*` (or `TURBO_DB_CONNECTION_STRING`),
`TURBO_DB_MIGRATION_*`, `TURBO_DB_MAX_AUTO_PREPARE`, `TURBO_DB_MIGRATE`,
`TURBO_DB_ALLOW_DESTRUCTIVE_MIGRATIONS`, and `TURBO_DB_MIGRATION_LOCK_SECONDS`.

`Auto` is right for a hotel on one server, which is nearly all of them. `Check` is for a hotel
whose deploy runs `Turbo.Main migrate` first (the Ploi scripts do) and wants the server to refuse a
database that is behind rather than quietly fix it. `Off` is for whoever owns the schema by hand.

## What the server will not do

A migration is never "just run". It stops, says why in one readable message, and exits with a
non-zero code (so a supervisor can see the server did not come up), in these cases:

- **The database was made by a newer version.** It has migrations this build does not ship.
  Migrations cannot run backwards, so an older server must not touch it: run the newer version, or
  restore a backup taken before the upgrade. (Minecraft refuses a world saved by a newer version
  for the same reason.) Applies in `Auto` and `Check`.
- **A migration would delete data.** A pending migration that drops a table or a column is
  refused on a database that already has migrations applied. Back up, then set
  `AllowDestructiveMigrations` to `true` (or run `Turbo.Main migrate --allow-destructive`) for
  that one start. A brand new database is never asked: there is nothing to lose.
- **Another process is migrating.** One migrator per database at a time; a second waits up to
  `MigrationLockSeconds`, then stops and says so.
- **Nothing can be reached.** No connection string, or a server that is not running, is reported as
  that, with the setting to check, instead of a stack trace.
- **A migration fails.** The message names it and says how many earlier ones were applied.
  PostgreSQL runs each migration in a transaction, so the failed one is rolled back whole and the
  database is left at the last one that worked. Fix the cause (or restore a backup) and start
  again: applying resumes at that migration.

Before applying to a database that has data, the server warns that a backup is the only way back.
Take one.

## `Turbo.Main migrate`

The same migrator, run by hand, for a deploy step or a look at where things stand:

```bash
dotnet Turbo.Main.dll migrate                    # apply what is pending, then exit
dotnet Turbo.Main.dll migrate --status           # exit 0 up to date, 2 behind, 1 on a problem
dotnet Turbo.Main.dll migrate --script turbo.sql # write the SQL for a DBA to review or run
dotnet Turbo.Main.dll migrate --allow-destructive
```

- It starts nothing else: no sockets, no plugins, no game.
- `--script` writes every migration as SQL that is safe to run on a database at any earlier
  migration, and twice (an idempotent script), and changes nothing. It needs no connection at all.
- It migrates the emulator's own tables. **Plugins' tables are migrated when each plugin loads**,
  by the same migrator, so the same settings (and the same refusals) apply to them. A plugin whose
  tables cannot be migrated (a database from a newer version, a destructive migration not allowed,
  or `Migrate` set to `Check` while it is behind) is **not loaded**: the server carries on without
  it and the log says why. For that reason `Check` suits a hotel with no plugins that have tables;
  with plugins, leave `Auto`.
- It ignores `Migrate=Off` and `Check`: asking for a migration is the operator deciding.

## Several things at once

One lock per database (a PostgreSQL advisory lock keyed on the database's name, taken on a
connection of its own) covers the emulator
and every plugin. Two servers started together, or a server started while a deploy migrates, take
turns, and the one that waited reads the state again once it has the lock, so nothing is applied
twice. The server releases the lock when its connection ends, so a migrator that is killed
mid-way never leaves the next start waiting on a lock nobody holds.

## For people writing migrations

The migrator's guarantees rest on how migrations are written:

- **A shipped migration is never edited.** A hotel that has applied it will not apply it again.
  A fix is a new migration.
- **Migrations only move forward.** There is no promise that `Down` works; a backup is the way
  back.
- **Add the migration with the change.** `dotnet ef migrations add Name` from `Turbo.Database`
  (see `CONTRIBUTING.md`). A test fails if an entity changed and its migration is missing: that is
  the "column does not exist" that stops every login.
- **Prefer adding to taking away.** To rename or remove a column, add the new one in one release
  and remove the old one in a later one, once nothing reads it. A migration that drops anything
  makes every hotel with data stop and ask its operator; that should be a deliberate release.
- **Seed data is idempotent.** Insert what is missing, never assume the table is empty.
- **One logical change per migration**, named for it, so the log reads well.
- **Plugins** keep their own history table (`__EFMigrationsHistory_<prefix>`) and table prefix, and
  call `MigrationHelper.MigrateAsync` from their `IPluginDbModule`, so they get all of the above.

## What a new hotel starts with

The `SeedReferenceData` migration gives every hotel the rows it needs to be playable: credits,
duckets and diamonds; Habbo's 36 private-room models (`model_a` to `model_z`, `model_0` to
`model_9`); the navigator's tabs, room categories and event categories, named as Habbo's texts
name them; a catalog root with a front page and the Habbo Club page selling one, three and twelve
months; and the permission groups, call-for-help topics, chat styles, pet breeds and speech, quests
and group badge parts. Furniture comes from Habbo's releases, imported in the admin panel, and the
catalog generator builds the rest of the catalog from it. `ANewDatabase_IsAPlayableHotel` checks
all of this against a real PostgreSQL.

## What a delete takes with it

- **A definition can't be deleted while it is used.** A furni definition, a room model or a
  currency is refused while any furniture, room, balance, offer or clothing uses it; the admin
  panel says what still uses it.
- **Logs keep no foreign keys.** Room chat, room entries, private messages and command logs name
  rooms and players by id, so they outlive both: a deleted room's chat is still there for the
  moderators. They have 64-bit ids, since a busy hotel writes them by the million.
- **What belongs to something goes with it.** A player's quests, a group's forum threads and read
  markers, a room's rights, bans and ratings.
- **A home room is checked, not constrained.** `player_settings.home_room_id` has no foreign key,
  because the settings grain writes the row whole and a stale id would fail every save. Deleting a
  room clears it, and loading settings drops one that no longer exists.

## How tables and columns are named

Every table and column is lower `snake_case`, written once in the entity's `[Table]` / `[Column]`
or made from the property name by the snake_case convention. A table is either one of the hotel's
root things (`players`, `rooms`, `furniture`, `pets`, `bots`, `guilds`, `quests`, `songs`,
`vouchers`) or starts with the domain it belongs to (`player_`, `room_`, `catalog_`, `messenger_`,
`moderation_`, `auth_`, `hotel_`, `gamedata_`, `habbo_` and so on), so a sorted table list groups by
domain. Times end in `_at` (every time is UTC, so no name says so), foreign keys in `_id`.
`SchemaNamingTests` holds every table, column, index and key to this, and to PostgreSQL's
63-character limit on a name.

Every table carries a comment saying what it holds (`[Comment]` on its entity), which pgAdmin,
DBeaver and `\d+` show beside it; `EveryTableSaysWhatItHolds` fails for a new table without one.
A string column is `text` unless its domain has a real limit, which it then declares with
`[MaxLength]`. A list is an array (`text[]` for a room's tags, `integer[]` for a goal's scores),
not a comma-joined string. A day is a `date`, a moment a `timestamptz`. A state is one enum column
(`player_respects.status`), not two booleans that could both be true. A rule the game keeps is kept
by the database too, as a CHECK, a unique index or a foreign key, so a row written from the panel,
a script or a plugin can't break it: a name and its lookup key always agree, a badge is worn in a
slot from 1 or not at all, a voucher is never used past its limit, a serial is sold once, a player
is on one quest at a time.

## Records of what happened

Most tables hold how things stand now. These hold how they got there, each written in the same
transaction as the change it records, so the record and the change can't disagree:

| Table | One row per | Answers |
| --- | --- | --- |
| `player_currency_ledger` | change to a balance, with its reason, detail and the balance it left | "my credits vanished", a disputed charge, a duplicated reward |
| `furniture_transfers` | furni changing owner: a trade (or one undone), a staff pickup that keeps it, a wired chest deposit or payout between players, a present opened by someone it changed hands to | "my rare was taken", a duplicated item |
| `moderation_actions` | moderation action: ban, silence, trade lock, disconnect, warning, room kick, ban and mute, room-wide mute, flood mute, rights taken away, a room's settings changed by staff who don't own it, badge, furni or present given, forum post hidden, a group renamed, its badge reset, a member removed or the group deleted by staff; by whom, from where (game, panel, console, the hotel itself), to whom, why, until when | what has been done to a player, what a staff member did, what happened in a room |
| `catalog_ltd_entries` | raffle entry or sale of an LTD, with the serial won | who owns which serial; no serial is sold twice |
| `player_subscription_grants` | extension of a membership: days, bought or given, until when | a disputed purchase, a gift |
| `player_logins` | session: address (through a trusted proxy), machine id, when it ended | a banned player's other accounts |
| `player_respects` | respect given, and whether it went through | respect farming between accounts |
| `wired_chest_transactions` | deposit or withdrawal at a wired chest | what went into and out of a chest |

`moderation_sanctions` and the permission grains hold what is in force (a ban, a silence);
`moderation_actions` is the history of every action, from every place it can be taken. It is
written as the action is taken, one row at a time (a few a minute at most, and staff look for one
the moment it is done), and a write that fails is logged with everything it would have recorded
and never fails the action itself, as OWASP's logging guidance asks. The panel reads it per
player, per staff member, per room or per kind, newest first, paged by id; `:history` shows a
player's last ones in game.

`room_chat_logs.created_at` is when the line was said: lines reach the database seconds later,
through the room's buffer and then the persistence grain's, so the time is taken as the room
hears the line. `message` is what the room heard, after the word filter; `typed_message` is what
the player typed, when the filter changed it, and null otherwise (nearly always, so it costs
nothing then). A chat search finds a line by either.

`players.last_seen_at` is the end of the player's last session. It is kept on the player as well
as in `player_logins`, so "last seen" survives the login log being trimmed.

### Respects, step by step

A respect changes two players, each owned by their own grain, so it can't be one transaction. It
is done in steps that can each be repeated safely:

1. A row in `player_respects` is written with a new id and `status` pending, before anyone changes.
2. The giver's grain spends one of today's respects and writes a `player_respect_changes` row
   (`kind` spent) in the same transaction.
3. The receiver's grain adds to their total and writes a `player_respect_changes` row (`kind`
   received) in the same transaction.
4. The respect becomes given (or refused, if the giver had none left), with the achievement facts.

If the server stops between steps, the recovery service finds the pending respect and runs it
again. A step already done finds its `player_respect_changes` row and does nothing, so nobody is
charged or credited twice. A scratch for a pet (`pet_respects`) is the same, and a pet fed
(`pet_feedings`) is one step, written with the pet's new nutrition.

## A furni is in one place

A furni is in an inventory (`room_id` and `chest_item_id` null), a room (`room_id`), or held by
another furni (`chest_item_id`: a wired chest, a jukebox's playlist, a present). The database
decides which as each move happens, not at the room's next save:

- **Placing** takes the furni into the room with one conditional write, only while the row is in
  its owner's inventory. A furni traded, deposited or placed elsewhere a moment before is refused,
  and the client's stale inventory entry is removed. A spot the room then refuses hands it back.
- **A jukebox** takes a disk in, and gives one back, the same way: one conditional write each,
  on where the row is now, in one transaction with the playlist's new order.
- **Picking up** and sending furni home (a room deleted, a floor plan changed, a rented space run
  out) take it out of the room in the database before any inventory lists it, and only what the
  database still has in that room. A staff member who keeps what they pick up becomes its owner
  in the same write, recorded in `furniture_transfers`.
- **The room's save** writes positions and data only, one statement per batch, and only for rows
  the database still has in that room, so a save written late, or again after a failure, can't
  put a furni back in a room it has left, and a row deleted meanwhile can't fail the rest.
- **The database holds it too.** `ck_furniture_one_place` refuses a row in a room and a holder at
  once; `ck_furniture_held` refuses a place among a holder's contents (`held_position`,
  `chest_transaction_id`) on a row in no holder, and the `furniture_leave_holder` trigger clears
  them however a row leaves its holder, including the holder's own row being deleted.

## Indexes and storage for the busiest tables

The logs the retention job trims by age (chat, private messages, room entries, logins, the
currency ledger, furni transfers, the command log, the moderation history, respects, pet
feedings, wallet receipts) each have a BRIN index on `created_at`, with `autosummarize` on. Rows
arrive in time order, so a summary per block range finds the old end of the table without
reading it. Measured on 5 million chat lines (a 444 MB table): the hourly pass with nothing to
remove went from a parallel scan of the whole table, 244 to 274 ms, to under a millisecond,
with an index of 32 kB where a b-tree on the same column is 80 MB.

`furniture` is stored with `fillfactor` 80. A room's save rewrites where its furni stand all day,
and no index covers those columns, so PostgreSQL can rewrite a row in place, with no new index
entries (a HOT update), when its page has room; full pages (the default, 100) never do. Measured
on 200,000 furni over 20,000 room saves: fillfactor 80 updated 20% in place against none, left the
indexes 35% smaller (70 MB against 108 MB), and saved 11% faster. 70 gained nothing more in index
size for a bigger table.

No index on `furniture` (or `furniture_builders_club`) holds a column a room's save writes
(`x`, `y`, `z`, `direction`, `wall_offset`, `extra_data`), and `FurnitureStorageTests` keeps it so.
That rules out covering indexes for loading a room or an inventory. Both were measured on 2 million
furni, cold (PostgreSQL restarted and the OS cache dropped before each load):

- **Inventory.** An index on `player_id` holding `id`, `definition_id`, `created_at` and
  `extra_data`, for rows in no room and no holder, loaded a 20,000-item inventory in 12-20 ms
  against 1.5 s, but only on a freshly vacuumed table. An inventory's rows share pages with furni
  in rooms, and rooms save all day, so those pages are rarely all-visible. After room saves the
  same load still read 13,403 of the 20,000 rows from the table (1.1-1.2 s, 25% better), while
  every save that changed a furni's state got 40% slower.
- **Room.** An index on `room_id` holding every column made position-only saves 83% slower
  (none in place, against 49%) and tripled the indexes (173 MB to 525 MB). A room of 2,000 furni
  loaded no faster after saves, because every row was fetched from the table anyway.

A cold room of 2,000 furni loads in about 150 ms and a warm one in 3 ms; an inventory is read
once, when it is first opened, and kept while the player is online.

Chat lines, the busiest log, are written by binary `COPY` (PostgreSQL's guidance for loading many
rows): a batch of 200 took 10 ms, against 50 ms through EF's batched inserts, on the same table
and indexes.

## What the hotel keeps, and for how long

Under UK and EU data protection law (storage limitation, as the ICO explains it) a record about a
person is kept only as long as its purpose needs, and there is no fixed period: each kind needs a
purpose and a period. `Turbo:Retention` sets each in days (0 keeps it for good), and
`DataRetentionService` removes older rows every hour, oldest first and a batch at a time, so it
never holds a long lock.

| Setting | Default | Purpose |
| --- | --- | --- |
| `ChatDays` | 90 | room chat and private messages: the evidence a report is checked against |
| `RoomEntryDays` | 90 | who entered which room |
| `LoginDays` | 180 | logins with address and machine: finding a banned player's other accounts |
| `CurrencyLedgerDays` | 365 | disputed charges, refunds, duplicated rewards |
| `FurnitureTransferDays` | 730 | tracing an item a player says was taken |
| `CommandLogDays` | 365 | what staff did, and when |
| `ModerationDays` | 0 (kept) | every ban, mute, kick and warning: what the next moderator judges a player by |
| `RespectDays` | 90 | spotting respect farming |
| `ReceiptDays` | 30 | records that only make a retried action safe |
| `DailyTaskDays` | 30 | daily tasks of past days |
| `WiredChestDays` | 180 | a chest's deposit and withdrawal log |
| `ClosedReportDays` | 365 | calls for help, once closed |
| `ExpiredRoomSanctionDays` | 7 | room bans and mutes that have run out |
| `EndedRoomEventDays` | 30 | room events that have ended |
| `AssetPublishDays` | 180 | asset publish runs |
| `AutomaticCatalogBackupsKept` | 20 | catalogue backups taken before a restore (ones made by hand are kept) |

A hotel that needs longer, or shorter, sets its own, and says so in its privacy notice. A
pending respect is never removed; the permission audit, sanctions and subscription grants are
kept for good. Where a player logged in from (address and machine) is shown only to staff with
`admin.players.identity` in the panel, or `command.alts` in game, and even `:alts` shows how many
addresses two accounts share, never the addresses.

`Turbo:Networking:TrustedProxies` lists the proxies whose `X-Forwarded-For` is believed (by default
the server itself, for nginx on the same machine). Behind any other proxy, add its address, or every
login will be recorded from the proxy.

## Searching text

PostgreSQL compares text exactly: `'Josh' = 'josh'` is false. A search a player types:

- **A player's name** goes through `name_key` (`PlayerNames.Key`), the lower-cased name, by equality
  or prefix. It is in the byte-order `C` collation, so its index serves a prefix `LIKE`.
- **Part of any other text** is `EF.Functions.Like(x.Column.ToLower(), LikePatterns.Containing(text),
  LikePatterns.ESCAPE)`, which escapes `%` and `_` in what was typed. On a table that grows, give
  it a trigram index on `lower(column)` in a migration (chat and room names have one).
- Never `x.Name == typed` or `x.Name.Contains(typed)` on what a player typed: both are
  case-sensitive.

## Furniture behaviour

A new hotel's furniture comes from Habbo's releases (the admin panel's import). The behaviour a
class has (a pet's food bowl, a club gift that cracks open, a vending machine, a trax song, a wired
chest) is given to a definition as it is made, from `Turbo.Gamedata/Furniture/FurnitureBehaviours.cs`;
a definition the hotel already has is never changed there. A new known behaviour goes in that
table, not in a data migration, which would only reach the definitions a hotel had when it ran.

## Deploying with Ploi

`scripts/ploi/deploy.sh` runs the release's own `Turbo.Main migrate` before it switches the
running server over, so a failed migration stops the deploy with the old release still serving.
See `scripts/ploi/README.md`.

## When something is wrong

| You see | It means | Do |
| --- | --- | --- |
| `... has migrations this version of Turbo does not know` | The database was used by a newer Turbo. | Run the newer version, or restore a backup from before the upgrade. |
| `... pending migrations would delete some of it` | A release drops a table or column. | Back up, then allow destructive migrations for one start. |
| `Another process has been migrating this database` | A second Turbo or a deploy holds the lock. | Wait for it. The lock is freed the moment its holder's connection ends; if the holder died without closing it, PostgreSQL frees it when it notices (see `pg_stat_activity`). |
| `... migration X failed` | X could not run; earlier ones are applied. | Fix what the message says, or restore a backup, and start again. |
| `... migration(s) behind` | `Migrate` is `Check` and the database is not current. | Run `Turbo.Main migrate`, or set `Migrate` to `Auto`. |
