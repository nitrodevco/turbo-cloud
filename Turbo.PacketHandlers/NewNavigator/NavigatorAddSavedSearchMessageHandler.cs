using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.NewNavigator;
using Turbo.Primitives.Navigator;

namespace Turbo.PacketHandlers.NewNavigator;

public class NavigatorAddSavedSearchMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<NavigatorAddSavedSearchMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        NavigatorAddSavedSearchMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || string.IsNullOrWhiteSpace(message.SearchCode))
            return;

        await _navigatorService
            .AddSavedSearchAsync(
                ctx.PlayerId,
                message.SearchCode,
                message.Filter ?? string.Empty,
                ct
            )
            .ConfigureAwait(false);
    }
}
