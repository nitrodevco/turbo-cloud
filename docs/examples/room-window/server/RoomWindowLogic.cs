using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Furni;
using Turbo.Contracts.Rooms;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace RoomWindow;

/// <summary>
/// The window: a linked pair (<c>LinkedPair = true</c>), so one bought is two halves, each naming
/// the other (<see cref="IFurniContext.PairedItemId"/>), as a teleporter's do. Each half looks into
/// the room its other half stands in. Its handlers (<see cref="LookHandler"/>,
/// <see cref="CrossHandler"/>) talk to it with <see cref="WindowAsk"/> messages through the room
/// (<c>IHotel.Rooms.InvokeFurniLogicAsync</c>); it answers in its room's turn.
/// <para>
/// A window is taken as one tile: the room behind it is drawn so that its other half stands on the
/// tile just behind this one (the side away from the way it faces).
/// </para>
/// </summary>
[FurniLogic(LOGIC, LinkedPair = true)]
public sealed class RoomWindowLogic(WindowDirectory directory) : FurniLogic
{
    public const string LOGIC = "room_window";

    /// <summary>The key a crossing player's entry extension carries, for <see cref="WindowArrival"/>.</summary>
    public const string EXTENSION = "roomwindow";

    /// <summary>How long one watch lasts; the client asks again (<see cref="Look"/>) well before.</summary>
    public static readonly TimeSpan WATCH_LEASE = TimeSpan.FromSeconds(60);

    // Placed, its room loaded or the plugin reloaded: the handlers can find it from now on.
    public override Task OnAttachedAsync(IFurniContext ctx, CancellationToken ct)
    {
        directory.Add(ctx.Room, ctx.Self.Id);

        return Task.CompletedTask;
    }

    // Picked up or its room unloading.
    public override Task OnDetachedAsync(IFurniContext ctx, CancellationToken ct)
    {
        directory.Remove(ctx.Room, ctx.Self.Id);

        return Task.CompletedTask;
    }

    public override async Task<FurniLogicReply> OnMessageAsync(
        IFurniContext ctx,
        FurniLogicMessage message,
        RoomUser? sender,
        CancellationToken ct
    )
    {
        if (message.Read<WindowAsk>() is not { } ask)
            return FurniLogicReply.NotHandled;

        switch (ask.Do)
        {
            // Where this half stands and where it looks. Asked of the half in the player's room.
            case WindowAsk.LOOK:
            {
                if (await FarRoomAsync(ctx, ct) is not { } far)
                    return FurniLogicReply.Handled(); // its other half is in an inventory: no view

                var behind = Behind(ctx.Self);

                return FurniLogicReply.Handled(
                    new LookAnswer(ctx.PairedItemId, far.Value, behind.X, behind.Y)
                );
            }

            // Where this half stands. Asked of the far half, in the far room's turn (the handler
            // asks, so no room waits on another).
            case WindowAsk.WHERE:
                return FurniLogicReply.Handled(new RoomTilePoint(ctx.Self.X, ctx.Self.Y));

            // The sender watches the far room: its packets reach them inside WindowRoom envelopes.
            // Through its own pair the watch passes the far room's door (doorbell, password) as a
            // teleporter does; core checks the pair, and a ban still keeps them out. Asked, not
            // awaited: the far room starts with its entry view, then streams.
            case WindowAsk.WATCH:
            {
                if (sender is null || await FarRoomAsync(ctx, ct) is not { } far)
                    return FurniLogicReply.Handled();

                await ctx.WatchRoomAsync(sender, far, new WindowRoom(far.Value), WATCH_LEASE, ct);

                return FurniLogicReply.Handled();
            }

            // The sender clicked a tile of the far room: walk them up to the window, then send
            // them through to that tile.
            case WindowAsk.CROSS:
            {
                if (sender is null || await FarRoomAsync(ctx, ct) is not { } far)
                    return FurniLogicReply.Handled("false");

                var walked = await ctx.WalkToInOrderAsync(
                    sender,
                    [.. Approaches(ctx, sender)],
                    async (user, ct2) =>
                    {
                        // The step through the window is shown, then the forward. Through its own
                        // pair it passes the far room's door; the extension tells the far room's
                        // arrival policy (WindowArrival) which tile was clicked.
                        await ctx.ShowStepAsync(user, Behind(ctx.Self), ct2);
                        await ctx.ForwardToRoomAsync(
                            user,
                            far,
                            new RoomEntryExtension(
                                EXTENSION,
                                JsonSerializer.Serialize(new RoomTilePoint(ask.X, ask.Y))
                            ),
                            ct2
                        );
                    },
                    ct
                );

                return FurniLogicReply.Handled(walked ? "true" : "false");
            }

            default:
                return FurniLogicReply.NotHandled;
        }
    }

    /// <summary>The room the other half stands in; null when it stands in none.</summary>
    private static async Task<RoomId?> FarRoomAsync(IFurniContext ctx, CancellationToken ct) =>
        ctx.PairedItemId == 0 ? null : await ctx.GetRoomOfItemAsync(ctx.PairedItemId, ct);

    /// <summary>In front of the window first, else beside it.</summary>
    private static IEnumerable<RoomTilePoint> Approaches(IFurniContext ctx, RoomUser user)
    {
        if (ctx.TileInFront is { } front)
            yield return front;

        foreach (var tile in ctx.TilesBeside(user).Where(x => x != ctx.TileInFront))
            yield return tile;
    }

    /// <summary>The tile behind the furni: one step against the way it faces.</summary>
    private static RoomTilePoint Behind(RoomFurni self)
    {
        var (dx, dy) = self.Rotation switch
        {
            Rotation.North => (0, -1),
            Rotation.East => (1, 0),
            Rotation.South => (0, 1),
            Rotation.West => (-1, 0),
            _ => (0, 0),
        };

        return new RoomTilePoint(self.X - dx, self.Y - dy);
    }
}

/// <summary>What the plugin's handlers ask a window (a <see cref="FurniLogicMessage"/>'s payload).</summary>
public sealed record WindowAsk(string Do, int X = 0, int Y = 0)
{
    public const string LOOK = "look";
    public const string WHERE = "where";
    public const string WATCH = "watch";
    public const string CROSS = "cross";
}

/// <summary>
/// A window's answer to <see cref="WindowAsk.LOOK"/>: its other half, the room that half stands
/// in, and the tile behind this half, where that half is drawn.
/// </summary>
public sealed record LookAnswer(int PartnerId, int RoomId, int BehindX, int BehindY);
