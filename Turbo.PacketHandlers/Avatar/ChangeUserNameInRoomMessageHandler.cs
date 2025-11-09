using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Avatar;

namespace Turbo.PacketHandlers.Avatar;

public class ChangeUserNameInRoomMessageHandler : IMessageHandler<ChangeUserNameInRoomMessage>
{
    public async ValueTask HandleAsync(
        ChangeUserNameInRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
