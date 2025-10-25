using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.NewNavigator;

namespace Turbo.PacketHandlers.NewNavigator;

public class NavigatorRemoveCollapsedCategoryMessageHandler
    : IMessageHandler<NavigatorRemoveCollapsedCategoryMessage>
{
    public async ValueTask HandleAsync(
        NavigatorRemoveCollapsedCategoryMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
