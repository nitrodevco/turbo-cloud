using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class DeleteFavouriteRoomMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<DeleteFavouriteRoomMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        DeleteFavouriteRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
            return;

        await _navigatorService
            .RemoveFavouriteRoomAsync(ctx.PlayerId, message.RoomId, ct)
            .ConfigureAwait(false);
    }
}
