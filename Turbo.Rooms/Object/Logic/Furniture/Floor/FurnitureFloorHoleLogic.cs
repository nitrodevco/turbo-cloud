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
/// A hole in the floor (Sulake's <c>HoleFurniture</c>: the Black Hole, "Reshape your room map
/// with this 2x2 Black Hole", and the SnowStorm 1x1 hole). In state 0 the client cuts the floor
/// away under it (<c>FurnitureFloorHoleLogic.STATE_HOLE</c>) and nobody can stand there; in state
/// 1 it is floor again, as its <c>&lt;stand/&gt;</c> allows. A use toggles it, and it is not
/// opened under anyone standing on it.
/// </summary>
[RoomObjectLogic("floor_hole")]
public class FurnitureFloorHoleLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private const int OPEN_STATE = 0;

    public bool IsOpen => StuffData.GetState() == OPEN_STATE;

    public override bool CanWalk() => !IsOpen && base.CanWalk();

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (GetNextToggleableState() == OPEN_STATE && IsStoodOn())
            return;

        await base.OnUseAsync(ctx, param, ct);
    }

    private bool IsStoodOn() =>
        FloorFootprint
            .Of(_ctx.RoomObject)
            .Tiles()
            .Any(tile => AvatarModule.GetAvatarsOnTile(MapModule.ToIdx(tile.X, tile.Y)).Any());
}
