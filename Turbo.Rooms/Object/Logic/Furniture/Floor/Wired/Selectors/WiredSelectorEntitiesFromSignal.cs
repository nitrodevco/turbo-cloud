using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

/// <summary>Picks the users a received signal forwarded into this stack.</summary>
[RoomObjectLogic("wf_slc_users_signal")]
public class WiredSelectorEntitiesFromSignal(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.USERS_FROM_SIGNAL;

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();

        foreach (var playerId in ctx.Signal.SelectedPlayerIds)
        {
            if (TryGetPlayer(playerId, out _))
                output.SelectedPlayerIds.Add(playerId);
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
