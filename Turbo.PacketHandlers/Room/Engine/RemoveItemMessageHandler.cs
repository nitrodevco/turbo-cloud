using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Engine;

/// <summary>
/// Deletes a post-it or photo outright; unlike a pickup nothing returns to an inventory.
/// </summary>
public class RemoveItemMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RemoveItemMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RemoveItemMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ItemId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .DeleteItemByIdAsync(ctx.AsActionContext(), message.ItemId, ct)
            .ConfigureAwait(false);
    }
}
