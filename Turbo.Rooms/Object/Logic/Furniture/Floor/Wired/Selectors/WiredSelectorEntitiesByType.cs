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
/// Picks every avatar of a kind: param 0 is a bitmask of 1 players, 2 bots, 4 pets. This is
/// the box that narrows a selection down to one kind; every other selector takes all three.
/// </summary>
[RoomObjectLogic("wf_slc_users_bytype")]
public class WiredSelectorEntitiesByType(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int TYPE_PLAYER = 1;
    private const int TYPE_BOT = 2;
    private const int TYPE_PET = 4;

    public override int WiredCode => (int)WiredSelectorType.USERS_BY_TYPE;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredRangeParamRule(0, 7, 1)];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var kinds = GetIntParamOrDefault(0, TYPE_PLAYER);

        foreach (var avatar in _roomGrain.AvatarModule.Avatars)
        {
            var kind = avatar switch
            {
                IRoomPlayer => TYPE_PLAYER,
                IRoomBot => TYPE_BOT,
                IRoomPet => TYPE_PET,
                _ => 0,
            };

            if ((kinds & kind) != 0)
                output.SelectedAvatarIds.Add(avatar.ObjectId);
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
