using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.NewNavigator;
using Turbo.Primitives.Messages.Outgoing.NewNavigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Orleans.Snapshots.Navigator;

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
        var searchCode = message.SearchCodeOriginal;
        var filterRaw = message.FilteringData;
        NavigatorSearchFilterType filterType = NavigatorSearchFilterType.Anything;
        string filterValue = string.Empty;

        if (!string.IsNullOrWhiteSpace(filterRaw))
        {
            var splitIndex = filterRaw.IndexOf(':');

            if (splitIndex > 0)
            {
                filterType = NavigatorSearchFilterTypeExtensions.FromLegacyString(
                    filterRaw[..splitIndex]
                );
                filterValue = filterRaw[(splitIndex + 1)..];
            }
            else
            {
                filterValue = filterRaw;
            }
        }

        var searchResults = await _navigatorService.GetSearchResultsAsync().ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new NavigatorSearchResultBlocksMessageComposer
                {
                    SearchCodeOriginal = searchCode,
                    FilteringData =
                        filterType == NavigatorSearchFilterType.Anything
                            ? filterValue
                            : $"{filterType.ToLegacyString()}:{filterValue}",
                    Blocks =
                    [
                        new NavigatorSearchResultBlockSnapshot
                        {
                            SearchCode = searchCode,
                            Text = "Search Results",
                            ActionAllowed = NavigatorActionAllowedType.Back,
                            Localization = "sample_result_block",
                            ForceClosed = false,
                            ViewMode = NavigatorViewModeType.Rows,
                            Results = searchResults,
                        },
                    ],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
