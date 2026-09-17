using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.NewNavigator;
using Turbo.Primitives.Messages.Outgoing.NewNavigator;
using Turbo.Primitives.Navigator;

namespace Turbo.PacketHandlers.NewNavigator;

public class NewNavigatorSearchMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<NewNavigatorSearchMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        NewNavigatorSearchMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var blocks = await _navigatorService
            .SearchAsync(ctx.PlayerId, message.SearchCodeOriginal, message.FilteringData, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new NavigatorSearchResultBlocksMessageComposer
                {
                    SearchCodeOriginal = message.SearchCodeOriginal,
                    FilteringData = message.FilteringData,
                    Blocks = blocks,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
