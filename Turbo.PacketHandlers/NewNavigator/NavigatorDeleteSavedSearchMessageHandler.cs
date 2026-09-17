using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.NewNavigator;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.NewNavigator;

public class NavigatorDeleteSavedSearchMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<NavigatorDeleteSavedSearchMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        NavigatorDeleteSavedSearchMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerNavigatorGrain(ctx.PlayerId)
            .DeleteSavedSearchAsync(message.SearchId, ct)
            .ConfigureAwait(false);
    }
}
