# Inventory Rules

- [Inventory sections](#inventory-sections)
- [Unseen items](#unseen-items)
- [Badges](#badges)
- [Trading](#trading)

Rules for inventory sections, unseen items, badges and trading.

## Inventory sections
- Furniture, pets and bots are three modules of `InventoryGrain` with one shape
  (`Inventory{Furni,Pet,Bot}Module`): `EnsureReadyAsync` loads the section on first use,
  `GetAsync` / `GetAllAsync` read it, and every change writes the row before the list and then
  tells the presence. The grain partials only forward; put behaviour in the module. A section
  lists only what is in no room.
- **Pets and bots are one flow.** `InventoryUnitModule<TEntity, TSnapshot>` loads, hands over to
  a room, takes back, creates (under the owned cap) and deletes both; `InventoryPetModule` and
  `InventoryBotModule` say only what differs — the table, the snapshot, the cap, the columns a
  unit brings back from a room, the presence calls, and their catalog product. The entities
  share `IInventoryUnitEntity`, the snapshots `IInventoryUnitSnapshot`, the live state
  `InventoryUnitSection<T>`. The two modules were two hundred lines copied; a third kind of
  room-placed unit is a subclass, not a copy. (EF translates the interface-typed `Where`,
  `ExecuteUpdate` and `ExecuteDelete` in the base to plain column SQL; checked against MySQL.)
- Inventory items are built by `IInventoryFurnitureLoader` and nowhere else, so loaded, granted,
  picked-up and traded items read their extra data and stuff data the same way.
- Tell the client once per change, not once per item: an addition is one "list changed", a
  removal one batch of removals (`InventoryFurniModule.SendAddedAsync` / `SendRemovedAsync`), and
  a grant of N items is one insert. The
  owner name comes from `InventoryGrain.GetOwnerNameAsync`, cached per activation.
- Ownership caps (`MaxPets`, `MaxBots`) count rows, placed or not, in the same context as the
  insert. A catalog purchase validates every product before it creates anything
  (`ValidateProduct` then `GrantProductAsync`).

## Unseen items
- What a player received and has not looked at yet is `IPlayerUnseenItemsGrain`
  (`GetPlayerUnseenItemsGrain`), per inventory tab (`UnseenItemCategory`: owned furni, rented
  furni, pets, badges, bots — the client's numbers). Kept in `player_unseen_items`, so a gift or
  trade that arrived while the player was offline is still new at login (sent in the login
  burst); a new arrival is sent to the client at once.
- **Who marks what.** Whatever gives the player something new tells the grain, never awaiting
  it: furni grants and teleporter pairs and trade receipts (`InventoryFurniModule.MarkUnseen`),
  a new pet or bot (`InventoryUnitModule.CreateAsync`), a new badge (`PlayerBadgeGrain`). A
  pick-up from a room, or a pet or bot coming back from one, is not new — do not mark it. A new
  way to receive something marks it too.
- The ids are the client's: a furni's item id (not the signed ref), a pet's or bot's id, a
  badge's row id.
- **Stale ids are the grain's to drop.** The client resets a whole tab when it is opened, but
  drops a single new item on its own without telling the server, and an item can be placed,
  traded or sold while still new. So the grain prunes its rows against what the player still
  holds in the inventory whenever it loads, and caps each tab
  (`InventoryConfig.MaxUnseenItemsPerCategory`). It reads the inventory's tables and writes
  only its own.

## Badges
- A badge is a code a player owns (`player_badges`); it needs no definition to exist. A player's
  badges are their own grain, `IPlayerBadgeGrain` (`GetPlayerBadgeGrain(playerId)`), and every
  grant, removal and "wear these" goes through it; do not add a second way to give one. They
  were a section of the inventory until it was clear rooms read a player's worn badges on every
  entry: that read queued behind whatever the inventory was doing — loading thousands of furni,
  a trade, a purchase — and the room's whole turn waited with it. The badge grain's reads are
  `[AlwaysInterleave]`; nothing it awaits calls it back.
- Owner count and rarity are the hotel's, not the player's. They live in `BadgeDirectoryGrain`
  (one grain: owner counts per code, recounted on a timer and adjusted on each grant) and are
  filled into a `PlayerBadgeSnapshot` on every read. The badge grain never stores them, because a
  copy would go stale the moment someone else got the badge.
- Because every badge shown anywhere asks the directory, it answers from memory only; its one
  query is the recount. Work that queries per request lives in `BadgeLeaderboardGrain`
  (`IBadgeLeaderboardGrain`), which asks the directory which codes are of a tier
  (`GetCodesOfRarityAsync`). The calls go one way: leaderboard → directory, never back.
- The client only draws the rarity it is sent (`BadgeRarityType` mirrors its `BadgeRarity`
  ids). How an owner count becomes a tier is therefore hotel data: the owner-share limits in
  `BadgeConfig`, with `MinimumPlayersForRarity` keeping a small hotel from calling everything
  unique. A `badge_definitions` row pins a rarity regardless of owners (staff badges).
- Every badge on the wire carries `ownerCount` and `badgeRarityId` (`Badges`, `BadgeReceived`,
  `BadgeInfo`, `HabboUserBadges`). The `Badges` list does not say what is worn: the client
  learns that from its own `HabboUserBadges`, so the two are sent together.
- A change to what is worn reaches the room through the presence
  (`OnSelectedBadgesChangedAsync` → `IRoomGrain.SetPlayerBadgesAsync`, told with `LogAndForget`),
  which updates the codes the wired "wearing badge" condition reads and broadcasts
  `HabboUserBadges`, since the client's room handler listens for it for every user.
- Leaderboards (`BadgeLeaderboardType`: total badges, one rarity tier, achievement level) are
  held by `BadgeLeaderboardGrain` as a score histogram plus the top `LeaderboardHeldEntries`
  entries per board, refreshed on an interleaved timer; requests are answered from memory, and
  only a chunk deeper than the held entries queries. Players on the same score share a rank. The
  achievement board is empty until achievements exist. Type, tier, chunk index and size all
  come from the client and are bounded in the leaderboard grain.
- A player's badges rank (info stand, profile) is asked of the leaderboard grain with the number
  of badges, not the player: it keeps how many players hold each total (the histogram,
  refreshed on the timer) and answers from that, so ranking every avatar in
  every room costs no query. The client shows a rank of zero or more and hides a negative one;
  no badges, bots and "not known yet" are `BadgeRanks.NONE`.
- The rank is worked out where the badge count is, in the badge grain
  (`IPlayerBadgeGrain.RefreshBadgesRankAsync`): when a badge is given or removed, and when the
  presence asks on room entry, because a rank also moves when other players get badges. The
  badge grain *tells* the player grain (`IPlayerGrain.SetBadgesRankAsync`, `LogAndForget`), which
  keeps it for `PlayerSummarySnapshot.BadgesRank`; a changed rank goes presence → room as a
  `UserChange`. A first entry after activation therefore shows no rank for a moment and then
  the rank. Do not make `GetSummaryAsync` fetch it: that call is on every hot path.
- The player grain never awaits the badge grain (badge grain → presence → player grain is
  already a chain). So a profile's badge figures do not pass through it: `PlayerService`
  reads `GetExtendedProfileSnapshotAsync` and `IPlayerBadgeGrain.GetBadgeSummaryAsync` side by
  side (`GetExtendedProfileAsync`) and the composer carries both.
- A client may claim a badge only through a request code the hotel lists
  (`BadgeConfig.RequestableBadges`); the badge code itself is never taken from the client.

## Trading
- Trades live in `RoomTradeGrain` (`IRoomTradeGrain`, keyed by room id), not in the room grain:
  adding items reads an inventory and the commit writes two, and none of that may hold up the
  room's turn. Handlers talk to the trade grain only. The room keeps the two things only it
  knows (`RoomTradeModule`): who the clicked avatar is and whether the room's trade mode lets
  each side trade (`GetTradePartiesAsync`), and the trading status on the avatars
  (`SetTradingStatusAsync`).
- The calls go one way. The trade grain awaits the room; the room never awaits the trade grain.
  When a trading player leaves, the room fires `CloseForPlayerAsync` with `LogAndForget` and
  carries on. Awaiting it would deadlock, because ending a trade calls the room back to clear
  the status.
- Open trade conversations are not persisted and reserve nothing. On submission, freeze both
  offers and one operation identity. The initiating inventory commits both offers through
  `IFurnitureCustody.ExchangeTradeAsync`: exact accepted state is checked under row locks, and
  both owner changes, movements and a request-bound receipt commit together. Resolve the receipt
  before validating old ownership on retry; never compensate an ambiguous response.
  Present opening locks the same wrapper and checks its current owner before releasing contents.
- The commit retries a lost write (`IsRetryableWriteConflict`) in a fresh transaction. The receipt
  lookup opens the transaction's snapshot before its `FOR UPDATE`, so when two trades overlap,
  the loser's snapshot predates the winner's commit: MySQL hands it the winner's rows and it
  declines, but MariaDB 11.6+ (`innodb_snapshot_isolation`, on by default) refuses the locking
  read with `ER_CHECKREAD` instead. Any transaction that reads before it locks needs that retry.
- Each participant's durable reconciliation obligation is checked before cached furniture reads.
  Reload current custody and acknowledge only captured receipts after a stable load and successful
  unseen/client invalidation calls. A bounded hosted worker retries across restarts. Delayed
  `ReceiveFurnitureAsync` invalidates readiness; it must not reinstall historical snapshots.
  Deactivation closes the conversation, while committed receipts and pending repair survive it.
- The flow mirrors the client's state machine: accept/unaccept while open, both accepted →
  `TradingConfirmation` (client countdown), both confirm → items move → `TradingCompleted`;
  a decline drops both acceptances. Any offer change also drops them.
