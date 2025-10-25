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
            .Session.SendComposerAsync(new NavigatorMetaDataMessage(), ct)
            .ConfigureAwait(false);
        await ctx
            .Session.SendComposerAsync(new NavigatorLiftedRoomsMessage(), ct)
            .ConfigureAwait(false);
        await ctx
            .Session.SendComposerAsync(new NavigatorCollapsedCategoriesMessage(), ct)
            .ConfigureAwait(false);
        await ctx
            .Session.SendComposerAsync(new NavigatorSavedSearchesMessage(), ct)
            .ConfigureAwait(false);
        await ctx
            .Session.SendComposerAsync(new NewNavigatorPreferencesMessage(), ct)
            .ConfigureAwait(false);
    }
}
