# Achievements

Achievements process immediately after a validated catalog is imported. There is no activation date, baseline or historical-action backfill. Account age uses account creation time; online time credits only durably recorded intervals. Reloading definitions and restarting Turbo preserve progress and awards.

The official [Habbo achievements API](https://www.habbo.com/api/public/achievements) is the source of truth for published achievement names, categories, states and `requiredScore` thresholds. The snapshot retrieved on 2026-10-02 contains 167 records. The API does not define rewards, reducer semantics, source units, gameplay hooks, eligibility or attribution. The accompanying [coverage CSV](achievement-coverage.csv) records every snapshot row and the local mapping decision; it also lists three hotel extensions absent from the API. AS3 defines packet order and client behavior, while the official JavaScript client provides implementation guidance.

## Install and configure

Apply migration `20261002153246_AddAchievements` through the normal EF migration workflow. It only adds tables: nothing existing is altered, and distinct-value progress (rooms visited, floor heights) lives in `achievement_distinct_values`. A hotel with no achievement definitions gets the shipped Habbo catalog installed on startup through the normal audited import (operation `install-habbo-defaults-2026-10-02`); that needs `Turbo:Achievements:BadgeAssetDirectory` and the badge texts, and otherwise leaves the catalog empty with a warning that names the manual import. A hotel that already has definitions is never touched, and `Turbo:Achievements:InstallDefaults` set to `false` turns the install off.

Configure `Turbo:Achievements:BadgeAssetDirectory` to the directory containing the hotel's badge PNGs. The hotel's configured external texts must contain each enabled badge's name and description, either its exact code or its numeric-level base, matching AS3 localization lookup. Serve the same assets and texts to Nitro.

Nitro's `badge.asset.url` must resolve every enabled badge, including levels 11–20. Standard Habbo badges are available at `https://images.habbo.com/c_images/album1584/%badgename%.gif`; configure your hotel's asset endpoint for custom badges. Server-side PNG validation does not establish that the client's configured image endpoint serves those assets. A development URL query override can set `badge.asset.url` without changing the shared client configuration.

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
| `Turbo:Achievements:InstallDefaults` | true | Install the shipped Habbo catalog when the hotel has no definitions |
| `Turbo:Achievements:FactRetentionDays` | 30 | Days a processed fact is kept for idempotent admission before it is deleted; `0` keeps them |
| `Turbo:Achievements:MaxDefinitions` | 1000 | Catalog size limit |
| `Turbo:Achievements:MaxDistinctValues` | 100000 | Distinct reducer storage limit |
| `Turbo:Achievements:DefaultCategory` | identity | List packet's default category |
| `Turbo:Achievements:ShowCongratulationsDialog` | true | Level-up dialog flag; corner notifications remain enabled |
| `Turbo:Players:AchievementOnlineIntervalSeconds` | 30 | Durable online checkpoints; also flushed on clean disconnect |

## Definitions and extensions

IDs 1001–1018 belong to the seed families. Custom IDs begin at 100000. IDs and keys are permanent. Revision numbers increase; published revisions are immutable. Existing source identity, reducer and level count cannot be reinterpreted or shortened. For API-mapped families, seed requirements, categories and states follow the snapshot described below; do not substitute provisional hotel thresholds. Each level declares its cumulative requirement, explicit badge code, score and typed rewards. Badge codes begin with `ACH_` and use a stable base followed by the level number, as required by the standard badge-limit packet. Rewards are hotel policy because the API does not publish them: the current seed assigns 10 score per level and no currency.

Supported reducers are counters, distinct values, maximum values, UTC calendar streaks, elapsed interval unions and rank attainment. Source facts retain their stored units; `UnitDivisor` converts accumulated values for display and threshold comparison. A divisor may change only in a new definition revision. That changes conversion, not stored source facts, and completed awards retain their frozen definition revision. Rank requirements descend and use display method 1 to hide numeric progress. Criteria do not execute arbitrary scripts or SQL.

Plugins register complete typed batches through `IAchievementCatalog.RegisterSources` and `IAchievementRewardRegistry.Register`. Registrations are disposable; collisions reject the entire batch. Sources normalize authoritative successful gameplay into versioned `AchievementFact` records using `IAchievementFactRecorder`. Record facts in the originating database unit of work, or durably before acknowledging accepted transient actions. Stable operation IDs identify one action, not a packet or a mutable before-value. Facts include UTC timestamps and applicable session/interval identities. An admitted fact freezes its definition bindings and remains processable after its producer unloads. A binding is the definition's id and revision: revisions are immutable and stay in `achievement_definitions`, so a fact whose revision the catalog has since replaced is still evaluated against exactly that revision. A fact whose revision is not stored stays unprocessed and is reported until it is restored.

Reward handlers implement `IAchievementRewardHandler`. A handler receives a player, immutable award key and versioned payload. It must commit a durable idempotency receipt with its effect, reject payload collisions, and safely replay after a crash. Returning successfully means delivery is durable. Handler unloading blocks pending awards until a compatible handler returns.

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

Progress, crossed awards and fact consumption commit atomically. Awards freeze their definition revision and reward payload. Levels deliver in order within an achievement; blocked awards do not stop unrelated achievements. Unknown handlers/currencies, invalid payloads and overflow are visible through pending-award inspection and server logs. Repair the handler or mapping and retry; never edit a frozen award to pretend delivery succeeded.

Wallet balances and award receipts commit together. A player holds one badge per achievement, the level they are at: reaching a level removes every lower level of that achievement from the inventory, manual or earned, and a worn lower level hands its slot to the new one. A higher level the player already owns is kept instead. Accounts that still own every earlier level are collapsed to their highest the next time the achievement list is requested. Badge directory refreshes are replayable. Human and pet respect use durable participant receipts and completion journals. Recovery rotates through pending operations and players, including offline players. The achievement list request is read-only. Login reconciles a player: state values (account age, owned pets, membership, floor heights, room rank) are recorded as facts only when they moved, and the badge cleanup and the re-evaluation of retained progress run only when the catalog revisions differ from the player's stored stamp (an operator's `reconcile` forces both and recomputes the totals). The score and earned-level totals are kept incrementally as awards complete and recomputed from the completed awards whenever a player is evaluated against a changed catalog. Level-up presentation is not polled: an offline player is shown their completed awards when the achievement list is requested on their next session. Completed rewards and score projections recover independently of a connected client.

