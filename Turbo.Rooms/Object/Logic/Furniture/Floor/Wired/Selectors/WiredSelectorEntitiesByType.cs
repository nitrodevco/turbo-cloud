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
/// Picks every avatar of a kind: param 0 is a bitmask of 1 players, 2 bots, 4 pets. Only
/// players can be handed on to other boxes, so bot and pet bits select nothing.
/// </summary>
[RoomObjectLogic("wf_slc_users_bytype")]
public class WiredSelectorEntitiesByType(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int TYPE_PLAYER = 1;

    public override int WiredCode => (int)WiredSelectorType.USERS_BY_TYPE;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredRangeParamRule(0, 7, 1)];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();

        if ((GetIntParamOrDefault(0, TYPE_PLAYER) & TYPE_PLAYER) == 0)
            return Task.FromResult<IWiredSelectionSet>(output);

        foreach (var avatar in _roomGrain.AvatarModule.Avatars)
        {
            if (avatar is IRoomPlayer player)
                output.SelectedPlayerIds.Add(player.PlayerId);
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
