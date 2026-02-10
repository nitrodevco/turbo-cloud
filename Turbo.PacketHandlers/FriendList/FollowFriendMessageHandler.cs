using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.FriendList;

public class FollowFriendMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<FollowFriendMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        FollowFriendMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var messengerGrain = _grainFactory.GetMessengerGrain(ctx.PlayerId);
        var (success, roomId, error) = await messengerGrain
            .FollowFriendAsync(message.PlayerId, ct)
            .ConfigureAwait(false);

        if (!success && error is not null)
        {
            await ctx.SendComposerAsync(
                    new FollowFriendFailedMessageComposer { ErrorCode = error.Value },
                    ct
                )
                .ConfigureAwait(false);
            return;
        }

        if (success)
        {
            // Tell the client to navigate to the friend's room
            await ctx.SendComposerAsync(
                    new RoomForwardMessageComposer { RoomId = RoomId.Parse(roomId) },
                    ct
                )
                .ConfigureAwait(false);
        }
    }
}