Pet return and room flush serialize their read/write units, preserve monotonic level, experience and respect, and record level gains in the same transaction even when pickup happens before the next flush. Returns retry deadlocks and lock timeouts in fresh transactions up to three attempts. Pickup and room deletion keep avatars until the inventory write succeeds. Composting waits for admitted pet respect to complete so its recipient cannot disappear after quota has been spent.

Achievement score is the sum of completed frozen score rewards. Achievement level is the sum of each achievement's highest completed level, including archived achievements. Login, profile and room consumers receive the appropriate projection. Leaderboard type 2 ranks earned levels, independently of score and manually owned badges.

Nitro requests the list once per session; unsolicited data does not open the window. It uses cumulative level offsets and final-level handling, category filtering/order, unseen IDs, account resets and queued two-second level transitions. Inventory and notifications coordinate badge updates. Client presentation deduplication is session-local; server receipts protect durable rewards across sessions and restarts.

## Verification scope

Shared packet fixtures cover lists, updates, score, level-up fields, cumulative offsets and final levels. Backend tests exercise reducers, authoritative state, failed profile writes, respect coordination, nutrition caps, reward retries, frozen revisions, separate score/level totals, badge ownership and wallet replay. Client checks cover state, categories, links, exclusions, transition timing, notification flags and account changes. Live verification uses Turbo-issued tickets and a dedicated development account. Database migrations and imports should always be dry-run reviewed before applying to another hotel.
