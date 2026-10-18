using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// Floor Switch 1 (<c>wf_floor_switch1</c>), a switch players flip ("Can only be operated from an
/// adjacent tile"). A use from a tile touching it (diagonals too) flips it at once; a use from
/// further away walks the avatar to the free tile beside it nearest to the avatar, and it flips
/// when the avatar is there. The avatar does not turn to face it. Habbo did exactly that for a
/// use with no walk asked for, 6 of 6 from three sides and both switches, and flipped at once for
/// a use from beside it, 4 of 4 (official client capture, 2026-10-18, build WIN63-202610151745).
/// <para>
/// The Toggle Furni State effect leaves it as it is: Habbo's effect ran, toggled the lamp it also
/// pointed at and left both switches out of its state update, whether the triggering user stood
/// beside the switch or not, 4 of 4 each (official client capture, 2026-10-18).
/// </para>
/// </summary>
[RoomObjectLogic("wf_floor_switch1")]
public class FurnitureFloorSwitchLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWalkToUseLogic(stuffDataFactory, ctx)
{
    public override bool CanWiredToggle() => false;

    protected override int WalkTimeoutMs => _roomGrain._roomConfig.FloorSwitchWalkTimeoutMs;

    public override Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (GetAvatar(ctx) is not { } avatar)
            return Task.CompletedTask;

        return WalkToUseAsync(
            avatar,
            StandsBeside(avatar) ? [MapModule.ToIdx(avatar.X, avatar.Y)] : TilesBeside(avatar),
            ct
        );
    }

    protected override Task UseFromTileAsync(IRoomAvatar avatar, CancellationToken ct) =>
        SetStateAsync(GetNextToggleableState());
}
