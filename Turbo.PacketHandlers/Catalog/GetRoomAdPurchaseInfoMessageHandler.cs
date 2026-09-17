using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;

namespace Turbo.PacketHandlers.Catalog;

public class GetRoomAdPurchaseInfoMessageHandler(INavigatorService navigatorService)
    : IMessageHandler<GetRoomAdPurchaseInfoMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;

    public async ValueTask HandleAsync(
        GetRoomAdPurchaseInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var rooms = await _navigatorService
            .SearchRoomsAsync(ctx.PlayerId, NavigatorSearchType.MyRooms, string.Empty, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new RoomAdPurchaseInfoEventMessageComposer
                {
                    IsVip = true,
                    Rooms =
                    [
                        .. rooms.Select(x => new RoomAdPurchaseRoomSnapshot
                        {
                            RoomId = x.RoomId,
                            Name = x.Name,
                            HasControllers = false,
                        }),
                    ],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
