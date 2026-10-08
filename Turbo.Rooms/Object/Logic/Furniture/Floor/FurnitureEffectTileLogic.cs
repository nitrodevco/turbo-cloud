using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A tile that puts its effect on whoever steps onto it, to keep, and lights up while it is stood
/// on (Sulake's <c>SwitchStateActionWithAvatarEffectFurniture</c>: the new-user room's effect
/// tiles <c>room_noob_fx1</c> to <c>fx4</c> and <c>room_noob_fxremove</c>, whose effect is none,
/// the advert tiles, the flying carpet). State 1 while anyone stands on it, 0 once nobody does,
/// for an asset with two states. The effect is its definition's <c>customparams</c>.
/// </summary>
[RoomObjectLogic("effect_tile")]
public class FurnitureEffectTileLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureEffectAreaLogic(stuffDataFactory, ctx)
{
    private const int IDLE_STATE = 0;
    private const int OCCUPIED_STATE = 1;

    protected override bool KeepsEffectOnLeave => true;

    protected override async Task OnOccupancyChangedAsync(bool occupied, CancellationToken ct)
    {
        var state = occupied ? OCCUPIED_STATE : IDLE_STATE;

        if (_ctx.Definition.TotalStates > OCCUPIED_STATE && GetState() != state)
            await SetStateAsync(state);
    }
}
