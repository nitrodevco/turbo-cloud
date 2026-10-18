using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// The Wired Disabler (<c>conf_wired_disable</c>). While one in the room is switched on, no
/// wired trigger in it fires; the boxes still open and save, and picking the disabler up while
/// it is on lets wired fire again at once. Switching it answers with its new state only: it is
/// not a flag of <c>ConfigurationItemStates</c>, and the boxes are told nothing (official client
/// capture, 2026-10-18: states 0 and 1, a walk-on trigger fired 3 of 3 times while off and 0 of
/// 3 while on). Rights holders switch it; off is the default state.
/// </summary>
[RoomObjectLogic("wired_disable")]
public class FurnitureWiredDisableLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private const int OFF = 0;
    private const int ON = 1;

    public bool IsOn => GetState() == ON;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Controller;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (!await HasRightsAsync(ctx))
            return;

        // Two states whatever the definition counts: it is a switch.
        await SetStateAsync(IsOn ? OFF : ON);
    }

    public override async Task OnStateChangedAsync(CancellationToken ct)
    {
        await base.OnStateChangedAsync(ct);
        WiredSystem.SetDisabledBy(_ctx.ObjectId, IsOn);
    }

    public override async Task OnAttachAsync(CancellationToken ct)
    {
        await base.OnAttachAsync(ct);
        WiredSystem.SetDisabledBy(_ctx.ObjectId, IsOn);
    }

    public override async Task OnDetachAsync(CancellationToken ct)
    {
        await base.OnDetachAsync(ct);
        WiredSystem.SetDisabledBy(_ctx.ObjectId, false);
    }
}
