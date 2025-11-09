using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Avatar;

namespace Turbo.PacketHandlers.Room.Avatar;

public class PassCarryItemMessageHandler : IMessageHandler<PassCarryItemMessage>
{
    public async ValueTask HandleAsync(
        PassCarryItemMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
