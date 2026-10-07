using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.WiredTrading;
using Turbo.Logging;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Grains;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains.WiredTrading;

/// <summary>
/// One wired chest's contents. Write-through: every move commits the rows, the credits and its
/// log entry in one database transaction before memory changes or anyone is told, so there is
/// nothing to flush on deactivation.
/// <para>
/// The furni a chest holds are ordinary <c>furniture</c> rows pointing at it (keeping their id,
/// serial and extra data), owned by the chest's owner. A deposit takes rows a player holds in no
/// room and no chest and tells their inventory to let go; a withdrawal re-owns rows to the
/// receiver and tells their inventory to take them. The credits are the chest's own row.
/// </para>
/// <para>
/// Awaits inventories and wallets, never a room: the room awaits this grain.
/// </para>
/// </summary>
internal sealed class WiredChestGrain : Grain, IWiredChestGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly WiredChestConfig _config;
    private readonly IGrainFactory _grainFactory;
    private readonly IFurnitureDefinitionProvider _defsProvider;
    private readonly IInventoryFurnitureLoader _furnitureLoader;
    private readonly ILogger<IWiredChestGrain> _logger;

    private static readonly IReadOnlyDictionary<ChestItemTypeSnapshot, int> NoItems =
        new Dictionary<ChestItemTypeSnapshot, int>();

    private readonly WiredChestLiveState _state;

    public WiredChestGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<WiredChestConfig> config,
        IGrainFactory grainFactory,
        IFurnitureDefinitionProvider defsProvider,
        IInventoryFurnitureLoader furnitureLoader,
        ILogger<IWiredChestGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _config = config.Value;
        _grainFactory = grainFactory;
        _defsProvider = defsProvider;
        _furnitureLoader = furnitureLoader;
        _logger = logger;

        _state = new() { ChestId = RoomObjectId.Parse((int)this.GetPrimaryKeyLong()) };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load wired chest {ChestId}", _state.ChestId);

            throw;
        }
    }

    public Task<WiredChestSummarySnapshot> GetSummaryAsync(
        WiredChestSettingsSnapshot chest,
        CancellationToken ct
    ) => Task.FromResult(BuildSummary(chest));

    public async Task<int> OpenAsync(
        WiredChestSettingsSnapshot chest,
        PlayerId viewerId,
        CancellationToken ct
    )
    {
        if (
            !_state.ViewerIds.Contains(viewerId)
            && _state.ViewerIds.Count >= _config.MaxViewersPerChest
        )
        {
            _logger.LogWarning(
                "Wired chest {ChestId} already has {Count} viewers; player {PlayerId} was not let in",
                _state.ChestId,
                _state.ViewerIds.Count,
                viewerId
            );

            return _state.ViewerIds.Count;
        }

        _state.ViewerIds.Add(viewerId);

        if (chest.Kind == WiredChestKind.Coins)
        {
            await _grainFactory.SendComposerToPlayerAsync(
                viewerId,
                new CoinsChestContentsMessageComposer
                {
                    ChestId = _state.ChestId,
                    Coins = _state.Coins,
                    IsUpdate = false,
                },
                ct
            );
        }
        else
        {
            var fragments = ComposerFragments.Build(
                [.. _state.Items.Select(x => x.ToStorageSnapshot())],
                _config.ContentsFragmentSize,
                (total, index, items) =>
                    new ItemsChestContentsChunkMessageComposer
                    {
                        ChestId = _state.ChestId,
                        TotalFragments = total,
                        FragmentNo = index,
                        Items = items,
                    }
            );

            await _grainFactory.GetPlayerPresenceGrain(viewerId).SendComposerAsync(fragments, ct);
        }

        return _state.ViewerIds.Count;
    }

    public Task<int> CloseAsync(PlayerId viewerId, CancellationToken ct)
    {
        _state.ViewerIds.Remove(viewerId);

        return Task.FromResult(_state.ViewerIds.Count);
    }

    public Task<WiredChestMoveResultSnapshot> DepositAsync(
        WiredChestDepositRequest request,
        CancellationToken ct
    ) =>
        request.Chest.Kind == WiredChestKind.Coins
            ? DepositCoinsAsync(request, ct)
            : DepositFurniAsync(request, ct);

    public Task<WiredChestMoveResultSnapshot> WithdrawAsync(
        WiredChestWithdrawRequest request,
        CancellationToken ct
    ) =>
        request.Chest.Kind == WiredChestKind.Coins
            ? WithdrawCoinsAsync(request, ct)
            : WithdrawFurniAsync(request, ct);

    public async Task<(
        UpgradeChestResultType Result,
        WiredChestSummarySnapshot Summary
    )> UpgradeAsync(
        WiredChestSettingsSnapshot chest,
        PlayerId payerId,
        int upgrades,
        CancellationToken ct
    )
    {
        if (chest.IsStarter)
            return (UpgradeChestResultType.StarterChest, BuildSummary(chest));

        if (payerId != chest.OwnerId)
            return (UpgradeChestResultType.NotOwner, BuildSummary(chest));

        var maxUpgrades =
            chest.Kind == WiredChestKind.Coins
                ? _config.CoinsMaxUpgrades
                : _config.FurniMaxUpgrades;

        if (upgrades <= 0 || _state.CapacityLevel + upgrades > maxUpgrades)
            return (UpgradeChestResultType.AtMaximumCapacity, BuildSummary(chest));

        var credits = CurrencyKind.Credits;
        var diamonds = CurrencyKind.ActivityPoints(_config.DiamondsActivityPointType);
        List<WalletDebitRequest> debits =
        [
            .. new[]
            {
                (Kind: credits, Amount: _config.UpgradeCostCredits * upgrades),
                (Kind: diamonds, Amount: _config.UpgradeCostDiamonds * upgrades),
            }
                .Where(x => x.Amount > 0)
                .Select(x => new WalletDebitRequest { CurrencyKind = x.Kind, Amount = x.Amount }),
        ];

        if (debits.Count > 0)
        {
            var debit = await _grainFactory.GetPlayerWalletGrain(payerId).TryDebitAsync(debits, ct);

            if (!debit.Succeeded)
                return (
                    debit.Failure?.CurrencyKind == credits
                        ? UpgradeChestResultType.InsufficientCredits
                        : UpgradeChestResultType.InsufficientDiamonds,
                    BuildSummary(chest)
                );
        }

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var row = await GetOrAddChestRowAsync(dbCtx, ct);

            row.CapacityLevel += upgrades;

            await dbCtx.SaveChangesAsync(ct);

            _state.CapacityLevel = row.CapacityLevel;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to raise the capacity of wired chest {ChestId} by {Upgrades}; refunding player {PlayerId}",
                _state.ChestId,
                upgrades,
                payerId
            );

            await _grainFactory.RefundAsync(
                payerId,
                debits,
                _logger,
                $"wired chest {_state.ChestId} upgrade"
            );

            return (UpgradeChestResultType.Error4, BuildSummary(chest));
        }

        return (UpgradeChestResultType.Success, BuildSummary(chest));
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var chestId = _state.ChestId.Value;
        var row = await dbCtx
            .WiredChests.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ItemEntityId == chestId, ct);

        _state.Coins = row?.Coins ?? 0;
        _state.CapacityLevel = row?.CapacityLevel ?? 0;

        var rows = await dbCtx
            .Furnitures.AsNoTracking()
            .Where(x => x.ChestItemEntityId == chestId)
            .OrderBy(x => x.ChestTransactionId)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.PlayerEntityId,
                x.FurnitureDefinitionEntityId,
                x.ExtraData,
                x.CreatedAt,
                x.ChestTransactionId,
            })
            .ToListAsync(ct);

        foreach (var stored in rows)
        {
            var definition = _defsProvider.TryGetDefinition(stored.FurnitureDefinitionEntityId);

            // A row whose definition is gone cannot be shown or handed out; leave it, loudly.
            if (definition is null)
            {
                _logger.LogWarning(
                    "Furniture {ItemId} in wired chest {ChestId} uses missing definition {DefinitionId}; left out",
                    stored.Id,
                    _state.ChestId,
                    stored.FurnitureDefinitionEntityId
                );

                continue;
            }

            var item = _furnitureLoader.Create(
                stored.Id,
                stored.PlayerEntityId,
                string.Empty,
                definition,
                stored.ExtraData,
                stored.CreatedAt
            );

            _state.Items.Add(
                new(
                    item,
                    ChestItemTypes.Of(definition, item.GetSnapshot().StuffData),
                    stored.ChestTransactionId ?? 0
                )
            );
        }

        foreach (var stored in _state.Items)
            InsertAtRandom(stored.Item.ItemId);
    }

    private async Task<WiredChestMoveResultSnapshot> DepositFurniAsync(
        WiredChestDepositRequest request,
        CancellationToken ct
    )
    {
        var offered = await ReadOfferAsync(request, ct);

        if (offered.Failure is { } refused)
            return WiredChestMoveResultSnapshot.Failed(refused, BuildSummary(request.Chest));

        var items = offered.Items;

        if (items.Any(x => CreditFurniValue.TryParse(x.Definition.Name, out _)))
            return WiredChestMoveResultSnapshot.Failed(
                WiredTransactionFailureType.Invalid,
                BuildSummary(request.Chest)
            );

        if (_state.Items.Count + items.Length > Capacity(request.Chest, request.Capacity))
            return WiredChestMoveResultSnapshot.Failed(
                WiredTransactionFailureType.ExceedsCapacity,
                BuildSummary(request.Chest)
            );

        var counts = CountByType(items.Select(x => ChestItemTypes.Of(x.Definition, x.StuffData)));
        var ids = items.Select(x => x.ItemId.Value).ToList();
        var depositorId = request.DepositorId.Value;
        var chestId = _state.ChestId.Value;
        long transactionId;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        await using (var tx = await dbCtx.Database.BeginTransactionAsync(ct))
        {
            var log = NewTransaction(request, counts, coins: 0, isDeposit: true);

            dbCtx.WiredChestTransactions.Add(log);

            await dbCtx.SaveChangesAsync(ct);

            transactionId = log.Id;

            var moved = await dbCtx
                .Furnitures.Where(x =>
                    ids.Contains(x.Id)
                    && x.PlayerEntityId == depositorId
                    && x.RoomEntityId == null
                    && x.ChestItemEntityId == null
                )
                .ExecuteUpdateAsync(
                    up =>
                        up.SetProperty(x => x.ChestItemEntityId, chestId)
                            .SetProperty(x => x.PlayerEntityId, request.Chest.OwnerId.Value)
                            .SetProperty(x => x.ChestTransactionId, transactionId),
                    ct
                );

            if (moved != ids.Count)
            {
                await tx.RollbackAsync(ct);

                _logger.LogWarning(
                    "Deposit of {Count} items by player {PlayerId} into wired chest {ChestId} found {Moved} of them; rolled back",
                    ids.Count,
                    request.DepositorId,
                    _state.ChestId,
                    moved
                );

                return WiredChestMoveResultSnapshot.Failed(
                    WiredTransactionFailureType.FundsGone,
                    BuildSummary(request.Chest)
                );
            }

            await tx.CommitAsync(ct);
        }

        var added = new List<WiredChestStoredItem>(items.Length);

        foreach (var item in items)
        {
            var stored = new WiredChestStoredItem(
                _furnitureLoader.CreateFromFurnitureItemSnapshot(
                    item,
                    request.Chest.OwnerId,
                    string.Empty
                ),
                ChestItemTypes.Of(item.Definition, item.StuffData),
                transactionId
            );

            _state.Items.Add(stored);
            added.Add(stored);
            InsertAtRandom(item.ItemId);
        }

        await _grainFactory
            .GetInventoryGrain(request.DepositorId)
            .ReleaseFurnitureAsync([.. items.Select(x => x.ItemId)], ct);

        await SendToViewersAsync(
            new ItemsChestContentsUpdatedMessageComposer
            {
                ChestId = _state.ChestId,
                RemovedItemIds = [],
                AddedItems = [.. added.Select(x => x.ToStorageSnapshot())],
            },
            ct
        );

        return Moved(request.Chest, transactionId, coins: 0, counts);
    }

    private async Task<WiredChestMoveResultSnapshot> DepositCoinsAsync(
        WiredChestDepositRequest request,
        CancellationToken ct
    )
    {
        var offered = await ReadOfferAsync(request, ct);

        if (offered.Failure is { } refused)
            return WiredChestMoveResultSnapshot.Failed(refused, BuildSummary(request.Chest));

        var items = offered.Items;
        var coins = 0;

        foreach (var item in items)
        {
            if (!CreditFurniValue.TryParse(item.Definition.Name, out var value))
                return WiredChestMoveResultSnapshot.Failed(
                    WiredTransactionFailureType.Invalid,
                    BuildSummary(request.Chest)
                );

            coins = checked(coins + value);
        }

        if ((long)_state.Coins + coins > Capacity(request.Chest, request.Capacity))
            return WiredChestMoveResultSnapshot.Failed(
                WiredTransactionFailureType.ExceedsCapacity,
                BuildSummary(request.Chest)
            );

        var ids = items.Select(x => x.ItemId.Value).ToList();
        var depositorId = request.DepositorId.Value;
        long transactionId;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        await using (var tx = await dbCtx.Database.BeginTransactionAsync(ct))
        {
            // The credit furni are spent: they become the chest's credits and nothing else.
            var spent = await dbCtx
                .Furnitures.Where(x =>
                    ids.Contains(x.Id)
                    && x.PlayerEntityId == depositorId
                    && x.RoomEntityId == null
                    && x.ChestItemEntityId == null
                )
                .ExecuteDeleteAsync(ct);

            if (spent != ids.Count)
            {
                await tx.RollbackAsync(ct);

                _logger.LogWarning(
                    "Credit deposit of {Count} items by player {PlayerId} into wired chest {ChestId} found {Spent} of them; rolled back",
                    ids.Count,
                    request.DepositorId,
                    _state.ChestId,
                    spent
                );

                return WiredChestMoveResultSnapshot.Failed(
                    WiredTransactionFailureType.FundsGone,
                    BuildSummary(request.Chest)
                );
            }

            var row = await GetOrAddChestRowAsync(dbCtx, ct);

            row.Coins += coins;

            var log = NewTransaction(request, NoItems, coins, isDeposit: true);

            dbCtx.WiredChestTransactions.Add(log);

            await dbCtx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            transactionId = log.Id;
            _state.Coins = row.Coins;
        }

        await _grainFactory
            .GetInventoryGrain(request.DepositorId)
            .ReleaseFurnitureAsync([.. items.Select(x => x.ItemId)], ct);

        await SendCoinsToViewersAsync(ct);

        return Moved(request.Chest, transactionId, coins, NoItems);
    }

    private async Task<WiredChestMoveResultSnapshot> WithdrawCoinsAsync(
        WiredChestWithdrawRequest request,
        CancellationToken ct
    )
    {
        var amount = request.Amount ?? _state.Coins;

        if (_state.Coins <= 0)
            return WiredChestMoveResultSnapshot.Failed(
                WiredTransactionFailureType.Empty,
                BuildSummary(request.Chest)
            );

        if (amount <= 0 || amount > _state.Coins)
            return WiredChestMoveResultSnapshot.Failed(
                WiredTransactionFailureType.InsufficientFunds,
                BuildSummary(request.Chest)
            );

        long transactionId;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        await using (var tx = await dbCtx.Database.BeginTransactionAsync(ct))
        {
            var row = await GetOrAddChestRowAsync(dbCtx, ct);

            if (row.Coins < amount)
            {
                await tx.RollbackAsync(ct);

                _logger.LogError(
                    "Wired chest {ChestId} holds {Stored} credits but its grain counted {Counted}; withdrawal refused",
                    _state.ChestId,
                    row.Coins,
                    _state.Coins
                );

                _state.Coins = row.Coins;

                return WiredChestMoveResultSnapshot.Failed(
                    WiredTransactionFailureType.FundsGone,
                    BuildSummary(request.Chest)
                );
            }

            row.Coins -= amount;

            var log = NewTransaction(request, NoItems, amount, isDeposit: false);

            dbCtx.WiredChestTransactions.Add(log);

            await dbCtx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            transactionId = log.Id;
            _state.Coins = row.Coins;
        }

        // Referenced by the transaction, so a retried credit can never pay twice.
        var credited = await _grainFactory
            .GetPlayerWalletGrain(request.ReceiverId)
            .CreditAsync(CurrencyKind.Credits, amount, CreditReference(transactionId), ct);

        if (credited == WalletCreditResult.Rejected)
        {
            _logger.LogError(
                "The wallet of player {PlayerId} refused {Amount} credits from wired chest {ChestId} (transaction {TransactionId}); putting them back",
                request.ReceiverId,
                amount,
                _state.ChestId,
                transactionId
            );

            await RestoreCoinsAsync(transactionId, amount, ct);

            return WiredChestMoveResultSnapshot.Failed(
                WiredTransactionFailureType.InternalError,
                BuildSummary(request.Chest)
            );
        }

        await SendCoinsToViewersAsync(ct);

        return Moved(request.Chest, transactionId, amount, NoItems);
    }

    private async Task<WiredChestMoveResultSnapshot> WithdrawFurniAsync(
        WiredChestWithdrawRequest request,
        CancellationToken ct
    )
    {
        var candidates = Ordered(request.Order)
            .Where(x => request.ItemType is null || x.Type == request.ItemType)
            .ToList();

        if (candidates.Count == 0)
            return WiredChestMoveResultSnapshot.Failed(
                WiredTransactionFailureType.Empty,
                BuildSummary(request.Chest)
            );

        var amount = request.Amount ?? candidates.Count;

        if (amount <= 0 || amount > candidates.Count)
            return WiredChestMoveResultSnapshot.Failed(
                WiredTransactionFailureType.InsufficientFunds,
                BuildSummary(request.Chest)
            );

        var taken = candidates.Take(amount).ToList();
        var counts = CountByType(taken.Select(x => x.Type));
        var ids = taken.Select(x => x.Item.ItemId.Value).ToList();
        var chestId = _state.ChestId.Value;
        long transactionId;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        await using (var tx = await dbCtx.Database.BeginTransactionAsync(ct))
        {
            var log = NewTransaction(request, counts, coins: 0, isDeposit: false);

            dbCtx.WiredChestTransactions.Add(log);

            await dbCtx.SaveChangesAsync(ct);

            transactionId = log.Id;

            var moved = await dbCtx
                .Furnitures.Where(x => ids.Contains(x.Id) && x.ChestItemEntityId == chestId)
                .ExecuteUpdateAsync(
                    up =>
                        up.SetProperty(x => x.ChestItemEntityId, (int?)null)
                            .SetProperty(x => x.ChestTransactionId, (long?)null)
                            .SetProperty(x => x.PlayerEntityId, request.ReceiverId.Value),
                    ct
                );

            if (moved != ids.Count)
            {
                await tx.RollbackAsync(ct);

                _logger.LogError(
                    "Withdrawal of {Count} items from wired chest {ChestId} found {Moved} of them; rolled back and reloading",
                    ids.Count,
                    _state.ChestId,
                    moved
                );

                await ReloadAsync(ct);

                return WiredChestMoveResultSnapshot.Failed(
                    WiredTransactionFailureType.FundsGone,
                    BuildSummary(request.Chest)
                );
            }

            await tx.CommitAsync(ct);
        }

        var takenIds = taken.Select(x => x.Item.ItemId).ToHashSet();

        _state.Items.RemoveAll(x => takenIds.Contains(x.Item.ItemId));
        _state.RandomOrder.RemoveAll(takenIds.Contains);

        await _grainFactory
            .GetInventoryGrain(request.ReceiverId)
            .ReceiveFurnitureAsync(
                [
                    .. taken.Select(x =>
                        _furnitureLoader
                            .CreateFromFurnitureItemSnapshot(
                                x.Item.GetSnapshot(),
                                request.ReceiverId,
                                request.ReceiverName
                            )
                            .GetSnapshot()
                    ),
                ],
                ct
            );

        await SendToViewersAsync(
            new ItemsChestContentsUpdatedMessageComposer
            {
                ChestId = _state.ChestId,
                RemovedItemIds = [.. ids],
                AddedItems = [],
            },
            ct
        );

        return Moved(request.Chest, transactionId, coins: 0, counts);
    }

    /// <summary>
    /// The items a deposit offers, as the depositor's inventory holds them now. Each must be
    /// there, tradeable, and named once.
    /// </summary>
    private async Task<(
        ImmutableArray<FurnitureItemSnapshot> Items,
        WiredTransactionFailureType? Failure
    )> ReadOfferAsync(WiredChestDepositRequest request, CancellationToken ct)
    {
        var ids = request.ItemIds.IsDefault ? [] : request.ItemIds.Distinct().ToImmutableArray();

        if (ids.IsEmpty)
            return ([], WiredTransactionFailureType.Empty);

        if (ids.Length > _config.MaxItemsPerDeposit)
            return ([], WiredTransactionFailureType.TooManyOffers);

        var items = await _grainFactory
            .GetInventoryGrain(request.DepositorId)
            .GetItemSnapshotsAsync(ids, ct);

        if (items.Length != ids.Length || items.Any(x => !x.Definition.CanTrade))
            return ([], WiredTransactionFailureType.Invalid);

        return (items, null);
    }

    private async Task RestoreCoinsAsync(long transactionId, int amount, CancellationToken ct)
    {
        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);
            await using var tx = await dbCtx.Database.BeginTransactionAsync(ct);

            var row = await GetOrAddChestRowAsync(dbCtx, ct);

            row.Coins += amount;

            var log = await dbCtx.WiredChestTransactions.FirstOrDefaultAsync(
                x => x.Id == transactionId,
                ct
            );

            if (log is not null)
                dbCtx.WiredChestTransactions.Remove(log);

            await dbCtx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _state.Coins = row.Coins;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to put {Amount} credits back into wired chest {ChestId} after transaction {TransactionId}",
                amount,
                _state.ChestId,
                transactionId
            );
        }
    }

    private async Task ReloadAsync(CancellationToken ct)
    {
        _state.Items.Clear();
        _state.RandomOrder.Clear();

        await HydrateAsync(ct);
    }

    private async Task<WiredChestEntity> GetOrAddChestRowAsync(
        TurboDbContext dbCtx,
        CancellationToken ct
    )
    {
        var chestId = _state.ChestId.Value;
        var row = await dbCtx.WiredChests.FirstOrDefaultAsync(x => x.ItemEntityId == chestId, ct);

        if (row is not null)
            return row;

        row = new WiredChestEntity { ItemEntityId = chestId };

        dbCtx.WiredChests.Add(row);

        return row;
    }

    private WiredChestTransactionEntity NewTransaction(
        WiredChestDepositRequest request,
        IReadOnlyDictionary<ChestItemTypeSnapshot, int> counts,
        int coins,
        bool isDeposit
    ) =>
        NewTransaction(
            request.Chest,
            request.DepositorId,
            request.DepositorName,
            request.Type,
            request.DefinitionInfo,
            counts,
            coins,
            isDeposit
        );

    private WiredChestTransactionEntity NewTransaction(
        WiredChestWithdrawRequest request,
        IReadOnlyDictionary<ChestItemTypeSnapshot, int> counts,
        int coins,
        bool isDeposit
    ) =>
        NewTransaction(
            request.Chest,
            request.ReceiverId,
            request.ReceiverName,
            request.Type,
            request.DefinitionInfo,
            counts,
            coins,
            isDeposit
        );

    private WiredChestTransactionEntity NewTransaction(
        WiredChestSettingsSnapshot chest,
        PlayerId playerId,
        string playerName,
        WiredTransactionType type,
        string definitionInfo,
        IReadOnlyDictionary<ChestItemTypeSnapshot, int> counts,
        int coins,
        bool isDeposit
    )
    {
        var log = new WiredChestTransactionEntity
        {
            RoomId = chest.RoomId.Value,
            Type = type,
            PlayerId = playerId.Value,
            PlayerName = Truncate(playerName, 64),
            DefinitionInfo = Truncate(definitionInfo, 255),
            CreatedAt = DateTime.UtcNow,
        };

        if (coins > 0)
            log.Entries.Add(
                new WiredChestTransactionEntryEntity
                {
                    ChestItemId = _state.ChestId.Value,
                    IsDeposit = isDeposit,
                    IsCoins = true,
                    PosterId = string.Empty,
                    Count = coins,
                }
            );

        foreach (var (type2, count) in counts)
            log.Entries.Add(
                new WiredChestTransactionEntryEntity
                {
                    ChestItemId = _state.ChestId.Value,
                    IsDeposit = isDeposit,
                    IsWallItem = type2.IsWallItem,
                    TypeId = type2.TypeId,
                    PosterId = Truncate(type2.LegacyPosterId, 64),
                    Count = count,
                }
            );

        return log;
    }

    private WiredChestMoveResultSnapshot Moved(
        WiredChestSettingsSnapshot chest,
        long transactionId,
        int coins,
        IReadOnlyDictionary<ChestItemTypeSnapshot, int> counts
    ) =>
        new()
        {
            Failure = null,
            TransactionId = transactionId,
            Coins = coins,
            Items =
            [
                .. counts.Select(x => new WiredTransactionItemCountSnapshot
                {
                    Type = x.Key,
                    Count = x.Value,
                }),
            ],
            Summary = BuildSummary(chest),
        };

    private WiredChestSummarySnapshot BuildSummary(WiredChestSettingsSnapshot chest) =>
        new()
        {
            ItemCount = _state.Items.Count,
            Coins = _state.Coins,
            CapacityLevel = _state.CapacityLevel,
            MaxCapacity = MaxCapacity(chest),
            CountsByType = CountByType(_state.Items.Select(x => x.Type)).ToImmutableDictionary(),
            Preview = BuildPreview(chest),
        };

    /// <summary>The most the chest holds: what its level allows, lowered by what its owner set.</summary>
    private int Capacity(WiredChestSettingsSnapshot chest, int ownerCapacity) =>
        Math.Min(MaxCapacity(chest), Math.Max(0, ownerCapacity));

    private int MaxCapacity(WiredChestSettingsSnapshot chest)
    {
        var coins = chest.Kind == WiredChestKind.Coins;

        if (chest.IsStarter)
            return coins ? _config.CoinsStarterCapacity : _config.FurniStarterCapacity;

        return coins
            ? _config.CoinsInitialCapacity + _config.CoinsUpgradeCapacity * _state.CapacityLevel
            : _config.FurniInitialCapacity + _config.FurniUpgradeCapacity * _state.CapacityLevel;
    }

    private ImmutableArray<ChestItemTypeSnapshot> BuildPreview(WiredChestSettingsSnapshot chest)
    {
        var amount = Math.Clamp(chest.PreviewAmount, 0, _config.MaxPreviewItems);

        if (chest.Kind != WiredChestKind.Furni || amount == 0 || _state.Items.Count == 0)
            return [];

        IEnumerable<WiredChestStoredItem> source = chest.PreviewMode switch
        {
            WiredChestPreviewMode.Random or WiredChestPreviewMode.RandomDistinct =>
                _state.Items.OrderBy(_ => Random.Shared.Next()),
            WiredChestPreviewMode.MostRecent or WiredChestPreviewMode.MostRecentDistinct =>
                Enumerable.Reverse(_state.Items),
            WiredChestPreviewMode.Oldest or WiredChestPreviewMode.OldestDistinct => _state.Items,
            WiredChestPreviewMode.NextRandom => Ordered(WiredChestIterationMode.Random),
            _ => [],
        };

        var types = source.Select(x => x.Type);

        if (
            chest.PreviewMode
            is WiredChestPreviewMode.RandomDistinct
                or WiredChestPreviewMode.MostRecentDistinct
                or WiredChestPreviewMode.OldestDistinct
        )
            types = types.Distinct();

        return [.. types.Take(amount)];
    }

    private IEnumerable<WiredChestStoredItem> Ordered(WiredChestIterationMode order)
    {
        switch (order)
        {
            case WiredChestIterationMode.LastInFirstOut:
                return Enumerable.Reverse(_state.Items);
            case WiredChestIterationMode.Random:
            {
                var byId = _state.Items.ToDictionary(x => x.Item.ItemId);

                return _state.RandomOrder.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
            }
            default:
                return _state.Items;
        }
    }

    private void InsertAtRandom(RoomObjectId itemId) =>
        _state.RandomOrder.Insert(Random.Shared.Next(_state.RandomOrder.Count + 1), itemId);

    private static Dictionary<ChestItemTypeSnapshot, int> CountByType(
        IEnumerable<ChestItemTypeSnapshot> types
    )
    {
        var counts = new Dictionary<ChestItemTypeSnapshot, int>();

        foreach (var type in types)
            counts[type] = counts.GetValueOrDefault(type) + 1;

        return counts;
    }

    private Task SendCoinsToViewersAsync(CancellationToken ct) =>
        SendToViewersAsync(
            new CoinsChestContentsMessageComposer
            {
                ChestId = _state.ChestId,
                Coins = _state.Coins,
                IsUpdate = true,
            },
            ct
        );

    private Task SendToViewersAsync(IComposer composer, CancellationToken ct) =>
        _state.ViewerIds.Count == 0
            ? Task.CompletedTask
            : _grainFactory.SendComposerToPlayersAsync(_state.ViewerIds, composer, ct);

    private static string CreditReference(long transactionId) => $"wiredchest:{transactionId}";

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];
}
