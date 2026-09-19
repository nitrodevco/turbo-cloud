using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>Restarts the room wired timer that "time elapsed" and "at given time" boxes read.</summary>
[RoomObjectLogic("wf_act_reset_timers")]
public class WiredActionResetTimers(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.RESET;

    public override Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        _roomGrain.WiredSystem.ResetTimers(_roomGrain.NowMs());

        return Task.FromResult(true);
    }
}
