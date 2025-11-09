using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

public class SetCustomStackingHeightMessageHandler : IMessageHandler<SetCustomStackingHeightMessage>
{
    public async ValueTask HandleAsync(
        SetCustomStackingHeightMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
