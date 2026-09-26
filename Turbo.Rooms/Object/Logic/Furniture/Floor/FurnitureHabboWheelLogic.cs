using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// The wheel of fortune: spinning plays the client's animation, then the wheel stops on a random
/// segment after the configured delay. A wheel already spinning ignores further spins.
/// </summary>
[RoomObjectLogic("habbo_wheel")]
public class FurnitureHabboWheelLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Everybody;

    public override Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct) =>
        Task.CompletedTask;

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (interaction is not SpinWheelInteraction || !IsAvatarAdjacent(ctx))
            return false;

        if (GetState() == WheelStates.SPINNING)
            return false;

        await SetStateAsync(WheelStates.SPINNING);

        _roomGrain.TimerSystem.Schedule(
            _ctx.ObjectId,
            _roomGrain._roomConfig.WheelSpinMs,
            StopAsync
        );

        return true;
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        _roomGrain.TimerSystem.Cancel(_ctx.ObjectId);

        return base.OnPickupAsync(ctx, ct);
    }

    private Task StopAsync(CancellationToken ct) =>
        SetStateAsync(Random.Shared.Next(WheelStates.MIN_VALUE, WheelStates.MAX_VALUE + 1));
}
