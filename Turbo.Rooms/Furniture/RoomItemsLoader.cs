using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Contracts.Enums;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Logging;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Rooms.Furniture.Floor;

namespace Turbo.Rooms.Furniture;

internal sealed class RoomItemsLoader(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    IFurnitureDefinitionProvider defsProvider
) : IRoomItemsLoader
{
    private readonly IDbContextFactory<TurboDbContext> _dbContextFactory = dbContextFactory;
    private readonly IFurnitureDefinitionProvider _defsProvider = defsProvider;

    public async Task<IReadOnlyList<IRoomFloorItem>> LoadByRoomIdAsync(
        long roomId,
        CancellationToken ct = default
    )
    {
        var dbCtx = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        try
        {
            var entities = await dbCtx
                .Furnitures.AsNoTracking()
                .Where(x => x.RoomEntityId == roomId)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var result = new List<IRoomFloorItem>(entities.Count);

            foreach (var entity in entities)
            {
                try
                {
                    var item = CreateFromEntity(entity);

                    if (item is null)
                        continue;

                    item.SetPosition(entity.X, entity.Y, entity.Z);
                    item.SetRotation(entity.Rotation);

                    result.Add(item);
                }
                catch (Exception e)
                {
                    continue;
                }
            }

            return result;
        }
        finally
        {
            await dbCtx.DisposeAsync().ConfigureAwait(false);
        }
    }

    public IRoomFloorItem CreateFromEntity(FurnitureEntity entity)
    {
        var definition =
            _defsProvider.TryGetDefinition(entity.FurnitureDefinitionEntityId)
            ?? throw new TurboException(TurboErrorCodeEnum.FurnitureDefinitionNotFound);

        var floorItem = new RoomFloorItem
        {
            Id = entity.Id,
            OwnerId = entity.PlayerEntityId,
            Definition = definition,
        };

        floorItem.SetStuffDataRaw(entity.StuffData ?? string.Empty);

        return floorItem;
    }
}
