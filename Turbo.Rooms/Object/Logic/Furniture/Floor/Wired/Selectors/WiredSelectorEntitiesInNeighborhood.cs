using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

/// <summary>
/// Picks the players on the tiles of a painted neighbourhood around each input furni or user.
/// Same param layout as the furni neighbourhood selector: merged flag, root x, root y, then
/// the spiral mask.
/// </summary>
[RoomObjectLogic("wf_slc_users_neighborhood")]
public class WiredSelectorEntitiesInNeighborhood(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.USERS_IN_NEIGHBORHOOD;

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
        [new WiredBoolParamRule(false), new WiredParamRule(0), new WiredParamRule(0)];

    public override IWiredParamRule? GetIntParamTailRule() => new WiredParamRule(0);

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
        var mask = _wiredData.IntParams.Count > 3 ? _wiredData.IntParams[3..] : [];

        foreach (var item in GetFloorItems(input))
            tiles.UnionWith(
                WiredNeighborhood.Tiles(_roomGrain, item.X, item.Y, rootX, rootY, mask)
            );

        foreach (var player in GetPlayers(input))
            tiles.UnionWith(
                WiredNeighborhood.Tiles(_roomGrain, player.X, player.Y, rootX, rootY, mask)
            );

        foreach (var tileId in tiles)
        {
            foreach (var avatarId in _roomGrain._state.TileAvatarStacks[tileId])
            {
                if (
                    _roomGrain._state.AvatarsByObjectId.TryGetValue(avatarId, out var avatar)
                    && avatar is IRoomPlayer player
                )
                    output.SelectedPlayerIds.Add(player.PlayerId);
            }
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
