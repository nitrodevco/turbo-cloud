using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Action;

public class RemoveRightsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RemoveRightsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RemoveRightsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx is null || ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.PlayerIds.Count < 1)
            return;

        var roomGrain = _grainFactory.GetRoomGrain(ctx.RoomId);

        foreach (var playerId in message.PlayerIds)
            await roomGrain
                .RemoveRightsFromPlayerAsync(ctx.AsActionContext(), playerId, ct)
                .ConfigureAwait(false);
    }
}
