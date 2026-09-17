using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Navigator;

public class EditEventMessageHandler(IGrainFactory grainFactory) : IMessageHandler<EditEventMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        EditEventMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .UpdateEventAsync(
                ctx.AsActionContext(),
                message.Id,
                message.Name.Trim(),
                message.Description.Trim(),
                ct
            )
            .ConfigureAwait(false);
    }
}
