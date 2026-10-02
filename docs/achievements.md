# Achievements

Achievements process immediately after a validated catalog is imported. There is no activation date, baseline or historical-action backfill. Account age uses account creation time; online time credits only durably recorded intervals. Reloading definitions and restarting Turbo preserve progress and awards.

AS3 defines packet order and client behavior. Thresholds in `AchievementDefaults` are editable hotel policy where authoritative server thresholds are unavailable. The official JavaScript client provides implementation guidance. Standard and custom definitions use the same progression and reward pipeline.

## Install and configure

Apply migration `20261002120020_AddAchievements` through the normal EF migration workflow. Existing badge rows migrate as manual ownership, so achievement upgrades cannot revoke them.

Configure `Turbo:Achievements:BadgeAssetDirectory` to the directory containing the hotel's badge PNGs. The hotel's configured external texts must contain each enabled badge's name and description, either its exact code or its numeric-level base, matching AS3 localization lookup. Serve the same assets and texts to Nitro.

In the Turbo console:

```text
achievement defaults achievements.json
achievement import achievements.json
achievement import achievements.json --apply install-achievements-v1 Install validated hotel defaults
achievement export current-achievements.json
reload achievements
```

Import is a dry run unless `--apply` is supplied with a stable operation ID and reason. It validates the complete resulting catalog before one atomic publication. Failed reloads retain the working catalog. Import operations are audited and replayable; reusing an operation ID with different content is rejected. Keep exported catalogs under hotel version control.

Configuration defaults:

| Setting | Default | Meaning |
| --- | --- | --- |
| `Turbo:Achievements:RecoverySeconds` | 5 | Persistent dispatcher poll interval |
| `Turbo:Achievements:RecoveryBatchSize` | 100 | Players/operations per recovery batch |
| `Turbo:Achievements:FactBatchSize` | 100 | Facts consumed in one player turn |
| `Turbo:Achievements:MaxDefinitions` | 1000 | Catalog size limit |
| `Turbo:Achievements:MaxDistinctValues` | 100000 | Distinct reducer storage limit |
| `Turbo:Achievements:DefaultCategory` | identity | List packet's default category |
| `Turbo:Achievements:ShowCongratulationsDialog` | true | Level-up dialog flag; corner notifications remain enabled |
| `Turbo:Players:AchievementOnlineIntervalSeconds` | 30 | Durable online checkpoints; also flushed on clean disconnect |

## Definitions and extensions

IDs 1001–1018 belong to the seed families. Custom IDs begin at 100000. IDs and keys are permanent. Revision numbers increase; published revisions are immutable. Existing source identity, reducer, units and level count cannot be reinterpreted or shortened. Each level declares its cumulative requirement, explicit badge code, score and typed rewards. Badge codes begin with `ACH_` and use a stable base followed by the level number, as required by the standard badge-limit packet. The defaults grant 10 score per level and no currency.

Supported reducers are counters, distinct values, maximum values, UTC calendar streaks, elapsed interval unions and rank attainment. `UnitDivisor` converts stored units into display units, for example seconds into minutes. Rank requirements descend and use display method 1 to hide numeric progress. Criteria do not execute arbitrary scripts or SQL.

Plugins register complete typed batches through `IAchievementCatalog.RegisterSources` and `IAchievementRewardRegistry.Register`. Registrations are disposable; collisions reject the entire batch. Sources normalize authoritative successful gameplay into versioned `AchievementFact` records using `IAchievementFactRecorder`. Record facts in the originating database unit of work, or durably before acknowledging accepted transient actions. Stable operation IDs identify one action, not a packet or a mutable before-value. Facts include UTC timestamps and applicable session/interval identities. An admitted fact freezes its definition bindings and remains processable after its producer unloads.

Reward handlers implement `IAchievementRewardHandler`. A handler receives a player, immutable award key and versioned payload. It must commit a durable idempotency receipt with its effect, reject payload collisions, and safely replay after a crash. Returning successfully means delivery is durable. Handler unloading blocks pending awards until a compatible handler returns.

## Initial coverage

