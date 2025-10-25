using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class SetRoomSessionTagsMessageHandler : IMessageHandler<SetRoomSessionTagsMessage>
{
    public async ValueTask HandleAsync(
        SetRoomSessionTagsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
