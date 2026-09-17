using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;

namespace Turbo.PacketHandlers.Navigator;

public class MyFrequentRoomHistorySearchMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<MyFrequentRoomHistorySearchMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        MyFrequentRoomHistorySearchMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendLegacySearchResultAsync(
                _navigatorService,
                NavigatorSearchType.MyFrequentHistory,
                string.Empty,
                ct
            )
            .ConfigureAwait(false);
    }
}
