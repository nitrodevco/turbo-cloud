# Load-test bots

`Turbo.LoadBots` is a fleet of bot players that connect to a running hotel the way the client
does (TCP game socket or WebSocket, Revision20260909 protocol, SSO login) and play: they make
rooms, buy and place furni, draw floor plans, configure every kind of wired box, build wired games
and play each other's games. Every request a bot sends waits for the reply that proves it worked,
so a normal run measures latency and checks correctness at the same time.

The bots never call grains or the database to play. The database is used only to provision bot
accounts and, read-only, to learn what the catalog sells (as a client learns from furnidata).

## Quick start

```powershell
# 1. Make 50 bot players, their reusable SSO tickets, credits and floor plan permissions.
#    Run it while the bots are offline; it is safe to run again.
dotnet run --project Turbo.LoadBots -- provision --LoadBots:Provision:Count=50

# 2. A pass/fail check of every feature with five bots (exit code 1 on any hard failure).
dotnet run --project Turbo.LoadBots -- smoke

# 3. A load test: every provisioned bot, ramped in at 5/s, for 10 minutes.
dotnet run --project Turbo.LoadBots -- run --LoadBots:Run:DurationMinutes=10
```

The database connection comes from the server's own `appsettings*.json`
(`Turbo:Database:ConnectionString`); the environment defaults to `Development`. Everything else
is in `Turbo.LoadBots/loadbots.json` under `LoadBots`, and any option can be overridden on the
command line (`--LoadBots:Run:Bots=200`) or with `LOADBOTS__LoadBots__Run__Bots=200`.

`provision` writes `loadbots.accounts.json` (names and tickets; git-ignored) in the working
directory, and `run`/`smoke` read it from there. Reports are written next to it as
`loadbots-report-<time>.json`.

## Accounts

`provision` creates players named `<NamePrefix>0001`, `0002`, … (default prefix `lb_`). Each
bot gets:

- one `security_tickets` row with `is_locked = 1`, so the ticket survives login and the bot can
  log in run after run (an unlocked ticket is deleted on use);
- credits topped up to `Provision:Credits`;
- the permission nodes in `Provision:GrantedPermissionNodes` (by default
  `room.floorplan.save_without_club` and `room.floorplan.large`, which the architects need).

It writes to tables the server caches per player (currencies, permissions), so run it while the
bots are logged out.

## Personas

Each bot is given a persona in proportion to `Run:Personas` (shuffled by `Run:Seed`) and then
chooses its own activities by weight, one after another with a think pause between:

| Persona | Does |
| --- | --- |
| `Decorator` | Simple rooms: a few furni at a time, rearranging, chatting, visiting, playing games. |
| `Architect` | Complex rooms: random floor plans (redrawn through the floor plan editor), many furni, a running wired machine. |
| `WiredEngineer` | Takes the next wired box types in a fleet-wide rotation and puts each through the editor (below). |
| `GameHost` | Builds a pad game out of wired and a game timer, then starts rounds. |
| `Visitor` | Finds rooms (by word of mouth or the navigator), walks, chats, dances, waves, plays games. |

Bots reuse their own rooms (found through "my rooms" in the navigator) and stop creating new ones
at `Run:MaxRoomsPerBot`; builders pick furni back up once a room holds `Run:MaxItemsPerRoom`.

### Wired

- **Every box type.** An engineer buys a box, places it on its own tile (so no stack ever runs),
  opens its editor and reads what the server says the box accepts: its default int params,
  furni limit and allowed sources. It saves exactly that, with real furni from the room picked,
  reopens it to check the save kept, then sends a save with 17 int params (more than any box
  takes) and expects it refused. Staff-only boxes (`wf_act_give_reward`) are expected to refuse
  an owner. A box with no editor at all is reported under `wired.box.opens_editor`, except the
  boxes AGENTS.md lists as waiting on a system (chests, transactions, contracts, the web API):
  those are still tried every run, with a short timeout, and listed as known gaps
  (`wired.open_known_gap`, soft) until the server answers for them. The list is `KNOWN_GAPS` in
  `WiredActivities`; take a box off it once its logic exists.
- **Machines.** Architects stack `wf_trg_periodically` with `wf_act_move_rotate` on a furni,
  which keeps shoving it about every second: steady room traffic for everyone in the room.
- **Games.** A host builds three stacks: stepping on a pad gives a point and whispers
  `goal <room>` to the player, the game timer starting tells everyone `start <room>` and puts them
  on a team, and the timer running out tells everyone `over <room>`. Players walk onto pads and
  check each goal message reaches them; the host checks every round starts and ends.

## Reading the results

The console prints a progress line every `Run:ReportIntervalSeconds` (bots online, packets per
second, failures so far, slowest operations) and a summary at the end. The JSON report holds:

- `Operations`: per request type, count, failures (no reply in time) and latency (mean, p50,
  p95, p99, max) from send to the reply that proves it.
- `Checks`: every correctness check with passes, failures and up to 25 failure examples.
  - **Hard** checks are the server breaking its contract: a reply missing or wrong, a save not
    kept, a message the bot cannot decode (`protocol.decodes`).
  - **Soft** checks can be the bot's own world model (a path another avatar blocked), so watch
    their rate rather than single failures.
- `Counters`: packets and bytes each way, connections opened and dropped, activities run.
- `WiredOutcomes`: how the last save of each wired box type went.

## Keeping the bots in step with the protocol

The bots take their header ids straight from `Turbo.Revisions/Revision20260909/Headers.cs`
(`InternalsVisibleTo`), so renaming or renumbering a header breaks their build.
`Turbo.Tests/LoadBots` runs every bot request through the revision's parser and every server
message the bots read through the bot decoder, so a layout change on either side fails a test.
