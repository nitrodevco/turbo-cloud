# Achievements

Achievements process immediately after a validated catalog is imported. There is no activation date, baseline or historical-action backfill. Account age uses account creation time; online time credits only durably recorded intervals. Reloading definitions and restarting Turbo preserve progress and awards.

The official [Habbo achievements API](https://www.habbo.com/api/public/achievements) is the source of truth for published achievement names, categories, states and `requiredScore` thresholds. The snapshot retrieved on 2026-10-02 contains 167 records. The API does not define rewards, reducer semantics, source units, gameplay hooks, eligibility or attribution. The accompanying [coverage CSV](achievement-coverage.csv) records every snapshot row and the local mapping decision; it also lists three hotel extensions absent from the API. AS3 defines packet order and client behavior, while the official JavaScript client provides implementation guidance.

## Install and configure

Apply migrations `AddRespectAndPetOperationJournals` and `AddAchievements` through the normal EF migration workflow. They add tables and one column (`player_subscriptions.purchased_days_subscribed`); nothing else existing is altered. See [Storage](#storage) for what each table is for. The shipped Habbo catalog is a [pack](#packs). On startup it is installed through the normal audited import (operation `pack:habbo:<version>:<hash>`): it adds every definition whose key and id the hotel does not have yet and never touches one it does, so your edits and retirements survive a new pack version while new Habbo achievements still arrive. It needs `Turbo:Achievements:BadgeAssetDirectory` and the badge texts for the achievements it enables; without them nothing is installed and a warning names `achievement reload`. `Turbo:Achievements:InstallDefaults` set to `false` turns the Habbo pack off.

Configure `Turbo:Achievements:BadgeAssetDirectory` to the directory containing the hotel's badge PNGs. The hotel's configured external texts must contain each enabled badge's name and description, either its exact code or its numeric-level base, matching AS3 localization lookup. Serve the same assets and texts to Nitro.

Nitro's `badge.asset.url` must resolve every enabled badge, including levels 11–20. Standard Habbo badges are available at `https://images.habbo.com/c_images/album1584/%badgename%.gif`; configure your hotel's asset endpoint for custom badges. Server-side PNG validation does not establish that the client's configured image endpoint serves those assets. A development URL query override can set `badge.asset.url` without changing the shared client configuration.

In the Turbo console:

```text
achievement defaults achievements.json
achievement sync achievements.json
achievement sync achievements.json --apply christmas-2026 Add the Christmas achievements
achievement retire twelve-days-of-christmas --apply retire-xmas-2026 Season is over
achievement import achievements.json --apply install-achievements-v1 Publish exactly these revisions
achievement export current-achievements.json
achievement reload
```

Everything validates only, and says what it would do, unless `--apply <operation-id> <reason>` is given; applying publishes one atomic, audited batch. Failed reloads retain the working catalog. Operations are replayable: reusing an operation ID with different content is rejected. `achievement defaults` exports every registered pack's definitions as a template, `achievement import` publishes the revisions exactly as numbered in the file, and [`achievement sync`](#adding-your-own-achievements-without-code) is the one to use day to day. The in-game `:reload achievements` operator command does the same as `achievement reload`. Keep exported catalogs under hotel version control.

Configuration defaults:

| Setting | Default | Meaning |
| --- | --- | --- |
| `Turbo:Achievements:RecoverySeconds` | 5 | Persistent dispatcher poll interval |
| `Turbo:Achievements:RecoveryBatchSize` | 100 | Players/operations per recovery batch |
| `Turbo:Achievements:FactBatchSize` | 100 | Facts consumed in one player turn |
| `Turbo:Achievements:InstallDefaults` | true | Install the shipped Habbo pack (read at startup; installing adds only what the hotel lacks) |
| `Turbo:Achievements:ArchiveShowsAll` | false | List every archived achievement to every player; by default only to players who progressed it |
| `Turbo:Achievements:FactRetentionDays` | 30 | Days a processed fact is kept for idempotent admission before it is deleted; `0` keeps them |
| `Turbo:Achievements:MaxDefinitions` | 1000 | Catalog size limit |
| `Turbo:Achievements:MaxDistinctValues` | 100000 | Distinct reducer storage limit |
| `Turbo:Achievements:MaxMatchValues` | 1000 | Most values a definition's match list may hold |
| `Turbo:Achievements:DefaultCategory` | identity | List packet's default category |
| `Turbo:Achievements:ShowCongratulationsDialog` | true | Level-up dialog flag; corner notifications remain enabled |
| `Turbo:Players:AchievementOnlineIntervalSeconds` | 30 | Durable online checkpoints; also flushed on clean disconnect |

## Storage

Seven achievement tables, plus four operation journals that belong to respect and pet care:

| Table | Holds | Why it is a table |
| --- | --- | --- |
| `achievement_definitions` | Every published revision of every definition | Revisions are immutable; facts and open awards freeze one |
| `achievement_audit` | Operator imports and actions, with operation IDs | Idempotent, replayable administration |
| `achievement_facts` | Authoritative facts waiting to be processed | Crash-safe outbox; pruned after `FactRetentionDays` |
| `achievement_progress` | One row per player and achievement | Reducer state, `EarnedLevel`/`CompletedLevel` cursors, `ScoreEarned` and the short list of open awards |
| `achievement_distinct_values` | Values a player has contributed to a distinct counter | Unbounded, so a fact costs one key lookup |
| `achievement_projections` | One row per player: score, earned levels, publication flag, catalog stamp, last observed state | Hot-path totals and the leaderboard |
| `achievement_wallet_receipts` | One receipt per currency reward | Exactly-once credit, committed with the balance |

Levels deliver strictly in order, so there is no row per level. A level that is earned but not finished is an *open award* on the achievement's progress row. It freezes the definition revision it was earned under and tracks how many rewards are delivered; once the rewards, badge and score are durable it is marked completed, and it is forgotten when the player has been shown it. History per level is deliberately not kept: `CompletedLevel`, `ScoreEarned` and `LastLevelAtUtc` answer what a player has and when they last advanced, and operator actions stay in the audit.

Membership state needs no table of its own. Every grant covers exactly its days with no overlap and nothing shortens a membership, so time spent as a member is `total_days_subscribed` minus what is left of the current run, and purchased days are `purchased_days_subscribed`. The badge a player holds for an achievement is found from the badge family (the code without its level suffix), so no entitlement table is kept.

`human_respect_operations`, `human_respect_participant_receipts`, `pet_respect_operations` and `pet_nutrition_operations` make respect and pet care durable on their own: they guard quota and double credit and let recovery resume a half-finished action. They are created by their own migration, `AddRespectAndPetOperationJournals`. Achievements only attach a fact to the same transaction.

## Definitions and extensions

A hotel's own achievements, and any plugin's, use IDs from 100000. Below that an ID belongs to the [pack](#packs) that declares the range: the Habbo pack uses 1001–1018 for the hand-mapped families and 10000 plus the API ID for every other published record. IDs and keys are permanent. Revision numbers increase; published revisions are immutable. Existing source identity, reducer and level count cannot be reinterpreted or shortened, with one exception: a definition on the placeholder source `catalog.unhooked` (which nothing records) may be moved once to a real source and reducer, because no progress can exist yet. For API-mapped families, seed requirements, categories and states follow the snapshot described below; do not substitute provisional hotel thresholds. Each level declares its cumulative requirement, explicit badge code, score and typed rewards. Badge codes begin with `ACH_` and use a stable base followed by the level number, as required by the standard badge-limit packet. Rewards are hotel policy because the API does not publish them: the current seed assigns 10 score per level and no currency.

Supported reducers are counters, distinct values, maximum values, UTC calendar streaks, elapsed interval unions and rank attainment. Source facts retain their stored units; `UnitDivisor` converts accumulated values for display and threshold comparison. A divisor may change only in a new definition revision. That changes conversion, not stored source facts, and completed awards retain their frozen definition revision. Rank requirements descend and use display method 1 to hide numeric progress. Criteria do not execute arbitrary scripts or SQL.

Plugins register complete typed batches through `IAchievementCatalog.RegisterSources` and `IAchievementRewardRegistry.Register`. Registrations are disposable; collisions reject the entire batch. Sources normalize authoritative successful gameplay into versioned `AchievementFact` records using `IAchievementFactRecorder`. Record facts in the originating database unit of work, or durably before acknowledging accepted transient actions. Stable operation IDs identify one action, not a packet or a mutable before-value. Facts include UTC timestamps and applicable session/interval identities. An admitted fact freezes its definition bindings and remains processable after its producer unloads. A binding is the definition's id and revision: revisions are immutable and stay in `achievement_definitions`, so a fact whose revision the catalog has since replaced is still evaluated against exactly that revision. A fact whose revision is not stored stays unprocessed and is reported until it is restored.

Plugins react to completed levels by registering `IAchievementObserver` through `IAchievementObserverRegistry.Register`. After an award commits (rewards, badge and score all durable), each observer receives an `AchievementLevelCompleted` describing the frozen award. Notification is best effort: it runs off the player's achievement grain, a crash between the commit and the call loses it, a throwing observer is logged without affecting delivery or other observers, and events for one player may arrive out of order (use `Level` and `EarnedAtUtc`). Blocked awards notify nothing until they complete. Use an observer for notifications and soft side effects; anything that must happen exactly once belongs in a reward handler.

Reward handlers implement `IAchievementRewardHandler`. A handler receives a player, immutable award key and versioned payload. It must commit a durable idempotency receipt with its effect, reject payload collisions, and safely replay after a crash. Returning successfully means delivery is durable. Handler unloading blocks pending awards until a compatible handler returns.

## States, retiring and seasons

Every definition has a `State`:

| State | Accrues progress | Who is shown it |
| --- | --- | --- |
| `Disabled` | no | nobody (hidden) |
| `Enabled` | yes, inside its window | everyone |
| `Archived` | no | players who progressed it, in the client's Archive tab (everyone with `ArchiveShowsAll`) |
| `OffSeason` | no | everyone; the client shows it like a normal one |

`WiredControlled` exists in the client but is not supported and is refused. Nothing that was earned is ever taken back: levels, badges and score stay, and archived achievements still count toward the totals. An award already opened, and a fact already recorded, still finish after the state changes.

To stop awarding something, `achievement retire <key>`. It publishes a new revision that is archived, so there is no hand-edited revision number and the change is on record; `unretire` turns it back on (it needs its badge images and texts again, like any enabled achievement), `disable` hides it from everyone and `offseason` marks it off-season. A hotel owner who wants a Habbo achievement gone from the list retires or disables it; there is no delete, because an ID and key are permanent.

An enabled definition can be **seasonal**: `ActiveFromUtc` (inclusive) and `ActiveUntilUtc` (exclusive). Before the window it is hidden, inside it it accrues, and after it it is archived. A fact belongs to the window by when it occurred, and the achievement it counts toward is frozen with the fact. Time-based progress (online time, streaks) does not accrue while an achievement is not accruing, so turning one back on starts counting from then.

## Adding your own achievements without code

A hotel owner defines an achievement in data over the facts the hotel already records. You write a JSON file, run `achievement sync`, and the catalog is made to match it: a new key is created, a definition that differs gets the next revision (never number one yourself), one that already matches is left alone, and a key the file omits is only reported, never retired. Validation runs first and lists every problem at once; nothing is applied while there is one, and the rest is one atomic, audited import.

The dry run also tells you what the client still needs, ready to paste. For a new category, a name text; for every level of a listed achievement, a badge image and a name and description text. A new category needs nothing else: the client builds its categories from whatever the server sends, and has no category icon.

```text
quests.christmas.name=Christmas
badge_name_ACH_TwelveDaysOfChristmas=Twelve Days of Christmas
badge_desc_ACH_TwelveDaysOfChristmas=Log in on %limit% of the 12 days of Christmas.
```

One badge name and description under the badge's base covers every level; a text for an exact level code overrides it. Put the images in `Turbo:Achievements:BadgeAssetDirectory` as `<badge code>.png` for every level, and serve the same files and texts to Nitro.

### Example: log in on 12 days of Christmas

[`examples/achievements/twelve-days-of-christmas.json`](examples/achievements/twelve-days-of-christmas.json) is a complete definition. It listens to logins, counts the distinct UTC days (`"Match": { "ValueFrom": "UtcDate" }`, so two logins on one day count once), awards at 3, 6, 9 and 12 days, and is only live from 25 December to 6 January:

```text
achievement sync docs/examples/achievements/twelve-days-of-christmas.json
achievement sync docs/examples/achievements/twelve-days-of-christmas.json --apply christmas-2026 Add the Christmas achievements
```

[`examples/achievements/visit-the-christmas-rooms.json`](examples/achievements/visit-the-christmas-rooms.json) counts how many of three named rooms a player has entered (`"Match": { "Values": ["1001", "1002", "1003"] }`). Both files are run by the test suite exactly as written, so they stay correct.

### What a definition can count

A definition picks a source (the kind of fact) and a reducer (how to count it). A source allows its usual reducer and the others listed:

| Source | Records | Reducers |
| --- | --- | --- |
| `identity.login` | each login | `CalendarStreak`, `Counter` (total logins), `Distinct` (with `UtcDate`: distinct days) |
| `explore.admitted-room` | entering another player's room; value is the room id | `Distinct`, `Counter` |
| `presence.online` | online intervals | `ElapsedSeconds` |
| `identity.account-age`, `membership.eligible-seconds`, `membership.purchased-days`, `pets.owned`, `builder.floor-heights` | a state value | `Maximum` |
| `identity.figure-change`, `identity.motto-change`, `explore.furniture-use`, `social.respect-given`, `social.respect-received`, `pets.nutrition-supplied`, `pets.level-increase`, `pets.respect-given`, `pets.respect-received` | each action | `Counter` |
| `builder.room-rank` | a room's ranking | `Rank` |
| `catalog.unhooked` | nothing | any, but never enabled |

`Match` narrows what is counted without running anything: `Values` lists the exact fact values to listen to (such as room ids), and `ValueFrom` chooses what a distinct achievement counts, the fact's own value or the UTC date it happened on. Furniture use carries no item information yet, so a furniture-specific achievement needs a plugin that records its own fact. Criteria never run scripts or SQL.

## Packs

A pack is a set of definitions that ships together and owns a range of IDs. The Habbo catalog is one; a plugin can ship another. Installing a pack only adds: a definition whose key or ID the hotel already has is left exactly as it is. Pack IDs are below 100000 and packs may not overlap each other; IDs from 100000 are the hotel's own.

The Habbo pack carries every published record the pinned snapshot can express: the 18 hand-mapped families plus 150 more, with their real categories and thresholds. Nothing in the hotel records the facts most of them need yet, so those ship `Disabled` (retired ones `Archived`) on the placeholder source `catalog.unhooked`. You can keep, edit, retire or re-goalpost any of them, and point one at a source something records (a plugin, or a later release) before enabling it; Turbo refuses to enable one nothing feeds. `DailyHotelPresence` (no levels in the API) and `RecycledItems` (thresholds that do not rise) cannot be shipped faithfully and are listed in the [coverage CSV](achievement-coverage.csv). Badge codes of names that end in a digit take an underscore before the level (`ACH_bazaar17_1`), because the client reads trailing digits as the level.

## For plugins

A plugin adds achievements in three ways, and none needs a database context:

- **Record a fact** when something finishes that a definition can count. `IAchievementFacts.RecordAsync` stores it, does nothing if that operation was already recorded or nothing listens, and asks for the player's progress to be updated without making the caller wait, so it is safe to call from a grain.

```csharp
await facts.RecordAsync(
    playerId,
    new AchievementFact
    {
        OperationId = $"heist:{heistId}:{playerId}", // one id per action: recording it again does nothing
        Source = "myplugin.heist-completed",          // a source the plugin registered
        OccurredAtUtc = DateTime.UtcNow,
    },
    ct);
```

- **Register a source** the plugin records, with `IAchievementCatalog.RegisterSources`, and tell hotel owners what definitions can use it. Code that changes the database itself should record the fact in the same unit of work with `IAchievementFactRecorder` instead, so it commits or rolls back with the change.
- **Ship definitions** as a pack (`IAchievementPackRegistry.Register`, then `achievement reload`), claiming a range of IDs below 100000 that no other pack uses, or just give hotel owners a JSON file to `achievement sync`.

Reward handlers and observers are described below.

## Published coverage and hotel extensions

The 15 API records mapped to the stable seed IDs are shown below. Exact published thresholds are retained in `achievement-coverage.csv`; its `Published_RequiredScore` column preserves each API sequence. API state and category are listed as returned on 2026-10-02. An `ENABLED` API state does not prove that a corresponding gameplay hook exists in this server.

| Hotel ID / key | API ID / name | API category / state | Levels | Mapping |
| --- | --- | --- | ---: | --- |
| 1001 online | 19 / AllTimeHotelPresence | identity / ENABLED | 20 | Durable online intervals; display conversion follows the published values |
| 1002 login | 4 / Login | identity / ENABLED | 20 | Local login source; API metadata alone does not define streak eligibility |
| 1003 account-age | 11 / RegistrationDuration | identity / ENABLED | 20 | Account creation duration |
| 1004 figure | 6 / AvatarLooks | identity / ENABLED | 1 | Successfully persisted figure changes |
| 1005 motto | — | hotel extension | 1 | Successfully persisted motto changes; absent from API |
| 1006 hc-duration | 163 / VipHC | identity / ENABLED | 5 | Published thresholds `0,12,24,36,48` months; use the 31-day month convention; eligibility gates the zero-threshold first level |
| 1007 purchased-hc | 289 / HC | identity / ENABLED | 5 | Published sequence is `14,360,720,1080,1440`; API does not define the server fact hook or unit |
| 1008 rooms-visited | 8 / RoomEntry | explore / ENABLED | 20 | Distinct successfully admitted other-owner rooms |
| 1009 furniture-use | 291 / HabboExplorer | explore / ENABLED | 1 | Successful permitted furniture use |
| 1010 respect-given | 18 / RespectGiven | social / ENABLED | 20 | Completed durable human-respect operation |
| 1011 respect-received | 17 / RespectEarned | social / ENABLED | 10 | Same completed operation, attributed to recipient |
| 1012 pets-owned | 23 / PetLover | pets / ENABLED | 10 | Current owned pets across inventory and rooms |
| 1013 pet-nutrition | 25 / PetFeeding | pets / ENABLED | 20 | Actual capped nutrition supplied |
| 1014 pet-levels | 24 / PetLevelUp | pets / ENABLED | 10 | Successfully persisted pet level increases |
| 1015 pet-respect-given | 26 / PetRespectGiver | pets / ENABLED | 20 | Completed durable pet-respect operation |
| 1016 pet-respect-received | 27 / PetRespectReceiver | pets / ENABLED | 10 | Same completed operation, attributed to pet owner |
| 1017 floor-heights | — | hotel extension | — | Distinct walkable heights; disabled while required hotel badge assets are unavailable |
| 1018 room-rank | — | enabled hotel extension | 9 | Positive-vote eligible room ranking; no equivalent API record |

The API record `BasicClub` (#32, `identity`, `ARCHIVED`) is retired and is not the source for hotel ID 1006. `VipHC` (#163) is the enabled record used there. `DailyHotelPresence` (#14) is enabled but has no `levelRequirements` in the snapshot, so it is not mapped as a leveled seed family. The similarly named `HabboBuilder_TSale_` and archived design records are not the floor-height criterion in ID 1017.

The remaining API records are listed in the coverage CSV with their exact state and thresholds. Unmapped rows identify that a verified authoritative hook is missing or has not been established here; the table does not claim a complete audit of every possible server path. Game achievements need a mode-specific completed result and eligible-player attribution, which generic room or Wired activity cannot establish. Gift achievements require successful purchase and delivery facts, including the receiver; opening a present alone is insufficient. Do not infer historical action totals from present state.

## Administration and recovery

The `command.achievements` permission gates `achievements`. The normal command registry generates its help. Arguments are player selector, action (`inspect`, `advance`, `reconcile`, `retry`), achievement ID, absolute progress, operation ID and remaining text as audit reason. Supply zero ID/progress placeholders for actions that do not use them. `advance` only moves progress forward; rank changes require authoritative rank facts. Reconcile evaluates recorded progress and current authoritative state. It cannot invent past actions, alter frozen rewards or revoke levels. Retry resumes durable pending rewards. Audits retain actor, reason, operation ID, request and before/after state.

Progress, crossed awards and fact consumption commit atomically. An earned level freezes the definition revision it was earned under, which stays readable in `achievement_definitions`, so a later catalog cannot change what was promised. Levels deliver in order within an achievement; blocked awards do not stop unrelated achievements. Unknown handlers/currencies, invalid payloads and overflow are visible through pending-award inspection and server logs. Repair the handler or mapping and retry; never edit an open award to pretend delivery succeeded.

Wallet balances and award receipts commit together. A player holds one badge per achievement, the level they are at: reaching a level removes every lower level of that achievement from the inventory, manual or earned, and a worn lower level hands its slot to the new one. A higher level the player already owns is kept instead. Accounts that still own every earlier level are collapsed to their highest the next time the achievement list is requested. Badge directory refreshes are replayable. Owned levels are recognised by their badge prefix, which a validated catalog keeps stable and unique per achievement, so a new revision must not rename the prefix of an achievement players already hold: the old badge would be left behind. Human and pet respect use durable participant receipts and completion journals. Recovery rotates through pending operations and players, including offline players. The achievement list request is read-only. Login reconciles a player: state values (account age, owned pets, membership, floor heights, room rank) are recorded as facts only when they moved, and the badge cleanup and the re-evaluation of retained progress run only when the catalog revisions differ from the player's stored stamp (an operator's `reconcile` forces both and recomputes the totals). The score and earned-level totals are kept incrementally as awards complete and recomputed from each achievement's completed levels and frozen scores whenever a player is evaluated against a changed catalog. Level-up presentation is not polled: an offline player is shown their completed awards when the achievement list is requested on their next session. Completed rewards and score projections recover independently of a connected client.

Pet return and room flush serialize their read/write units, preserve monotonic level, experience and respect, and record level gains in the same transaction even when pickup happens before the next flush. Returns retry deadlocks and lock timeouts in fresh transactions up to three attempts. Pickup and room deletion keep avatars until the inventory write succeeds. Composting waits for admitted pet respect to complete so its recipient cannot disappear after quota has been spent.

Achievement score is the sum of completed frozen score rewards. Achievement level is the sum of each achievement's highest completed level, including archived achievements. Login, profile and room consumers receive the appropriate projection. Leaderboard type 2 ranks earned levels, independently of score and manually owned badges.

Nitro requests the list once per session; unsolicited data does not open the window. It uses cumulative level offsets and final-level handling, category filtering/order, unseen IDs, account resets and queued two-second level transitions. Inventory and notifications coordinate badge updates. Client presentation deduplication is session-local; server receipts protect durable rewards across sessions and restarts.

## Verification scope

Shared packet fixtures cover lists, updates, score, level-up fields, cumulative offsets and final levels. Backend tests exercise reducers, authoritative state, failed profile writes, respect coordination, nutrition caps, reward retries, frozen revisions, separate score/level totals, badge ownership and wallet replay. Client checks cover state, categories, links, exclusions, transition timing, notification flags and account changes. Live verification uses Turbo-issued tickets and a dedicated development account. Database migrations and imports should always be dry-run reviewed before applying to another hotel.
