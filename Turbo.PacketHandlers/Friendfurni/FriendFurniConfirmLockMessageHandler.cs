using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Friendfurni;

namespace Turbo.PacketHandlers.Friendfurni;

public class FriendFurniConfirmLockMessageHandler : IMessageHandler<FriendFurniConfirmLockMessage>
{
    public async ValueTask HandleAsync(
        FriendFurniConfirmLockMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
