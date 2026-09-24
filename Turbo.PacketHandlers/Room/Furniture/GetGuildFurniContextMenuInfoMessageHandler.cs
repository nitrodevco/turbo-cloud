using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// The menu that opens when somebody clicks a piece of guild furni. Which entries it draws
/// depends on whether the viewer is in that group and whether they could read its forum, so
/// both are answered per viewer.
/// </summary>
public class GetGuildFurniContextMenuInfoMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetGuildFurniContextMenuInfoMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetGuildFurniContextMenuInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .SendGuildFurniContextMenuAsync(ctx.PlayerId, message.ObjectId, ct)
            .ConfigureAwait(false);
    }
}
