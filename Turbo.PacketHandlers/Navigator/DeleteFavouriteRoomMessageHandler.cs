using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class DeleteFavouriteRoomMessageHandler : IMessageHandler<DeleteFavouriteRoomMessage>
{
    public async ValueTask HandleAsync(
        DeleteFavouriteRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
