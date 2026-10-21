using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// A note written in the editor a post-it wall opened (Flash
/// <c>SpamWallPostItWidgetHandler</c>): the id is the player's post-it pad the request named, not
/// a room item, and the location is where the note goes. The room decides; the client gets no
/// reply of its own, and nothing changes when it is refused.
/// </summary>
public class AddSpamWallPostItMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<AddSpamWallPostItMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        AddSpamWallPostItMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.RoomId <= 0 || message.ObjectId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .AddSpamWallPostItAsync(
                ctx.AsActionContext(),
                message.ObjectId,
                message.Location,
                message.Color,
                message.Text,
                ct
            )
            .ConfigureAwait(false);
    }
}
