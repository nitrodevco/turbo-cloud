using System.Collections.Generic;
using System.Linq;
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
/// Params: what the neighbourhood is around ("In neighbourhood of": 1 the users of the user
/// slot, 0 the furni of the furni slot), root x, root y, then 15 ints of the spiral tile mask.
/// The editor sends 14 mask ints; Habbo stores 15, padding with 0 and dropping any past them,
/// and clamps the root into -64..64 (official client capture 2026-10-22T22-51-19 / 22-53-42).
/// The furni and the users selector differ only in what they collect from a tile.
/// </summary>
public abstract class FurnitureWiredNeighborhoodSelectorLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_USERS = 0;
    private const int MASK_START_INDEX = 3;

    // A fresh box opens on the official server merged, rooted on its input, with the 5x5 square
    // around it painted (spiral ranks 0-24) in 15 mask ints, one more than the editor's drawing
    // packs into (defaults [1,0,0,33554431,0 x14] for both selectors, 2026-10-20T17-33-23-Open-wait).
    private const int DEFAULT_MASK_INTS = WiredNeighborhood.MASK_INTS + 1;
    private const int DEFAULT_MASK_SQUARE = (1 << 25) - 1;

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
            new WiredBoolParamRule(true), // in neighbourhood of users
            RootOffset, // rootX
            RootOffset, // rootY
            .. Enumerable
                .Range(0, DEFAULT_MASK_INTS)
                .Select(i => WiredRules.AnyInt(i == 0 ? DEFAULT_MASK_SQUARE : 0)),
        ];

    private WiredClampParamRule RootOffset =>
        new(-WiredConfig.NeighborhoodMaxRootOffset, WiredConfig.NeighborhoodMaxRootOffset, 0);

    // The editor sends the 21x21 drawing in 14 mask ints; Habbo stores 15 and drops the rest.
    protected override int GetRequiredIntParamCount() => MASK_START_INDEX;

    protected override bool TryNormalizeIntParams(List<int> proposed, out List<int> normalized)
    {
        var count = GetIntParamRules().Count;
        List<int> sent = [.. proposed.Take(count)];

        while (sent.Count >= MASK_START_INDEX && sent.Count < count)
            sent.Add(0);

        return base.TryNormalizeIntParams(sent, out normalized);
    }

    protected abstract void CollectTile(int tileId, WiredSelectionSet output);

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var input = GetIntParamOrDefault(PARAM_USERS, true)
            ? WiredSlotSelection.ForUserSlot(this, ctx, 0)
            : WiredSlotSelection.ForSlot(this, ctx, 0);
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
