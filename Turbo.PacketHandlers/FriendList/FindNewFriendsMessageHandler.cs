using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.FriendList;

public class FindNewFriendsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<FindNewFriendsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        FindNewFriendsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        // Try to find a random populated room
        var roomDirectory = _grainFactory.GetRoomDirectoryGrain();
        var randomRoomId = await roomDirectory
            .GetRandomPopulatedRoomAsync(ct)
            .ConfigureAwait(false);

        if (randomRoomId is not null && randomRoomId.Value > 0)
        {
            await ctx.SendComposerAsync(
                    new FindFriendsProcessResultMessageComposer { Success = true },
                    ct
                )
                .ConfigureAwait(false);

            // Navigate the player to the random room
            await ctx.SendComposerAsync(
                    new RoomForwardMessageComposer { RoomId = randomRoomId.Value },
                    ct
                )
                .ConfigureAwait(false);
        }
        else
        {
            await ctx.SendComposerAsync(
                    new FindFriendsProcessResultMessageComposer { Success = false },
                    ct
                )
                .ConfigureAwait(false);
        }
    }
}
