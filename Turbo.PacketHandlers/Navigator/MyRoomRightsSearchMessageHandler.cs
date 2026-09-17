using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;

namespace Turbo.PacketHandlers.Navigator;

public class MyRoomRightsSearchMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<MyRoomRightsSearchMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        MyRoomRightsSearchMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendLegacySearchResultAsync(
                _navigatorService,
                NavigatorSearchType.MyRoomRights,
                string.Empty,
                ct
            )
            .ConfigureAwait(false);
    }
}
