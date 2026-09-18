using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Avatar;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Avatar;

/// <summary>
/// Turns the avatar towards a clicked tile it cannot or need not walk to.
/// </summary>
public class LookToMessageHandler(IGrainFactory grainFactory) : IMessageHandler<LookToMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        LookToMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .LookToAsync(ctx.AsActionContext(), message.X, message.Y, ct)
            .ConfigureAwait(false);
    }
}
