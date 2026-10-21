using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Habbicons;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Habbicons;

/// <summary>One Habbicon's state and price (<c>HabbiconController.getHabbiconInfo</c>).</summary>
public class GetHabbiconInfoMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetHabbiconInfoMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetHabbiconInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _grainFactory
            .GetPlayerHabbiconGrain(ctx.PlayerId)
            .SendInfoAsync(message.HabbiconId, ct)
            .ConfigureAwait(false);
    }
}
