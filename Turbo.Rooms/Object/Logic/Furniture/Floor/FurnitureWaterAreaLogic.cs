using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// Room-derived neighbour state consumed by AS3 FurnitureWaterAreaVisualization.
/// Shore state belongs to the active room; definition walk and stack rules are inherited.
/// </summary>
[RoomObjectLogic("furniture_water_area")]
public sealed class FurnitureWaterAreaLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private int _originalState;
    private bool _capturedOriginalState;

    protected override StuffPersistanceType _stuffPersistanceType =>
        StuffPersistanceType.RoomActive;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public override bool CanToggle() => false;

    public override Task OnAttachAsync(CancellationToken ct)
    {
        if (!_capturedOriginalState)
        {
            _originalState = StuffData.GetState();
            _capturedOriginalState = true;
        }

        return base.OnAttachAsync(ct);
    }

    public void SetDerivedState(int state)
    {
        if (StuffData.GetState() == state)
            return;

        StuffData.SetState(state.ToString());
    }

    public void ResetPickedUpState()
    {
        StuffData.SetState(_originalState.ToString());
    }

    public override Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct) =>
        Task.CompletedTask;

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        ResetPickedUpState();
        return base.OnPickupAsync(ctx, ct);
    }
}
