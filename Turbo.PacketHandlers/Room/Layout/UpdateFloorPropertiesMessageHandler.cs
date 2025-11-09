using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Layout;

namespace Turbo.PacketHandlers.Room.Layout;

public class UpdateFloorPropertiesMessageHandler : IMessageHandler<UpdateFloorPropertiesMessage>
{
    public async ValueTask HandleAsync(
        UpdateFloorPropertiesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
