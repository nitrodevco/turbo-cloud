using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Engine;

/// <summary>A player clicked another avatar; the room turns it into a wired trigger.</summary>
public class ClickCharacterMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ClickCharacterMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ClickCharacterMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ObjectId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .ClickAvatarAsync(ctx.AsActionContext(), message.ObjectId, ct)
            .ConfigureAwait(false);
    }
}
