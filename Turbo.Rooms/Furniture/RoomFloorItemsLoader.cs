using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Contracts.Enums.Rooms.Furniture.Data;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Furniture.Abstractions;
using Turbo.Primitives.Rooms;
using Turbo.Rooms.Abstractions.Furniture;
using Turbo.Rooms.Furniture.Data;

namespace Turbo.Rooms.Furniture;

public sealed class RoomFloorItemsLoader(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    IFurnitureDefinitionProvider defsProvider
) : IRoomFloorItemsLoader
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

                    item.SetPosition(entity.X, entity.Y, (float)entity.Z);
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

    private IRoomFloorItem CreateFromEntity(FurnitureEntity entity)
    {
        var definition =
            _defsProvider.TryGetDefinition(entity.FurnitureDefinitionEntityId)
            ?? throw new InvalidOperationException(
                $"Furniture definition with id {entity.FurnitureDefinitionEntityId} not found"
            );
        var stuffData = StuffDataFactory.CreateStuffData((int)StuffDataTypeEnum.LegacyKey);

        return new RoomFloorItem
        {
            Id = entity.Id,
            Definition = definition,
            StuffData = stuffData,
        };
    }
}
