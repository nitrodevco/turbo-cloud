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

/// <summary>Picks the players carrying the hand item in param 0 (zero: any hand item).</summary>
[RoomObjectLogic("wf_slc_users_handitem")]
public class WiredSelectorEntitiesWithHanditem(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.USERS_WITH_HANDITEM;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [WiredRules.HandItem(_roomGrain._wiredConfig)];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var handItemId = GetIntParamOrDefault(0, 0);

        foreach (var avatar in _roomGrain.AvatarModule.Avatars)
        {
            if (avatar is not IRoomPlayer player)
                continue;

            if (handItemId == 0 ? player.HandItemId != 0 : player.HandItemId == handItemId)
                output.SelectedPlayerIds.Add(player.PlayerId);
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
