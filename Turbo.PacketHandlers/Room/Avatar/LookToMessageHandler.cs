using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Avatar;

namespace Turbo.PacketHandlers.Room.Avatar;

public class LookToMessageHandler : IMessageHandler<LookToMessage>
{
    public async ValueTask HandleAsync(
        LookToMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
