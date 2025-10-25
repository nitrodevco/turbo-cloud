using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class GetPopularRoomTagsMessageHandler : IMessageHandler<GetPopularRoomTagsMessage>
{
    public async ValueTask HandleAsync(
        GetPopularRoomTagsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
