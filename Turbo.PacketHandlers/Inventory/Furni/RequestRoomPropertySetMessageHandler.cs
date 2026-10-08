using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Inventory.Furni;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Inventory.Furni;

/// <summary>
/// Sent when a wallpaper, floor or landscape is used from the inventory, in place of starting a
/// placement. The room decides whether the player may and tells everyone in it; the client
/// gets no reply of its own, and nothing changes when it is refused.
/// </summary>
public class RequestRoomPropertySetMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RequestRoomPropertySetMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RequestRoomPropertySetMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ItemId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .ApplyDecorationAsync(ctx.AsActionContext(), message.ItemId, ct)
            .ConfigureAwait(false);
    }
}
