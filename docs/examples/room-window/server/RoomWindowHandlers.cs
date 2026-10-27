using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;

namespace RoomWindow;

/// <summary>
/// The client module asks what the room's windows look into (on entering a room, then every half
/// minute to renew the watches). For each window: ask it where it looks, ask its other half where
/// it stands (in that half's own room), answer with where to draw that room, then let the player
/// watch it through the window. Each ask runs in the room it is for, so no room waits on another.
/// </summary>
public sealed class LookHandler(IHotel hotel, WindowDirectory directory)
    : IPluginMessageHandler<Look>
{
    public async ValueTask HandleAsync(Look message, PluginMessageContext ctx, CancellationToken ct)
    {
        if (ctx.RoomId.Value <= 0)
            return;

        var windows = directory.In(ctx.RoomId);
        var views = new List<WindowView>(windows.Count);

        foreach (var windowId in windows)
        {
            var look = (
                await hotel.Rooms.InvokeFurniLogicAsync(
                    ctx.RoomId,
                    windowId,
                    Ask(WindowAsk.LOOK, ctx),
                    ct
                )
            ).Read<LookAnswer>();

            if (look is null)
                continue;

            var far = (
                await hotel.Rooms.InvokeFurniLogicAsync(
                    new RoomId(look.RoomId),
                    look.PartnerId,
                    Ask(WindowAsk.WHERE, ctx),
                    ct
                )
            ).Read<RoomTilePoint?>();

            if (far is not { } partner)
                continue;

            // The far room's tile (0, 0) goes where its window lands on the tile behind ours.
            views.Add(
                new WindowView(
                    windowId,
                    look.RoomId,
                    look.BehindX - partner.X,
                    look.BehindY - partner.Y
                )
            );
        }

        // Where to draw first, so the client has the room drawn before its packets come.
        await ctx.ReplyAsync(new Views([.. views]), ct);

        foreach (var view in views)
            await hotel.Rooms.InvokeFurniLogicAsync(
                ctx.RoomId,
                view.WindowId,
                Ask(WindowAsk.WATCH, ctx),
                ct
            );
    }

    private static FurniLogicMessage Ask(string what, PluginMessageContext ctx) =>
        FurniLogicMessage.Of(RoomWindowLogic.LOGIC, new WindowAsk(what), ctx.PlayerId);
}

/// <summary>
/// The player clicked a tile of the room behind a window: the window walks them to it and sends
/// them through (<see cref="RoomWindowLogic"/>, <see cref="WindowArrival"/>).
/// </summary>
public sealed class CrossHandler(IHotel hotel) : IPluginMessageHandler<Cross>
{
    public async ValueTask HandleAsync(
        Cross message,
        PluginMessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.RoomId.Value <= 0)
            return;

        await hotel.Rooms.InvokeFurniLogicAsync(
            ctx.RoomId,
            message.WindowId,
            FurniLogicMessage.Of(
                RoomWindowLogic.LOGIC,
                new WindowAsk(WindowAsk.CROSS, message.X, message.Y),
                ctx.PlayerId
            ),
            ct
        );
    }
}
