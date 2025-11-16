using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.NewNavigator;
using Turbo.Primitives.Messages.Outgoing.NewNavigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans.Snapshots.Navigator;

namespace Turbo.PacketHandlers.NewNavigator;

public class NewNavigatorInitMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<NewNavigatorInitMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        NewNavigatorInitMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var topLevelContexts = await _navigatorService
            .GetTopLevelContextAsync()
            .ConfigureAwait(false);

        await ctx
            .Session.SendComposerAsync(
                new NavigatorMetaDataMessage { TopLevelContexts = topLevelContexts },
                ct
            )
            .ConfigureAwait(false);
        await ctx
            .Session.SendComposerAsync(new NavigatorLiftedRoomsMessage { LiftedRooms = [] }, ct)
            .ConfigureAwait(false);
        await ctx
            .Session.SendComposerAsync(
                new NavigatorCollapsedCategoriesMessage { CollapsedCategoryIds = [] },
                ct
            )
            .ConfigureAwait(false);
        await ctx
            .Session.SendComposerAsync(new NavigatorSavedSearchesMessage { SavedSearches = [] }, ct)
            .ConfigureAwait(false);
        await ctx
            .Session.SendComposerAsync(
                new NewNavigatorPreferencesMessageComposer
                {
                    WindowX = 427,
                    WindowY = 41,
                    WindowWidth = 425,
                    WindowHeight = 400,
                    LeftPaneHidden = false,
                    ResultsMode = 1,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
