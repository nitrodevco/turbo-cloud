using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Avatar;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Avatar;

/// <summary>
/// Hands the carried drink or snack to the player next to you.
/// </summary>
public class PassCarryItemMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<PassCarryItemMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        PassCarryItemMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.TargetId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .PassHandItemAsync(ctx.AsActionContext(), message.TargetId, ct)
            .ConfigureAwait(false);
    }
}
