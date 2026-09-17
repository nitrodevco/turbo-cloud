using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;

namespace Turbo.PacketHandlers.Navigator;

public class RoomAdSearchMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<RoomAdSearchMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        RoomAdSearchMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var searchType = Enum.IsDefined((NavigatorSearchType)message.TabId)
            ? (NavigatorSearchType)message.TabId
            : NavigatorSearchType.Events;

        await ctx.SendLegacySearchResultAsync(_navigatorService, searchType, string.Empty, ct)
            .ConfigureAwait(false);
    }
}
