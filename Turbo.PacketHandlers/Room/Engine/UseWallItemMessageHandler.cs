using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;

namespace Turbo.PacketHandlers.Room.Engine;

public class UseWallItemMessageHandler : IMessageHandler<UseWallItemMessage>
{
    public async ValueTask HandleAsync(
        UseWallItemMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
