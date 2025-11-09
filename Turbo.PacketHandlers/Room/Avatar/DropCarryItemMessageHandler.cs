using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Avatar;

namespace Turbo.PacketHandlers.Room.Avatar;

public class DropCarryItemMessageHandler : IMessageHandler<DropCarryItemMessage>
{
    public async ValueTask HandleAsync(
        DropCarryItemMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
