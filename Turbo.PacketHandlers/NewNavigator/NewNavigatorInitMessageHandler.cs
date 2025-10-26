using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.NewNavigator;
using Turbo.Primitives.Messages.Outgoing.NewNavigator;

namespace Turbo.PacketHandlers.NewNavigator;

public class NewNavigatorInitMessageHandler : IMessageHandler<NewNavigatorInitMessage>
{
    public async ValueTask HandleAsync(
        NewNavigatorInitMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx
            .Session.SendComposerAsync(new NavigatorMetaDataMessage { TopLevelContexts = [] }, ct)
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
                new NewNavigatorPreferencesMessage
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
