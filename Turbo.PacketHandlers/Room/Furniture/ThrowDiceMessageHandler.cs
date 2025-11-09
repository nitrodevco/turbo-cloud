using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

public class ThrowDiceMessageHandler : IMessageHandler<ThrowDiceMessage>
{
    public async ValueTask HandleAsync(
        ThrowDiceMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
