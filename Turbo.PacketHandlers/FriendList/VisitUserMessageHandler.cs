using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.PacketHandlers.FriendList;

public class VisitUserMessageHandler(IGrainFactory grainFactory) : IMessageHandler<VisitUserMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        VisitUserMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        // Resolve target player ID by name
        var playerDirectory = _grainFactory.GetPlayerDirectoryGrain();
        var targetId = await playerDirectory
            .GetPlayerIdAsync(message.PlayerName, ct)
            .ConfigureAwait(false);

        if (targetId is null)
            return;

        // Get their active room
        var targetPresence = _grainFactory.GetPlayerPresenceGrain(targetId.Value);
        var activeRoom = await targetPresence.GetActiveRoomAsync().ConfigureAwait(false);

        if (activeRoom.RoomId <= 0)
            return;

        // Forward the requester to that room
        var myPresence = _grainFactory.GetPlayerPresenceGrain(ctx.PlayerId);
        await myPresence.SetPendingRoomAsync(activeRoom.RoomId, true).ConfigureAwait(false);
    }
}
