using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Furniture;

namespace Turbo.PacketHandlers.Room.Furniture;

public class OpenMysteryTrophyMessageHandler : IMessageHandler<OpenMysteryTrophyMessage>
{
    public async ValueTask HandleAsync(
        OpenMysteryTrophyMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
