using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// Gives one of the daily respects to a player in the same room.
/// </summary>
public class RespectUserMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RespectUserMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RespectUserMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.PlayerId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .RespectPlayerAsync(ctx.AsActionContext(), message.PlayerId, ct)
            .ConfigureAwait(false);
    }
}
