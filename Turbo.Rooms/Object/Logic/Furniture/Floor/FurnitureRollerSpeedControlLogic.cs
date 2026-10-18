using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// The Roller Speed Controller (<c>conf_queue_speed</c>). It sets how often every roller in the
/// room moves, one of <c>RoomConfig.RollerSpeedControlTickMs</c>; without one they move every
/// <c>RoomConfig.RollerTickMs</c>. Each speed has three states: using it goes to the next
/// speed's first state (0, 3, 6, 9, then 0 again), and on every roller move it shows the other
/// two in turn (1 and 2 at the first speed), the room told each time with the move. It is placed
/// at the first, fastest speed, whatever it was at when picked up (official client capture,
/// 2026-10-18: 500/1000/1500/2000 ms, a re-placed controller at "0" and 500 ms 2 of 2 times).
/// Rights holders switch it.
/// </summary>
[RoomObjectLogic("roller_speed_control")]
public class FurnitureRollerSpeedControlLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private const int STATES_PER_SPEED = 3;

    private int Speeds => Math.Max(1, _roomGrain._roomConfig.RollerSpeedControlTickMs.Length);

    /// <summary>The speed it is at, from 0, the fastest.</summary>
    public int Speed => Math.Clamp(GetState() / STATES_PER_SPEED, 0, Speeds - 1);

    /// <summary>How often the room's rollers move at this speed.</summary>
    public int TickMs =>
        _roomGrain._roomConfig.RollerSpeedControlTickMs is { Length: > 0 } ticks
            ? ticks[Speed]
            : _roomGrain._roomConfig.RollerTickMs;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Controller;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (!await HasRightsAsync(ctx))
            return;

        await SetStateAsync((Speed + 1) % Speeds * STATES_PER_SPEED);
    }

    /// <summary>
    /// The rollers moved: it shows the other of its speed's two moving states. The state is only
    /// shown, never saved, so a reloaded room finds it at its speed's first state.
    /// </summary>
    public IComposer OnRollersMoved()
    {
        var first = Speed * STATES_PER_SPEED;

        StuffData.SetState((GetState() == first + 1 ? first + 2 : first + 1).ToString());

        return _ctx.RoomObject.GetRefreshStuffDataComposer();
    }

    public override async Task OnPlaceAsync(ActionContext ctx, CancellationToken ct)
    {
        // Before the room is told of it, so it arrives at the first speed.
        StuffData.SetState("0");
        PersistStuffData(false);

        await base.OnPlaceAsync(ctx, ct);
    }

    public override async Task OnAttachAsync(CancellationToken ct)
    {
        await base.OnAttachAsync(ct);
        await AnnounceAsync(ct);
    }

    public override async Task OnDetachAsync(CancellationToken ct)
    {
        await base.OnDetachAsync(ct);
        await AnnounceAsync(ct);
    }

    // The roller system looks for its rollers and controllers again.
    private Task AnnounceAsync(CancellationToken ct) =>
        _ctx.PublishRoomEventAsync(
            new RoomRollerChangedEvent
            {
                RoomId = _ctx.RoomId,
                CausedBy = ActionContext.CreateForSystem(_ctx.RoomId),
                ObjectId = _ctx.ObjectId,
            },
            ct
        );
}
