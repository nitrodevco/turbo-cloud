using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;

namespace Turbo.PacketHandlers.Navigator;

public class RoomTextSearchMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<RoomTextSearchMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        RoomTextSearchMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var query = message.Query?.Trim() ?? string.Empty;

        if (query.Length == 0)
            return;

        // The legacy client picks the search type with a prefix on the query text.
        var separator = query.IndexOf(':');
        var searchType = (separator > 0 ? query[..separator] : string.Empty) switch
        {
            "tag" => NavigatorSearchType.TagSearch,
            "roomname" => NavigatorSearchType.RoomNameSearch,
            "owner" => NavigatorSearchType.ByOwner,
            "group" => NavigatorSearchType.GroupNameSearch,
            _ => NavigatorSearchType.TextSearch,
        };

        await ctx.SendLegacySearchResultAsync(_navigatorService, searchType, query, ct)
            .ConfigureAwait(false);
    }
}
