using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Action;

public class AssignRightsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<AssignRightsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        AssignRightsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.PlayerId < 0)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        await roomGrain
            .GiveRightsToPlayerAsync(ctx.AsActionContext(), message.PlayerId, ct)
            .ConfigureAwait(false);
    }
}
