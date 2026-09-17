using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.NewNavigator;
using Turbo.Primitives.Messages.Outgoing.NewNavigator;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.NewNavigator;

public class NewNavigatorInitMessageHandler(
    INavigatorService navigatorService,
    IGrainFactory grainFactory
) : IMessageHandler<NewNavigatorInitMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        NewNavigatorInitMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var topLevelContexts = await _navigatorService
            .GetTopLevelContextAsync()
            .ConfigureAwait(false);
        var navigator = await _grainFactory
            .GetPlayerNavigatorGrain(ctx.PlayerId)
            .GetSnapshotAsync(ct)
            .ConfigureAwait(false);
        var settings = await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .GetSettingsAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new NavigatorMetaDataMessage { TopLevelContexts = topLevelContexts },
                ct
            )
            .ConfigureAwait(false);
        await ctx.SendComposerAsync(new NavigatorLiftedRoomsMessage { LiftedRooms = [] }, ct)
            .ConfigureAwait(false);
        await ctx.SendComposerAsync(
                new NavigatorCollapsedCategoriesMessage
                {
                    CollapsedCategoryIds = [.. navigator.CollapsedSearchCodes],
                },
                ct
            )
            .ConfigureAwait(false);
        await ctx.SendComposerAsync(
                new NavigatorSavedSearchesMessage { SavedSearches = [.. navigator.SavedSearches] },
                ct
            )
            .ConfigureAwait(false);
        await ctx.SendComposerAsync(
                new NewNavigatorPreferencesMessageComposer
                {
                    WindowX = settings.NavigatorWindowX,
                    WindowY = settings.NavigatorWindowY,
                    WindowWidth = settings.NavigatorWindowWidth,
                    WindowHeight = settings.NavigatorWindowHeight,
                    LeftPaneHidden = settings.NavigatorLeftPaneHidden,
                    ResultsMode = (int)settings.NavigatorResultsMode,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
