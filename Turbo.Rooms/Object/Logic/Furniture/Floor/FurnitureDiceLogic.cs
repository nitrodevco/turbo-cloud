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
/// A dice: throwing it shows the rolling animation, then lands on a face after the configured
/// delay; closing it returns it to the off state. The client only offers throw/close to an
/// avatar standing next to the dice, and the server enforces the same.
/// </summary>
[RoomObjectLogic("dice")]
public class FurnitureDiceLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Everybody;

    // Double-clicking a dice goes through the dedicated throw/close packets, never a plain use.
    public override Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct) =>
        Task.CompletedTask;

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (!IsAvatarAdjacent(ctx))
            return false;

        switch (interaction)
        {
            case ThrowDiceInteraction:
                if (GetState() == DiceStates.ROLLING)
                    return false;

                await SetStateAsync(DiceStates.ROLLING);

                _roomGrain.TimerSystem.Schedule(
                    _ctx.ObjectId,
                    _roomGrain._roomConfig.DiceRollMs,
                    LandAsync
                );

                return true;

            case DiceOffInteraction:
                _roomGrain.TimerSystem.Cancel(_ctx.ObjectId);

                await SetStateAsync(DiceStates.OFF);

                return true;

            default:
                return false;
        }
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        _roomGrain.TimerSystem.Cancel(_ctx.ObjectId);

        return base.OnPickupAsync(ctx, ct);
    }

    private async Task LandAsync(CancellationToken ct)
    {
        var value = Random.Shared.Next(DiceStates.MIN_VALUE, DiceStates.MAX_VALUE + 1);

        await SetStateAsync(value);
    }
}
