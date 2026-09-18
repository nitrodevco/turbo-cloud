using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Layout;
using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Layout;

/// <summary>
/// Floor plan editor: where the door is.
/// </summary>
public class GetRoomEntryTileMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetRoomEntryTileMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetRoomEntryTileMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var map = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetMapSnapshotAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new RoomEntryTileMessageComposer
                {
                    X = map.DoorX,
                    Y = map.DoorY,
                    Rotation = map.DoorRotation,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
