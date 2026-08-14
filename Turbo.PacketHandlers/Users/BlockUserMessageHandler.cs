using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class BlockUserMessageHandler(IGrainFactory grainFactory) : IMessageHandler<BlockUserMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        BlockUserMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerMessengerGrain(ctx.PlayerId)
            .BlockPlayerAsync(message.PlayerId, ct)
            .ConfigureAwait(false);
    }
}
