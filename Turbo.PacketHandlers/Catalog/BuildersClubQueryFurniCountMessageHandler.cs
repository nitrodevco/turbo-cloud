using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Catalog;

/// <summary>
/// The client asks once as the catalog opens and refuses to place anything until it is told, so
/// this is what unlocks the warehouse for a session.
/// </summary>
public class BuildersClubQueryFurniCountMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<BuildersClubQueryFurniCountMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        BuildersClubQueryFurniCountMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetBuildersClubGrain()
            .SendFurniCountAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);
    }
}
