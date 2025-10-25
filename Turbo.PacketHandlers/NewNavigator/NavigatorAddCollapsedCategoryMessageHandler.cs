using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.NewNavigator;

namespace Turbo.PacketHandlers.NewNavigator;

public class NavigatorAddCollapsedCategoryMessageHandler
    : IMessageHandler<NavigatorAddCollapsedCategoryMessage>
{
    public async ValueTask HandleAsync(
        NavigatorAddCollapsedCategoryMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
