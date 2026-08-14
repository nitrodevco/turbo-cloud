using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Navigator;

public class ForwardToSomeRoomMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ForwardToSomeRoomMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ForwardToSomeRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var roomDirectory = _grainFactory.GetRoomDirectoryGrain();
        var randomRoomId = await roomDirectory
            .GetRandomPopulatedRoomAsync(ct)
            .ConfigureAwait(false);

        if (randomRoomId is not null && randomRoomId.Value > 0)
        {
            await ctx.SendComposerAsync(
                    new RoomForwardMessageComposer { RoomId = randomRoomId.Value },
                    ct
                )
                .ConfigureAwait(false);
        }
    }
}
