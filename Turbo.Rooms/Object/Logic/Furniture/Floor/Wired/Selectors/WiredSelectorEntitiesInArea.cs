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

/// <summary>Picks the players inside a rectangle. Params: root x, root y, width, height.</summary>
[RoomObjectLogic("wf_slc_users_area")]
public class WiredSelectorEntitiesInArea(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.USERS_IN_AREA;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredParamRule(0),
            new WiredParamRule(0),
            new WiredParamRule(0),
            new WiredParamRule(0),
        ];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var rootX = GetIntParamOrDefault(0, 0);
        var rootY = GetIntParamOrDefault(1, 0);
        var width = GetIntParamOrDefault(2, 0);
        var height = GetIntParamOrDefault(3, 0);

        if (width * height > _roomGrain._roomConfig.WiredMaxAreaTiles)
            return Task.FromResult<IWiredSelectionSet>(output);

        foreach (var avatar in _roomGrain._state.AvatarsByObjectId.Values)
        {
            if (avatar is not IRoomPlayer player)
                continue;

            var inside =
                player.X >= rootX
                && player.X < rootX + width
                && player.Y >= rootY
                && player.Y < rootY + height;

            if (inside)
                output.SelectedPlayerIds.Add(player.PlayerId);
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
