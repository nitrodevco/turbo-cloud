using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

/// <summary>
/// Picks what stands on the tiles of a painted neighbourhood around each input furni or user.
/// Params: merged flag, root x, root y, then the spiral mask ints. The furni and the users
/// selector differ only in what they collect from a tile.
/// </summary>
public abstract class FurnitureWiredNeighborhoodSelectorLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int MASK_START_INDEX = 3;

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SignalItems,
                WiredFurniSourceType.TriggeredItem,
            ],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [WiredPlayerSourceType.TriggeredUser, WiredPlayerSourceType.SignalUsers],
        ];

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false), // merged source
            WiredRules.TileOffset(_roomGrain._wiredConfig), // rootX
            WiredRules.TileOffset(_roomGrain._wiredConfig), // rootY
        ];

    public override IWiredParamRule? GetIntParamTailRule() => WiredRules.AnyInt();

    protected abstract void CollectTile(int tileId, WiredSelectionSet output);

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var input = ctx.GetSelection(this);
        var output = new WiredSelectionSet();
        var tiles = new HashSet<int>();
        var rootX = GetIntParamOrDefault(1, 0);
        var rootY = GetIntParamOrDefault(2, 0);
        var mask =
            _wiredData.IntParams.Count > MASK_START_INDEX
                ? _wiredData.IntParams[MASK_START_INDEX..]
                : [];

        foreach (var item in GetFloorItems(input))
            tiles.UnionWith(
                WiredNeighborhood.Tiles(_roomGrain, item.X, item.Y, rootX, rootY, mask)
            );

        foreach (var player in GetPlayers(input))
            tiles.UnionWith(
                WiredNeighborhood.Tiles(_roomGrain, player.X, player.Y, rootX, rootY, mask)
            );

        foreach (var tileId in tiles)
            CollectTile(tileId, output);

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
