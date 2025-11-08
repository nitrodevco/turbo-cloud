using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Session;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Rooms.Abstractions;

namespace Turbo.PacketHandlers.Room;

public class OpenFlatConnectionMessageHandler(IRoomService roomService)
    : IMessageHandler<OpenFlatConnectionMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        OpenFlatConnectionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var roomGrain = await _roomService.GetRoomGrainAsync(message.RoomId).ConfigureAwait(false);
        var snapshot = await roomGrain.GetSnapshotAsync().ConfigureAwait(false);
        var mapSnapshot = await roomGrain.GetMapSnapshotAsync().ConfigureAwait(false);

        ctx.Session.SetActiveRoomId(message.RoomId);

        await ctx
            .Session.SendComposerAsync(
                new OpenConnectionMessageComposer { RoomId = (int)snapshot.Id },
                ct
            )
            .ConfigureAwait(false);

        await ctx
            .Session.SendComposerAsync(
                new RoomReadyMessageComposer
                {
                    RoomType = mapSnapshot.ModelName,
                    RoomId = (int)snapshot.Id,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
