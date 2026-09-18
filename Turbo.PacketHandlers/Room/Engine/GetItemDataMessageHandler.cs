using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Engine;

/// <summary>
/// Double-click on a post-it: the client opens its editor once the note data arrives.
/// </summary>
public class GetItemDataMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetItemDataMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetItemDataMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ObjectId <= 0)
            return;

        var data = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetItemDataAsync(message.ObjectId, ct)
            .ConfigureAwait(false);

        if (data is null)
            return;

        await ctx.SendComposerAsync(
                new ItemDataUpdateMessageComposer { ObjectId = message.ObjectId, State = data },
                ct
            )
            .ConfigureAwait(false);
    }
}
