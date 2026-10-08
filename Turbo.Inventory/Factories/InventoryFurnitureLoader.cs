using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Furniture;
using Turbo.Inventory.Furniture;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Furniture;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Primitives.Sound;

namespace Turbo.Inventory.Factories;

internal sealed class InventoryFurnitureLoader(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IFurnitureDefinitionProvider defsProvider,
    IStuffDataFactory stuffDataFactory,
    ILogger<IInventoryFurnitureLoader> logger
) : IInventoryFurnitureLoader
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly IFurnitureDefinitionProvider _defsProvider = defsProvider;
    private readonly IStuffDataFactory _stuffDataFactory = stuffDataFactory;
    private readonly ILogger<IInventoryFurnitureLoader> _logger = logger;

    public async Task<IReadOnlyList<IFurnitureItem>> LoadByPlayerIdAsync(
        PlayerId playerId,
        string ownerName,
        CancellationToken ct
    )
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var rows = await dbCtx
            .Furnitures.AsNoTracking()
            .Where(x =>
                x.PlayerEntityId == (int)playerId
                && x.RoomEntityId == null
                && x.ChestItemEntityId == null
            )
            .Select(x => new
            {
                x.Id,
                x.FurnitureDefinitionEntityId,
                x.ExtraData,
                x.CreatedAt,
            })
            .ToListAsync(ct);

        var items = new List<IFurnitureItem>(rows.Count);

        foreach (var row in rows)
        {
            var definition = _defsProvider.TryGetDefinition(row.FurnitureDefinitionEntityId);

            // A row whose definition is gone cannot be shown or placed; skip it, loudly.
            if (definition is null)
            {
                _logger.LogWarning(
                    "Furniture {ItemId} of player {PlayerId} uses missing definition {DefinitionId}; left out of the inventory",
                    row.Id,
                    playerId,
                    row.FurnitureDefinitionEntityId
                );

                continue;
            }

            items.Add(
                Create(row.Id, playerId, ownerName, definition, row.ExtraData, row.CreatedAt)
            );
        }

        return items;
    }

    public IFurnitureItem Create(
        RoomObjectId itemId,
        PlayerId ownerId,
        string ownerName,
        FurnitureDefinitionSnapshot definition,
        string? extraDataJson,
        DateTime? createdAtUtc
    ) => Build(itemId, ownerId, ownerName, definition, extraDataJson, null, createdAtUtc);

    public IFurnitureItem CreateFromFurnitureItemSnapshot(
        FurnitureItemSnapshot snapshot,
        PlayerId ownerId,
        string ownerName
    ) =>
        Build(
            snapshot.ItemId,
            ownerId,
            ownerName,
            snapshot.Definition,
            snapshot.ExtraData,
            (StuffDataType)snapshot.StuffData.StuffBitmask,
            snapshot.CreatedAtUtc
        );

    public IFurnitureItem CreateFromRoomItemSnapshot(RoomItemSnapshot snapshot)
    {
        var definition =
            _defsProvider.TryGetDefinition(snapshot.DefinitionId)
            ?? throw new TurboException(TurboErrorCodeEnum.FurnitureDefinitionNotFound);

        return Build(
            snapshot.ObjectId,
            snapshot.OwnerId,
            snapshot.OwnerName,
            definition,
            snapshot.ExtraData,
            (StuffDataType)snapshot.StuffData.StuffBitmask,
            null
        );
    }

    /// <summary>
    /// The one place an inventory item is assembled: the stuff data always comes from the
    /// stuff section of the item's own extra data. A room says which kind of stuff data its
    /// item has; a row read from the database or a new grant has nobody to say, so the
    /// stored section's shape decides (<see cref="StoredStuffDataType"/>).
    /// </summary>
    private FurnitureItem Build(
        RoomObjectId itemId,
        PlayerId ownerId,
        string ownerName,
        FurnitureDefinitionSnapshot definition,
        string? extraDataJson,
        StuffDataType? stuffDataType,
        DateTime? createdAtUtc
    )
    {
        var extraData = new ExtraData(extraDataJson);
        var stuffData = _stuffDataFactory.CreateStuffDataFromExtraData(
            stuffDataType ?? StoredStuffDataType(extraData),
            extraData
        );

        return new FurnitureItem
        {
            ItemId = itemId,
            OwnerId = ownerId,
            OwnerName = ownerName,
            Definition = definition,
            ExtraData = extraData,
            StuffData = stuffData,
            CreatedAtUtc = createdAtUtc,
            Extra = ObjectExtra(definition, extraData, stuffData),
        };
    }

    /// <summary>
    /// The number the client reads beside an item's stuff data, never from it: a song disk's
    /// song, and a present's box and ribbon (which pick the frames it is drawn with). Zero for
    /// everything else.
    /// </summary>
    private int ObjectExtra(
        FurnitureDefinitionSnapshot definition,
        ExtraData extraData,
        IStuffData stuffData
    )
    {
        if (SongDisks.IsSongDisk(definition))
            return SongDisks.SongIdOf(stuffData);

        if (!PresentData.IsPresent(definition.LogicName))
            return 0;

        return FurnitureExtraDataSections
                .Read<PresentStorage>(extraData, PresentStorage.SECTION, _logger)
                ?.GetObjectExtra()
            ?? 0;
    }

    /// <summary>
    /// Which kind of stuff data a stored stuff section holds, by the shape of its <c>Data</c>:
    /// a string array (a badge display, guild furni), a number array, a key/value map, or
    /// else the legacy string. The kind belongs to the furniture logic, which inventories do not
    /// run; reading it from the section keeps an item the same here as in the room it left.
    /// </summary>
    private static StuffDataType StoredStuffDataType(ExtraData extraData)
    {
        if (
            !extraData.TryGetSection(ExtraDataSectionType.STUFF, out var section)
            || section.ValueKind != JsonValueKind.Object
            || !section.TryGetProperty(nameof(IStringStuffData.Data), out var data)
        )
            return StuffDataType.LegacyKey;

        if (data.ValueKind == JsonValueKind.Object)
            return StuffDataType.MapKey;

        if (data.ValueKind != JsonValueKind.Array)
            return StuffDataType.LegacyKey;

        return
            data.GetArrayLength() > 0
            && data.EnumerateArray().All(value => value.ValueKind == JsonValueKind.Number)
            ? StuffDataType.NumberKey
            : StuffDataType.StringKey;
    }
}
