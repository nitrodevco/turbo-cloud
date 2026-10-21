using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Catalog;

public class GetRoomAdPurchaseInfoMessageHandler(
    INavigatorService navigatorService,
    IGrainFactory grainFactory
) : IMessageHandler<GetRoomAdPurchaseInfoMessage>
{
    private readonly INavigatorService _navigatorService = navigatorService;
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetRoomAdPurchaseInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var rooms = await _navigatorService
            .SearchRoomsAsync(ctx.PlayerId, NavigatorSearchType.MyRooms, string.Empty, ct)
            .ConfigureAwait(false);

        // The page sells its club-level-2 offer to a VIP and the other one to anyone else
        // (RoomAdsCatalogWidget.selectedOffer); Habbo answers false to an account without club
        // (official client capture: isVip false). Club and VIP are one level here.
        var isVip = await _grainFactory.HasActiveClubAsync(ctx.PlayerId, ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new RoomAdPurchaseInfoEventMessageComposer
                {
                    IsVip = isVip,
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
