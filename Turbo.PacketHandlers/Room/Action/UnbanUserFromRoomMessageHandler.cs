using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Action;

/// <summary>
/// Lifts a ban from the room settings ban list; the room may not be the one the actor is in.
/// </summary>
public class UnbanUserFromRoomMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UnbanUserFromRoomMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UnbanUserFromRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0 || message.UserId <= 0)
            return;

        var unbanned = await _grainFactory
            .GetRoomGrain(message.RoomId)
            .UnbanPlayerAsync(ctx.AsActionContext(), message.UserId, ct)
            .ConfigureAwait(false);

        if (!unbanned)
            return;

        await ctx.SendComposerAsync(
                new UserUnbannedFromRoomEventMessageComposer
                {
                    RoomId = message.RoomId,
                    PlayerId = message.UserId,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