| ID / key | Category | Authoritative qualifying behavior | Status |
| --- | --- | --- | --- |
| 1001 online | identity | Session intervals checkpointed every 30 seconds and on clean disconnect; only durable time survives crashes | Enabled, 20 minute levels |
| 1002 login | identity | Successfully committed login on UTC days; longest consecutive streak retained | Enabled, 20 day levels |
| 1003 account-age | identity | Completed UTC duration from account creation on current-state evaluation | Enabled, 20 day levels |
| 1004 figure | identity | Successfully persisted actual figure change; same figure and gender-only changes do not count | Enabled, one action |
| 1005 motto | identity | Successfully persisted actual motto change | Enabled, one action |
| 1006 hc-duration | identity | Union of recorded eligible membership intervals, preserving gaps | Enabled, five day levels |
| 1007 purchased-hc | identity | Eligible membership intervals originating from successful purchases | Enabled, five day levels |
| 1008 rooms-visited | explore | Distinct successfully admitted other-owner rooms; denied entry and own rooms excluded | Enabled, 20 count levels |
| 1009 furniture-use | explore | Permitted player furniture use that changes the item's state; rejected and no-op uses excluded | Enabled, one action |
| 1010 respect-given | social | Durable coordinated human-respect spend and recipient credit | Enabled, ten count levels |
| 1011 respect-received | social | Same completed human-respect operation, attributed to recipient | Enabled, ten count levels |
| 1012 pets-owned | pets | Current owned pets across inventory and rooms, without double counting | Enabled, ten count levels |
| 1013 pet-nutrition | pets | Actual capped nutrition gain persisted with fact; hand-feed actor or food-bowl supplier receives credit | Enabled, ten nutrition levels |
| 1014 pet-levels | pets | Successfully persisted pet level increases, attributed to owner | Enabled, ten count levels |
| 1015 pet-respect-given | pets | Durable actor quota receipt plus completed pet respect mutation | Enabled, ten count levels |
| 1016 pet-respect-received | pets | Same completed operation, attributed to pet owner | Enabled, ten count levels |
| 1017 floor-heights | room_builder | Distinct walkable heights in current owned room models, evaluated on load and committed floor-plan changes | Disabled: five `ACH_HabboBuilder` PNG assets are unavailable |
| 1018 room-rank | room_builder | Positive-vote eligible Navigator rooms ordered by score and room ID; initial/reload evaluation and committed ratings refresh affected ranks | Enabled, nine descending rank levels |

Count families use cumulative requirements `1,2,3,5,10,15,20,30,50,75,100,150,200,300,500,750,1000,1500,2000,3000`; ten-level families use the first ten. Day requirements are `1,2,3,5,7,10,14,21,30,45,60,90,120,180,270,365,540,730,1095,1825`. Online minutes are `5,15,30,60,120,180,300,600,1200,1800,3000,6000,9000,12000,18000,24000,36000,48000,72000,100000`. Nutrition requirements are `10,25,50,100,250,500,1000,2500,5000,10000`. Active HC days are `1,30,90,180,365`; purchased HC days are `30,60,90,180,360`. Floor heights are `2,3,4,5,6`; room ranks are top `2000,1000,500,250,100,50,10,5,1`.

Unsupported game-specific families remain absent/disabled: Banzai, Freeze and SnowStorm require their own authoritative gameplay, results and durable participation records. Generic Wired games do not establish those outcomes. Complete gifting achievements require purchase, delivery and recipient coordination; opening a present alone is insufficient. Trade, marketplace, crafting, friends, group, talent, guide/helper, quest and other families need verified successful mutation hooks, eligibility rules, assets and thresholds before import. Do not fabricate historical counts from present state.

## Administration and recovery

The `command.achievements` permission gates `achievements`. The normal command registry generates its help. Arguments are player selector, action (`inspect`, `advance`, `reconcile`, `retry`), achievement ID, absolute progress, operation ID and remaining text as audit reason. Supply zero ID/progress placeholders for actions that do not use them. `advance` only moves progress forward; rank changes require authoritative rank facts. Reconcile evaluates recorded progress and current authoritative state. It cannot invent past actions, alter frozen rewards or revoke levels. Retry resumes durable pending rewards. Audits retain actor, reason, operation ID, request and before/after state.

Progress, crossed awards and fact consumption commit atomically. Awards freeze their definition revision and reward payload. Levels deliver in order within an achievement; blocked awards do not stop unrelated achievements. Unknown handlers/currencies, invalid payloads and overflow are visible through pending-award inspection and server logs. Repair the handler or mapping and retry; never edit a frozen award to pretend delivery succeeded.

Wallet balances and award receipts commit together. Achievement badge entitlements are separate from manual grants; upgrading removes only that achievement's prior entitlement and preserves independent ownership and worn slots. Badge directory refreshes are replayable. Human and pet respect use durable participant receipts and completion journals. Recovery rotates through pending operations and players, including offline players. Completed rewards and score projections recover independently of a connected client.

Pet return and room flush serialize their read/write units, preserve monotonic level, experience and respect, and record level gains in the same transaction even when pickup happens before the next flush. Returns retry deadlocks and lock timeouts in fresh transactions up to three attempts. Pickup and room deletion keep avatars until the inventory write succeeds. Composting waits for admitted pet respect to complete so its recipient cannot disappear after quota has been spent.

Achievement score is the sum of completed frozen score rewards. Achievement level is the sum of each achievement's highest completed level, including archived achievements. Login, profile and room consumers receive the appropriate projection. Leaderboard type 2 ranks earned levels, independently of score and manually owned badges.

Nitro requests the list once per session; unsolicited data does not open the window. It uses cumulative level offsets and final-level handling, category filtering/order, unseen IDs, account resets and queued two-second level transitions. Inventory and notifications coordinate badge updates. Client presentation deduplication is session-local; server receipts protect durable rewards across sessions and restarts.

## Verification scope

Shared packet fixtures cover lists, updates, score, level-up fields, cumulative offsets and final levels. Backend tests exercise reducers, authoritative state, failed profile writes, respect coordination, nutrition caps, reward retries, frozen revisions, separate score/level totals, badge ownership and wallet replay. Client checks cover state, categories, links, exclusions, transition timing, notification flags and account changes. Live verification uses Turbo-issued tickets and a dedicated development account. Database migrations and imports should always be dry-run reviewed before applying to another hotel.
