using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Catalog;

public class BuildersClubPlaceWallItemMessageHandler(IRoomService roomService)
    : IMessageHandler<BuildersClubPlaceWallItemMessage>
{
    private readonly IRoomService _roomService = roomService;

    public async ValueTask HandleAsync(
        BuildersClubPlaceWallItemMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || string.IsNullOrEmpty(message.Location))
            return;

        await _roomService
            .PlaceBuildersClubWallItemInRoomAsync(
                ctx.AsActionContext(),
                message.PageId,
                message.OfferId,
                message.ExtraParam ?? string.Empty,
                message.Location,
                message.ConfirmedHideRoom,
                ct
            )
            .ConfigureAwait(false);
    }
}
