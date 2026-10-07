using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Turbo.Database.Entities.WiredTrading;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Database.Extensions;

public static class WiredTradingEntityExtensions
{
    private const string READABLE_TIMESTAMP_FORMAT = "yyyy-MM-dd HH:mm:ss";

    /// <summary>A log line: who, when, and how much went each way. The entries must be loaded.</summary>
    public static WiredTransactionInfoSnapshot ToInfoSnapshot(
        this WiredChestTransactionEntity entity
    ) =>
        new()
        {
            TransactionId = entity.Id,
            FlatId = RoomId.Parse(entity.RoomId),
            Type = entity.Type,
            DefinitionInfo = entity.DefinitionInfo,
            UserId = PlayerId.Parse(entity.PlayerId),
            UserName = entity.PlayerName,
            Timestamp = new DateTimeOffset(
                DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)
            ).ToUnixTimeSeconds(),
            ReadableTimestamp = entity.CreatedAt.ToString(
                READABLE_TIMESTAMP_FORMAT,
                CultureInfo.InvariantCulture
            ),
            ChestCount = entity.Entries.Select(x => x.ChestItemId).Distinct().Count(),
            WithdrawFurniCount = Sum(entity.Entries, deposit: false, coins: false),
            DepositFurniCount = Sum(entity.Entries, deposit: true, coins: false),
            WithdrawCoinsCount = Sum(entity.Entries, deposit: false, coins: true),
            DepositCoinsCount = Sum(entity.Entries, deposit: true, coins: true),
        };

    /// <summary>
    /// A log line in full: the furni per type each way (credits are in the line itself). Past
    /// <paramref name="maxItemTypes"/> types per side the rest are left out and the client is
    /// told there are more.
    /// </summary>
    public static WiredTransactionDetailsSnapshot ToDetailsSnapshot(
        this WiredChestTransactionEntity entity,
        int maxItemTypes
    )
    {
        var deposited = Types(entity.Entries, deposit: true);
        var withdrawn = Types(entity.Entries, deposit: false);

        return new()
        {
            Info = entity.ToInfoSnapshot(),
            ChestIds = [.. entity.Entries.Select(x => x.ChestItemId).Distinct()],
            Deposited = [.. deposited.Take(maxItemTypes)],
            Withdrawn = [.. withdrawn.Take(maxItemTypes)],
            IsIncompleteData = deposited.Count > maxItemTypes || withdrawn.Count > maxItemTypes,
        };
    }

    private static int Sum(
        IEnumerable<WiredChestTransactionEntryEntity> entries,
        bool deposit,
        bool coins
    ) => entries.Where(x => x.IsDeposit == deposit && x.IsCoins == coins).Sum(x => x.Count);

    private static List<WiredTransactionItemCountSnapshot> Types(
        IEnumerable<WiredChestTransactionEntryEntity> entries,
        bool deposit
    ) =>
        [
            .. entries
                .Where(x => x.IsDeposit == deposit && !x.IsCoins)
                .GroupBy(x => new ChestItemTypeSnapshot
                {
                    IsWallItem = x.IsWallItem,
                    TypeId = x.TypeId,
                    LegacyPosterId = x.PosterId,
                })
                .Select(x => new WiredTransactionItemCountSnapshot
                {
                    Type = x.Key,
                    Count = x.Sum(entry => entry.Count),
                }),
        ];
}
