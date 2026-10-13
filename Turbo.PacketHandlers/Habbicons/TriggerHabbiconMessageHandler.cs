using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Habbicons;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Habbicons;

/// <summary>A Habbicon shown over the avatar from the chat bar's picker (<c>HabbiconSelector</c>).</summary>
public class TriggerHabbiconMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<TriggerHabbiconMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        TriggerHabbiconMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        await _grainFactory
            .GetPlayerHabbiconGrain(ctx.PlayerId)
            .TriggerAsync(ctx.AsActionContext(), message.HabbiconId, ct)
            .ConfigureAwait(false);
    }
}
