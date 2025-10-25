using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.RoomSettings;

namespace Turbo.PacketHandlers.RoomSettings;

public class GetBannedUsersFromRoomMessageHandler : IMessageHandler<GetBannedUsersFromRoomMessage>
{
    public async ValueTask HandleAsync(
        GetBannedUsersFromRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
