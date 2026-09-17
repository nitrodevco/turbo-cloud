using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.NewNavigator;
using Turbo.Primitives.Navigator;

namespace Turbo.PacketHandlers.NewNavigator;

public class NavigatorAddCollapsedCategoryMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<NavigatorAddCollapsedCategoryMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        NavigatorAddCollapsedCategoryMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || string.IsNullOrWhiteSpace(message.CategoryName))
            return;

        await _navigatorService
            .AddCollapsedSearchCodeAsync(ctx.PlayerId, message.CategoryName, ct)
            .ConfigureAwait(false);
    }
}
