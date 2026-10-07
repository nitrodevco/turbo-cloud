using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Grains;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains.WiredTrading;

/// <summary>
/// Pages through one room's wired chest transactions. Stateless: every request is a query,
/// and the logs are written by the chests, not here.
/// </summary>
internal sealed class WiredTransactionLogGrain : Grain, IWiredTransactionLogGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly WiredChestConfig _config;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IWiredTransactionLogGrain> _logger;

    public WiredTransactionLogGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<WiredChestConfig> config,
        IGrainFactory grainFactory,
        ILogger<IWiredTransactionLogGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _config = config.Value;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public async Task SendLogsAsync(
        PlayerId viewerId,
        RoomObjectId? chestId,
        int pageSize,
        int page,
        CancellationToken ct
    )
    {
        var roomId = this.GetRoomId();

        pageSize = Math.Clamp(pageSize, 1, _config.MaxLogPageSize);
        page = Math.Max(1, page);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var query = dbCtx.WiredChestTransactions.AsNoTracking();

        // A chest's log follows the chest wherever it stood; the room's log is the room's.
        query = chestId is { } chest
            ? query.Where(x => x.Entries.Any(entry => entry.ChestItemId == chest.Value))
            : query.Where(x => x.RoomId == roomId.Value);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.Entries)
            .ToListAsync(ct);

        await _grainFactory.SendComposerToPlayerAsync(
            viewerId,
            new WiredTransactionLogListMessageComposer
            {
                LogList = new()
                {
                    ListType = chestId is null
                        ? WiredTransactionLogListType.Room
                        : WiredTransactionLogListType.Chest,
                    ListId = chestId?.Value ?? roomId.Value,
                    TotalLogs = total,
                    CurrentPage = page,
                    PageSize = pageSize,
                    Logs = [.. rows.Select(x => x.ToInfoSnapshot())],
                },
            },
            ct
        );
    }

    public async Task SendDetailsAsync(PlayerId viewerId, long transactionId, CancellationToken ct)
    {
        var roomId = this.GetRoomId();

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var row = await dbCtx
            .WiredChestTransactions.AsNoTracking()
            .Include(x => x.Entries)
            .FirstOrDefaultAsync(x => x.Id == transactionId && x.RoomId == roomId.Value, ct);

        if (row is null)
        {
            _logger.LogWarning(
                "Player {PlayerId} asked for wired transaction {TransactionId}, which room {RoomId} does not have",
                viewerId,
                transactionId,
                roomId
            );

            return;
        }

        await _grainFactory.SendComposerToPlayerAsync(
            viewerId,
            new WiredTransactionLogDetailsMessageComposer
            {
                Details = row.ToDetailsSnapshot(_config.MaxLogDetailItemTypes),
            },
            ct
        );
    }
}
