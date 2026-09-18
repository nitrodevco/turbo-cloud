using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Avatar;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Avatar;

/// <summary>
/// Drops the carried item.
/// </summary>
public class DropCarryItemMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<DropCarryItemMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        DropCarryItemMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .DropHandItemAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);
    }
}
