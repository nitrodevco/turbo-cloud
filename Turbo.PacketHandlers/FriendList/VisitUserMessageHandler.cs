using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.FriendList;

/// <summary>
/// Forwards the client to the target player's room. The client then runs the normal navigator
/// entry flow (GetGuestRoom with roomForward), so door checks and prompts apply as usual.
/// </summary>
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

        var playerDirectory = _grainFactory.GetPlayerDirectoryGrain();
        var targetId = await playerDirectory
            .GetPlayerIdAsync(message.PlayerName, ct)
            .ConfigureAwait(false);

        if (targetId is null)
            return;

        var targetPresence = _grainFactory.GetPlayerPresenceGrain(targetId.Value);
        var activeRoom = await targetPresence.GetActiveRoomAsync().ConfigureAwait(false);

        if (activeRoom.RoomId <= 0)
            return;

        await ctx.SendComposerAsync(
                new RoomForwardMessageComposer { RoomId = activeRoom.RoomId },
                ct
            )
            .ConfigureAwait(false);
    }
}
