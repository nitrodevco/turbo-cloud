using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class GetPopularRoomTagsMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<GetPopularRoomTagsMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        GetPopularRoomTagsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var tags = await _navigatorService.GetPopularTagsAsync(ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(new PopularRoomTagsResultMessageComposer { Tags = tags }, ct)
            .ConfigureAwait(false);
    }
}
