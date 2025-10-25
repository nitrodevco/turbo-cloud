using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;

namespace Turbo.PacketHandlers.RoomSettings;

public class GetFlatControllersMessageHandler : IMessageHandler<GetFlatControllersMessage>
{
    public async ValueTask HandleAsync(
        GetFlatControllersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
