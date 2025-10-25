using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class RoomsWhereMyFriendsAreSearchMessageHandler
    : IMessageHandler<RoomsWhereMyFriendsAreSearchMessage>
{
    public async ValueTask HandleAsync(
        RoomsWhereMyFriendsAreSearchMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
