using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;

namespace Turbo.PacketHandlers.Navigator;

internal static class LegacyRoomSearchExtensions
{
    /// <summary>
    /// Runs a legacy navigator search and answers with the result list the client is waiting on;
    /// the search type is echoed so the client can match the response to its tab.
    /// </summary>
    public static async Task SendLegacySearchResultAsync(
        this MessageContext ctx,
        INavigatorService navigatorService,
        NavigatorSearchType searchType,
        string searchParam,
        CancellationToken ct
    )
    {
        var rooms = await navigatorService
            .SearchRoomsAsync(ctx.PlayerId, searchType, searchParam, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new GuestRoomSearchResultMessageComposer
                {
                    SearchType = searchType,
                    SearchParam = searchParam,
                    Rooms = [.. rooms],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
