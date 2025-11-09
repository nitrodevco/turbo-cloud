using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Moderator;

namespace Turbo.PacketHandlers.Moderator;

public class GetModeratorRoomInfoMessageHandler : IMessageHandler<GetModeratorRoomInfoMessage>
{
    public async ValueTask HandleAsync(
        GetModeratorRoomInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
