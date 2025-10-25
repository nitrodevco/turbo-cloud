using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.NewNavigator;

namespace Turbo.PacketHandlers.NewNavigator;

public class NavigatorSetSearchCodeViewModeMessageHandler
    : IMessageHandler<NavigatorSetSearchCodeViewModeMessage>
{
    public async ValueTask HandleAsync(
        NavigatorSetSearchCodeViewModeMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
