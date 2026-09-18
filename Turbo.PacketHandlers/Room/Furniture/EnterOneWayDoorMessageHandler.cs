using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Furniture;

/// <summary>
/// Walks the avatar through a one-way gate from the tile in front of it.
/// </summary>
public class EnterOneWayDoorMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<EnterOneWayDoorMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        EnterOneWayDoorMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ObjectId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .InteractWithItemAsync(
                ctx.AsActionContext(),
                message.ObjectId,
                FurnitureInteractionType.EnterOneWayDoor,
                ct
            )
            .ConfigureAwait(false);
    }
}
