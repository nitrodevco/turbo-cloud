using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Engine;

/// <summary>
/// Key/value data for map-backed furniture such as a Vimeo display.
/// </summary>
public class SetObjectDataMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetObjectDataMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetObjectDataMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ObjectId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .SetObjectDataAsync(ctx.AsActionContext(), message.ObjectId, message.Data, ct)
            .ConfigureAwait(false);
    }
}
