using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Engine;

namespace Turbo.PacketHandlers.Room.Engine;

public class TogglePetRidingPermissionMessageHandler
    : IMessageHandler<TogglePetRidingPermissionMessage>
{
    public async ValueTask HandleAsync(
        TogglePetRidingPermissionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
