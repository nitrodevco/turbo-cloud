using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Action;

/// <summary>
/// "Remove all rights" from the room settings window. The message names the room, so it is
/// honoured for that room rather than whichever room the player is currently in.
/// </summary>
public class RemoveAllRightsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RemoveAllRightsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RemoveAllRightsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(message.RoomId)
            .RemoveAllRightsAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);
    }
}
