using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans.Snapshots.Navigator;
using Turbo.Primitives.Orleans.Snapshots.Room;

namespace Turbo.Navigator;

public sealed class NavigatorProvider(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    ILogger<NavigatorProvider> logger
) : INavigatorProvider
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILogger<NavigatorProvider> _logger = logger;

    private ImmutableArray<NavigatorTopLevelContextSnapshot> _topLevelContexts = [];

    public Task<ImmutableArray<NavigatorTopLevelContextSnapshot>> GetTopLevelContextsAsync() =>
        Task.FromResult(_topLevelContexts);

    public async Task<List<RoomInfoSnapshot>> GetRoomResultsAsync(CancellationToken ct = default)
    {
        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var roomEntities = await dbCtx
                .Rooms.AsNoTracking()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return
            [
                .. roomEntities.Select(x => new RoomInfoSnapshot
                {
                    RoomId = x.Id,
                    Name = x.Name ?? string.Empty,
                    Description = x.Description ?? string.Empty,
                    OwnerId = (long)x.PlayerEntityId,
                    OwnerName = string.Empty,
                    Population = 0,
                    DoorMode = x.DoorMode,
                    PlayersMax = x.PlayersMax,
                    TradeType = x.TradeType,
                    Score = 0,
                    Ranking = 0,
                    CategoryId = x.NavigatorCategoryEntityId ?? -1,
                    Tags = [],
                    AllowPets = x.AllowPets,
                    AllowPetsEat = x.AllowPetsEat,
                    LastUpdatedUtc = DateTime.UtcNow,
                }),
            ];
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var topLevelEntities = await dbCtx
                .NavigatorTopLevelContexts.AsNoTracking()
                .OrderBy(x => x.OrderNum)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            _topLevelContexts =
            [
                .. topLevelEntities.Select(x => new NavigatorTopLevelContextSnapshot
                {
                    SearchCode = x.SearchCode,
                    QuickLinks = [],
                }),
            ];

            _logger.LogInformation(
                "Loaded navigator snapshot: TotalTopLevelContexts={TotalTopLevelContextsCount}",
                _topLevelContexts.Length
            );
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }
}
