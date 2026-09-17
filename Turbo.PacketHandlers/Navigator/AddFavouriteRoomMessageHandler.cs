using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Navigator;
using Turbo.Primitives.Navigator;

namespace Turbo.PacketHandlers.Navigator;

public class AddFavouriteRoomMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<AddFavouriteRoomMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        AddFavouriteRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.RoomId <= 0)
            return;

        await _navigatorService
            .AddFavouriteRoomAsync(ctx.PlayerId, message.RoomId, ct)
            .ConfigureAwait(false);
    }
}
