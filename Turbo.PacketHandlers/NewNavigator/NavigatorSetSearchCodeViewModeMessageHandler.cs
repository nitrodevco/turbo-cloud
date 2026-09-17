using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.NewNavigator;
using Turbo.Primitives.Navigator;

namespace Turbo.PacketHandlers.NewNavigator;

public class NavigatorSetSearchCodeViewModeMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<NavigatorSetSearchCodeViewModeMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        NavigatorSetSearchCodeViewModeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || string.IsNullOrWhiteSpace(message.CategoryName))
            return;

        await _navigatorService
            .SetViewModeAsync(ctx.PlayerId, message.CategoryName, message.ViewMode, ct)
            .ConfigureAwait(false);
    }
}
