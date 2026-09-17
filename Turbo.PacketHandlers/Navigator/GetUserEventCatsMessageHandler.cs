using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class GetUserEventCatsMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<GetUserEventCatsMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        GetUserEventCatsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendComposerAsync(
                new UserEventCatsMessageComposer
                {
                    EventCategories =
                    [
                        .. _navigatorService.GetEventCategories().Where(x => x.Visible),
                    ],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
