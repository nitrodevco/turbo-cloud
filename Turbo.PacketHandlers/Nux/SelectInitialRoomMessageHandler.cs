using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Nux;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Nux;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Players.Onboarding;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Nux;

/// <summary>
/// The new user flow's room pick. Habbo answers <c>SelectInitialRoom</c> (0, the room) and
/// <c>FlatCreated</c>; the client then makes it home and goes in. Anything else (the step done, a
/// type the hotel has no room for, no room made) is a non-zero status and room 0, which ends the
/// flow on the hotel view.
/// </summary>
public class SelectInitialRoomMessageHandler(
    IPlayerOnboardingService onboarding,
    IStarterRoomService starterRooms
) : IMessageHandler<SelectInitialRoomMessage>
{
    private const short STATUS_OK = 0;
    private const short STATUS_FAILED = 1;

    public async ValueTask HandleAsync(
        SelectInitialRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        CreatedStarterRoom? room = null;

        if (
            await starterRooms.HasTypeAsync(message.RoomType, ct).ConfigureAwait(false)
            && await onboarding.IsRoomPendingAsync(ctx.PlayerId, ct).ConfigureAwait(false)
            && await onboarding.CompleteRoomAsync(ctx.PlayerId, ct).ConfigureAwait(false)
        )
            room = await starterRooms
                .CreateAsync(ctx.PlayerId, message.RoomType, ct)
                .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new SelectInitialRoomEventMessageComposer
                {
                    Status = room is null ? STATUS_FAILED : STATUS_OK,
                    RoomId = room?.RoomId ?? new RoomId(0),
                },
                ct
            )
            .ConfigureAwait(false);

        if (room is null)
            return;

        await ctx.SendComposerAsync(
                new FlatCreatedMessageComposer { RoomId = room.RoomId, Name = room.Name },
                ct
            )
            .ConfigureAwait(false);
    }
}
