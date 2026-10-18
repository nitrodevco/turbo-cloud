using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// The Glass Door (<c>wf_glassdoor</c>), a door wired or a use raises and lowers, as Habbo has
/// it (official client capture 2026-10-17): two states, placed in 0. In state 0 the pane is up and
/// nobody can stand on it, so a walk onto it goes nowhere and one across goes round; in state 1
/// it is floor at its own height. A use, or wired, does not change it while someone stands on it:
/// the use gets no answer and wired leaves it out of the state update it sends.
/// </summary>
[RoomObjectLogic("wf_glassdoor")]
public class FurnitureWiredDoorLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private const int OPEN_STATE = 1;

    public bool IsOpen => GetState() == OPEN_STATE;

    public override bool CanWalk() => IsOpen && base.CanWalk();

    public override bool CanChangeState() => !IsStoodOn();

    public override int GetNextToggleableState() => IsOpen ? 0 : OPEN_STATE;

    public override int GetPrevToggleableState() => GetNextToggleableState();

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (!CanChangeState())
            return;

        await base.OnUseAsync(ctx, param, ct);
    }

    private bool IsStoodOn() =>
        FloorFootprint
            .Of(_ctx.RoomObject)
            .Tiles()
            .Any(tile => AvatarModule.GetAvatarsOnTile(MapModule.ToIdx(tile.X, tile.Y)).Any());
}
